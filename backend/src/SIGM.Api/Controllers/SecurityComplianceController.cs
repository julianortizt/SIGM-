using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/security-compliance")]
[Authorize]
public class SecurityComplianceController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("personal-data-requests")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListPdr(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,patient_id,request_type,description,status,requested_at,resolved_at,resolution,handled_by FROM sigm.personal_data_requests WHERE tenant_id=@t ORDER BY requested_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("personal-data-requests")][RequirePermission("patients.update")]
    public async Task<IActionResult> CreatePdr([FromBody] PdrReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.personal_data_requests(id,tenant_id,patient_id,request_type,description,status) VALUES(gen_random_uuid(),@t,@p,@type,@desc,'RECEIVED')", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", req.PatientId); cmd.Parameters.AddWithValue("@type", req.RequestType); cmd.Parameters.AddWithValue("@desc", req.Description);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    [HttpPost("personal-data-requests/{id:guid}/resolve")][RequirePermission("patients.update")]
    public async Task<IActionResult> ResolvePdr(Guid id, [FromBody] PdrResolveReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.personal_data_requests SET status=@s, resolved_at=now(), resolution=@r, handled_by=@u, updated_at=now() WHERE id=@id AND tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@s", req.Status ?? "RESOLVED"); cmd.Parameters.AddWithValue("@r", (object?)req.Resolution ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound() : Ok();
    }

    [HttpGet("security-incidents")][RequirePermission("audit.read")]
    public async Task<IActionResult> ListIncidents(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,incident_type,severity,description,status,detected_at,resolved_at,reported_to,metadata FROM sigm.security_incidents WHERE tenant_id=@t OR tenant_id IS NULL ORDER BY detected_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("security-incidents")][RequirePermission("audit.read")]
    public async Task<IActionResult> CreateIncident([FromBody] IncidentReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.security_incidents(id,tenant_id,incident_type,severity,description,status,detected_at,metadata)
            VALUES(gen_random_uuid(),@t,@type,@sev,@desc,'OPEN',now(),COALESCE(@meta::jsonb,'{}'::jsonb))", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@type", req.IncidentType); cmd.Parameters.AddWithValue("@sev", req.Severity ?? "MEDIUM");
        cmd.Parameters.AddWithValue("@desc", req.Description); cmd.Parameters.AddWithValue("@meta", (object?)req.Metadata ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("authentication-events")][RequirePermission("audit.read")]
    public async Task<IActionResult> AuthEvents(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,user_id,event_type,success,ip_address,user_agent,detail,occurred_at FROM sigm.authentication_events WHERE tenant_id=@t OR tenant_id IS NULL ORDER BY occurred_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }

    [HttpGet("sessions")][RequirePermission("tenant.users.read")]
    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,user_id,ip_address,user_agent,created_at,last_seen_at,expires_at,revoked_at,revoked_reason FROM sigm.user_sessions WHERE tenant_id=@t ORDER BY created_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("sessions/{id:guid}/revoke")][RequirePermission("tenant.users.manage")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.user_sessions SET revoked_at=now(), revoked_reason='MANUAL' WHERE id=@id AND tenant_id=@t AND revoked_at IS NULL", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound() : Ok();
    }

    private static async Task<List<Dictionary<string, object?>>> ReadAll(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Dictionary<string, object?>>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) { var d = new Dictionary<string, object?>(); for (int i = 0; i < rd.FieldCount; i++) d[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i); list.Add(d); }
        return list;
    }

    public record PdrReq(Guid PatientId, string RequestType, string Description);
    public record PdrResolveReq(string? Status, string? Resolution);
    public record IncidentReq(string IncidentType, string? Severity, string Description, string? Metadata);
}
