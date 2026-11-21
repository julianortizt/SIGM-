using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGM.Api.Contracts;
using SIGM.Api.Security;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(SigmDbContext db, ITenantContext tenant, TenantConnectionFactory cf) : ControllerBase
{
    [HttpGet]
    [RequirePermission("tenant.users.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var users = await (from ut in db.UserTenants.AsNoTracking()
                           join u in db.Users.AsNoTracking() on ut.UserId equals u.Id
                           where ut.TenantId == tid
                           select new { u.Id, u.Email, u.FullName, u.Phone, u.IsActive, ut.Status }).ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost]
    [RequirePermission("tenant.users.manage")]
    public async Task<IActionResult> Create([FromBody] CreateTenantUserRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var conn = await cf.OpenAsync(ct, platform: true);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Correo y nombre completo son obligatorios." });

        var requestedRoles = (req.RoleCodes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();
        // Los roles globales son plantillas disponibles para todos los tenants, PERO los roles
        // de plataforma (PLATFORM_ADMIN y cualquier roleCode que conceda permisos platform.*)
        // nunca pueden asignarse desde un panel de tenant: permitirlo era una escalada
        // de privilegios confirmada (un CLINIC_ADMIN podía crear superadministradores).
        var validRoles = new List<string>();
        foreach (var code in requestedRoles)
        {
            await using var q = new NpgsqlCommand(@"SELECT r.code FROM sigm.roles r
                WHERE r.code=@c AND (r.tenant_id IS NULL OR r.tenant_id=@t)
                  AND left(r.code, 9) <> 'PLATFORM_'
                  AND NOT EXISTS (
                      SELECT 1 FROM sigm.role_permissions rp
                      JOIN sigm.permissions p ON p.id=rp.permission_id
                      WHERE rp.role_id=r.id AND p.code LIKE 'platform.%')
                LIMIT 1", conn, tx);
            q.Parameters.AddWithValue("@c", code); q.Parameters.AddWithValue("@t", tid);
            if (await q.ExecuteScalarAsync(ct) is not null) validRoles.Add(code);
        }
        var invalidRoles = requestedRoles.Except(validRoles).ToArray();
        if (invalidRoles.Length > 0)
            return BadRequest(new { message = "Roles no válidos para esta organización: " + string.Join(", ", invalidRoles) });

        var userId = Guid.NewGuid();
        var hash = PasswordService.Hash(req.Password ?? Guid.NewGuid().ToString("N"));
        var correoYaExistia = false;

        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.users(id,email,password_hash,full_name,phone,is_active)
            VALUES(@id,@email,@hash,@name,@phone,true) ON CONFLICT (email) DO NOTHING RETURNING id", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@name", req.FullName.Trim());
            cmd.Parameters.AddWithValue("@phone", (object?)req.Phone ?? DBNull.Value);
            var existing = await cmd.ExecuteScalarAsync(ct);
            if (existing is Guid e) userId = e;
            else
            {
                correoYaExistia = true;
                await using var q = new NpgsqlCommand("SELECT id FROM sigm.users WHERE email=@email", conn, tx);
                q.Parameters.AddWithValue("@email", email);
                userId = (Guid)(await q.ExecuteScalarAsync(ct))!;
            }
        }

        // Un correo que ya pertenece a otra organización no puede vincularse automáticamente
        // a esta: eso creaba membresías cruzadas y permitía heredar permisos ajenos.
        if (correoYaExistia)
        {
            await using var own = new NpgsqlCommand(@"SELECT EXISTS (
                SELECT 1 FROM sigm.user_tenants WHERE tenant_id=@t AND user_id=@u)", conn, tx);
            own.Parameters.AddWithValue("@t", tid); own.Parameters.AddWithValue("@u", userId);
            if (await own.ExecuteScalarAsync(ct) is not true)
                return Conflict(new { message = "Ese correo ya pertenece a otra organización. Use una cuenta propia de esta organización." });
        }

        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.user_tenants(tenant_id,user_id,status,joined_at) VALUES(@tid,@uid,'ACTIVE',now()) ON CONFLICT DO NOTHING", conn, tx))
        { cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@uid", userId); await cmd.ExecuteNonQueryAsync(ct); }

        foreach (var code in validRoles)
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.user_roles(tenant_id,user_id,role_id)
                SELECT @tid,@uid,r.id FROM sigm.roles r WHERE r.code=@c AND (r.tenant_id IS NULL OR r.tenant_id=@tid)
                  AND left(r.code, 9) <> 'PLATFORM_'
                  AND NOT EXISTS (
                      SELECT 1 FROM sigm.role_permissions rp
                      JOIN sigm.permissions p ON p.id=rp.permission_id
                      WHERE rp.role_id=r.id AND p.code LIKE 'platform.%')
                LIMIT 1
                ON CONFLICT DO NOTHING", conn, tx);
            cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@uid", userId); cmd.Parameters.AddWithValue("@c", code);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Ok(new { id = userId, email });
    }
}
