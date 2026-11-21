using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Contracts;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/specialties")]
public class SpecialtiesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("SELECT id,code,name FROM sigm.specialties WHERE is_active ORDER BY name", c);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), code = rd.GetString(1), name = rd.GetString(2) });
        return Ok(list);
    }

    [HttpGet("tenant")]
    [Authorize]
    [RequirePermission("tenant.specialties.read")]
    public async Task<IActionResult> ListForTenant(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT s.id,s.code,s.name,ts.is_active
            FROM sigm.tenant_specialties ts JOIN sigm.specialties s ON s.id=ts.specialty_id
            WHERE ts.tenant_id=@t AND ts.is_active ORDER BY s.name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), code = rd.GetString(1), name = rd.GetString(2), isActive = rd.GetBoolean(3) });
        return Ok(list);
    }

    [HttpPut("tenant")]
    [Authorize]
    [RequirePermission("tenant.specialties.manage")]
    public async Task<IActionResult> Enable([FromBody] EnableSpecialtiesRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var ids = (req.SpecialtyIds ?? []).Distinct().ToArray();
        await using (var cmd = new NpgsqlCommand("UPDATE sigm.tenant_specialties SET is_active = (specialty_id = ANY(@ids)), updated_at=now() WHERE tenant_id=@t", c, tx))
        { cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@ids", ids); await cmd.ExecuteNonQueryAsync(ct); }
        foreach (var sid in ids)
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.tenant_specialties(tenant_id,specialty_id,is_active)
                SELECT @t,@s,true WHERE EXISTS(SELECT 1 FROM sigm.specialties WHERE id=@s AND is_active)
                ON CONFLICT (tenant_id,specialty_id) DO UPDATE SET is_active=true, updated_at=now()", c, tx);
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@s", sid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
