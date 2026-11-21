using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGM.Api.Contracts;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/organizations")]
public class OrganizationsController(SigmDbContext db, TenantConnectionFactory cf) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<OrganizationCreatedResponse>> Create([FromBody] RegisterOrganizationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TenantCode) || string.IsNullOrWhiteSpace(req.AdminEmail))
            return BadRequest(new { message = "TenantCode y AdminEmail son obligatorios." });
        if (req.ProductType is not ("CLINIC" or "PRACTICE"))
            return BadRequest(new { message = "ProductType debe ser CLINIC o PRACTICE." });

        await using var conn = await cf.OpenAsync(ct, platform: true);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var email = req.AdminEmail.Trim().ToLowerInvariant();
        var hash = PasswordService.Hash(req.AdminPassword);

        try
        {
            await Exec(conn, tx, ct,
                @"INSERT INTO sigm.tenants(id,tenant_code,legal_name,commercial_name,tax_id,product_type,status)
                  VALUES(@id,@code,@legal,@comm,@tax,@ptype,'ACTIVE')",
                ("@id", tenantId), ("@code", req.TenantCode.Trim()), ("@legal", req.LegalName.Trim()),
                ("@comm", req.CommercialName.Trim()), ("@tax", (object?)req.TaxId ?? DBNull.Value),
                ("@ptype", req.ProductType));

            await Exec(conn, tx, ct,
                @"INSERT INTO sigm.organizations(id,tenant_id,legal_name,commercial_name,tax_id,organization_type,is_primary,status,email)
                  VALUES(@oid,@tid,@legal,@comm,@tax,@ptype,true,'ACTIVE',@email)",
                ("@oid", orgId), ("@tid", tenantId), ("@legal", req.LegalName.Trim()), ("@comm", req.CommercialName.Trim()),
                ("@tax", (object?)req.TaxId ?? DBNull.Value), ("@ptype", req.ProductType), ("@email", email));

            Guid? planId = req.PlanId;
            if (planId is null)
            {
                await using var q = new NpgsqlCommand("SELECT id FROM sigm.plans WHERE is_active AND (product_type=@p OR product_type='BOTH') ORDER BY price LIMIT 1", conn, tx);
                q.Parameters.AddWithValue("@p", req.ProductType);
                planId = (Guid?)await q.ExecuteScalarAsync(ct);
            }
            if (planId is Guid pid)
            {
                await Exec(conn, tx, ct,
                    @"INSERT INTO sigm.tenant_subscriptions(tenant_id,plan_id,status,started_at,current_period_start)
                      VALUES(@tid,@pid,'TRIAL',now(),now())",
                    ("@tid", tenantId), ("@pid", pid));
            }

            await Exec(conn, tx, ct,
                @"INSERT INTO sigm.users(id,email,password_hash,full_name,is_active)
                  VALUES(@id,@email,@hash,@name,true)
                  ON CONFLICT (email) DO UPDATE SET full_name=EXCLUDED.full_name",
                ("@id", userId), ("@email", email), ("@hash", hash), ("@name", req.AdminFullName.Trim()));

            await using (var q = new NpgsqlCommand("SELECT id FROM sigm.users WHERE email=@email", conn, tx))
            { q.Parameters.AddWithValue("@email", email); userId = (Guid)(await q.ExecuteScalarAsync(ct))!; }

            await Exec(conn, tx, ct,
                @"INSERT INTO sigm.user_tenants(tenant_id,user_id,status,joined_at)
                  VALUES(@tid,@uid,'ACTIVE',now()) ON CONFLICT DO NOTHING",
                ("@tid", tenantId), ("@uid", userId));

            string roleCode = req.ProductType == "CLINIC" ? "TENANT_ADMIN" : "PRACTICE_OWNER";
            Guid roleId = Guid.Empty;
            await using (var q = new NpgsqlCommand("SELECT id FROM sigm.roles WHERE code=@c AND tenant_id IS NULL LIMIT 1", conn, tx))
            {
                q.Parameters.AddWithValue("@c", roleCode);
                var r = await q.ExecuteScalarAsync(ct);
                if (r is null) { roleCode = "TENANT_ADMIN"; q.Parameters.Clear(); q.Parameters.AddWithValue("@c", roleCode); r = await q.ExecuteScalarAsync(ct); }
                if (r is Guid g) roleId = g;
            }
            if (roleId != Guid.Empty)
            {
                await Exec(conn, tx, ct,
                    @"INSERT INTO sigm.user_roles(tenant_id,user_id,role_id) VALUES(@tid,@uid,@rid) ON CONFLICT DO NOTHING",
                    ("@tid", tenantId), ("@uid", userId), ("@rid", roleId));
            }

            foreach (var sid in (req.SpecialtyIds ?? []).Distinct())
            {
                await Exec(conn, tx, ct,
                    @"INSERT INTO sigm.tenant_specialties(tenant_id,specialty_id,is_active)
                      SELECT @tid,@sid,true WHERE EXISTS(SELECT 1 FROM sigm.specialties WHERE id=@sid AND is_active)
                      ON CONFLICT (tenant_id,specialty_id) DO UPDATE SET is_active=true",
                    ("@tid", tenantId), ("@sid", sid));
            }

            await tx.CommitAsync(ct);
            return Created($"/api/v1/organizations/{tenantId}", new OrganizationCreatedResponse(tenantId, userId, req.ProductType));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { message = "Ya existe un tenant o usuario con los datos proporcionados.", detail = ex.ConstraintName });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    [HttpGet("{tenantId:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.TenantId == tenantId, ct);
        if (org is null) return NotFound();
        return Ok(org);
    }

    private static async Task Exec(NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct, string sql, params (string name, object value)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
