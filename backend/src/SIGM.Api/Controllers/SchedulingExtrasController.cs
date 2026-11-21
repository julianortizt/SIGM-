using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Authorize]
public class SchedulingExtrasController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("api/v1/appointments/{appointmentId:guid}/status-history")]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> StatusHistory(Guid appointmentId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,from_status,to_status,changed_by,reason,changed_at FROM sigm.appointment_status_history WHERE tenant_id=@t AND appointment_id=@a ORDER BY changed_at DESC", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@a", appointmentId);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), fromStatus = rd.IsDBNull(1)?null:rd.GetString(1), toStatus = rd.GetString(2), changedBy = rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3), reason = rd.IsDBNull(4)?null:rd.GetString(4), changedAt = rd.GetFieldValue<DateTimeOffset>(5) });
        return Ok(list);
    }

    [HttpGet("api/v1/appointments/{appointmentId:guid}/resources")]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> AppointmentResources(Guid appointmentId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,resource_id,starts_at,ends_at,quantity FROM sigm.appointment_resources WHERE tenant_id=@t AND appointment_id=@a", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@a", appointmentId);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), resourceId = rd.GetGuid(1), startsAt = rd.GetFieldValue<DateTimeOffset>(2), endsAt = rd.GetFieldValue<DateTimeOffset>(3), quantity = rd.GetInt32(4) });
        return Ok(list);
    }

    [HttpPost("api/v1/appointments/{appointmentId:guid}/resources")]
    [RequirePermission("appointments.manage")]
    public async Task<IActionResult> AddAppointmentResource(Guid appointmentId, [FromBody] ApptResourceReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.appointment_resources(id,tenant_id,appointment_id,resource_id,starts_at,ends_at,quantity)
            VALUES(gen_random_uuid(),@t,@a,@r,@s,@e,@q)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@a", appointmentId); cmd.Parameters.AddWithValue("@r", req.ResourceId);
        cmd.Parameters.AddWithValue("@s", req.StartsAt.ToUniversalTime()); cmd.Parameters.AddWithValue("@e", req.EndsAt.ToUniversalTime()); cmd.Parameters.AddWithValue("@q", req.Quantity <= 0 ? 1 : req.Quantity);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("api/v1/document-permissions")]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> DocumentPermissions([FromQuery] Guid documentId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,document_id,user_id,role_id,permission_level,granted_by,granted_at,expires_at FROM sigm.document_permissions WHERE tenant_id=@t AND document_id=@d", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", documentId);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), documentId = rd.GetGuid(1), userId = rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2), roleId = rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3), permissionLevel = rd.GetString(4), grantedBy = rd.IsDBNull(5)?(Guid?)null:rd.GetGuid(5), grantedAt = rd.GetFieldValue<DateTimeOffset>(6), expiresAt = rd.IsDBNull(7)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(7) });
        return Ok(list);
    }

    [HttpPost("api/v1/document-permissions")]
    [RequirePermission("documents.update")]
    public async Task<IActionResult> GrantDocumentPermission([FromBody] DocPermReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.document_permissions(id,tenant_id,document_id,user_id,role_id,permission_level,granted_by,expires_at)
            VALUES(gen_random_uuid(),@t,@d,@u,@r,@lvl,@g,@exp)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", req.DocumentId); cmd.Parameters.AddWithValue("@u", (object?)req.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@r", (object?)req.RoleId ?? DBNull.Value); cmd.Parameters.AddWithValue("@lvl", req.PermissionLevel ?? "READ");
        cmd.Parameters.AddWithValue("@g", (object?)tenant.UserId ?? DBNull.Value); cmd.Parameters.AddWithValue("@exp", (object?)req.ExpiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("api/v1/documents/{documentId:guid}/signatures")]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> Signatures(Guid documentId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,signer_user_id,signature_type,signature_hash,certificate_serial,signed_at,metadata FROM sigm.document_signatures WHERE tenant_id=@t AND document_id=@d ORDER BY signed_at DESC", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", documentId);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), signerUserId = rd.GetGuid(1), signatureType = rd.GetString(2), signatureHash = rd.GetString(3), certificateSerial = rd.IsDBNull(4)?null:rd.GetString(4), signedAt = rd.GetFieldValue<DateTimeOffset>(5), metadata = rd.IsDBNull(6)?null:rd.GetString(6) });
        return Ok(list);
    }

    public record ApptResourceReq(Guid ResourceId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Quantity);
    public record DocPermReq(Guid DocumentId, Guid? UserId, Guid? RoleId, string? PermissionLevel, DateTimeOffset? ExpiresAt);
}
