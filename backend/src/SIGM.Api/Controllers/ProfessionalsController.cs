using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/professionals")]
[Authorize]
public class ProfessionalsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record CreateProfessionalRequest(Guid UserId, string DisplayName, string ProfessionalType, string? ProfessionalLicense, Guid? PrimarySiteId, Guid[] SpecialtyIds);

    [HttpGet]
    [RequirePermission("tenant.professionals.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        var pros = new List<dynamic>();
        await using (var cmd = new NpgsqlCommand("SELECT id,user_id,display_name,professional_type,professional_license,primary_site_id,is_active FROM sigm.professionals WHERE tenant_id=@t ORDER BY display_name", c))
        {
            cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) pros.Add(new { id = rd.GetGuid(0), userId = rd.GetGuid(1), displayName = rd.GetString(2), professionalType = rd.GetString(3), professionalLicense = rd.IsDBNull(4)?null:rd.GetString(4), primarySiteId = rd.IsDBNull(5)?(Guid?)null:rd.GetGuid(5), isActive = rd.GetBoolean(6) });
        }
        var specs = new List<(Guid pid, Guid sid, bool active, string vs)>();
        await using (var cmd = new NpgsqlCommand("SELECT professional_id,specialty_id,is_active,verification_status FROM sigm.professional_specialties WHERE tenant_id=@t", c))
        {
            cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) specs.Add((rd.GetGuid(0), rd.GetGuid(1), rd.GetBoolean(2), rd.IsDBNull(3)?"":rd.GetString(3)));
        }
        var result = pros.Select(p => new {
            p.id, p.userId, p.displayName, p.professionalType, p.professionalLicense, p.primarySiteId, p.isActive,
            specialties = specs.Where(s => s.pid == (Guid)p.id).Select(s => new { specialtyId = s.sid, isActive = s.active, verificationStatus = s.vs })
        });
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("tenant.professionals.manage")]
    public async Task<IActionResult> Create([FromBody] CreateProfessionalRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.professionals(id,tenant_id,user_id,display_name,professional_type,professional_license,primary_site_id,is_active)
                                VALUES(@id,@tid,@uid,@name,@type,@lic,@site,true)", c, tx))
        {
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@uid", req.UserId);
            cmd.Parameters.AddWithValue("@name", req.DisplayName.Trim()); cmd.Parameters.AddWithValue("@type", req.ProfessionalType ?? "PHYSICIAN");
            cmd.Parameters.AddWithValue("@lic", (object?)req.ProfessionalLicense ?? DBNull.Value); cmd.Parameters.AddWithValue("@site", (object?)req.PrimarySiteId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach (var sid in (req.SpecialtyIds ?? []).Distinct())
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.professional_specialties(tenant_id,professional_id,specialty_id,is_active,verification_status)
                                SELECT @tid,@pid,@sid,true,'PENDING'
                                WHERE EXISTS(SELECT 1 FROM sigm.tenant_specialties WHERE tenant_id=@tid AND specialty_id=@sid AND is_active)
                                ON CONFLICT DO NOTHING", c, tx);
            cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@pid", id); cmd.Parameters.AddWithValue("@sid", sid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new { id });
    }
}
