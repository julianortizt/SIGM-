using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/notes")]
[Authorize]
public class ClinicalNotesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record AmendRequest(string Reason, object Content);

    [HttpPost("{id:guid}/sign")]
    [RequirePermission("clinical.notes.sign")]
    public async Task<IActionResult> Sign(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid) return BadRequest(new { message = "Contexto no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"UPDATE sigm.clinical_notes SET status='SIGNED', signed_at=now(), signed_by=@uid, updated_at=now()
                            WHERE id=@id AND tenant_id=@tid AND status='DRAFT'", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@uid", uid);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        if (n == 0) return BadRequest(new { message = "La nota no existe, no está en borrador o no puede firmarse." });
        return Ok(new { id, status = "SIGNED" });
    }

    [HttpPost("{id:guid}/amendments")]
    [RequirePermission("clinical.amend")]
    public async Task<IActionResult> Amend(Guid id, [FromBody] AmendRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid) return BadRequest(new { message = "Contexto no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        string? status;
        await using (var cmd = new NpgsqlCommand("SELECT status FROM sigm.clinical_notes WHERE id=@id AND tenant_id=@tid", c, tx))
        { cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid); status = (string?)await cmd.ExecuteScalarAsync(ct); }
        if (status is null) return NotFound();
        if (status != "SIGNED" && status != "AMENDED") return BadRequest(new { message = "Solo se pueden adicionar adendas a notas firmadas." });
        int next;
        await using (var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(amendment_number),0)+1 FROM sigm.clinical_amendments WHERE tenant_id=@tid AND note_id=@nid", c, tx))
        { cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@nid", id); next = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)); }
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.clinical_amendments(id,tenant_id,note_id,amendment_number,reason,content,author_user_id,signed_at)
                                VALUES(gen_random_uuid(),@tid,@nid,@n,@reason,@content::jsonb,@uid,now())", c, tx))
        {
            cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@nid", id); cmd.Parameters.AddWithValue("@n", next);
            cmd.Parameters.AddWithValue("@reason", req.Reason); cmd.Parameters.AddWithValue("@content", System.Text.Json.JsonSerializer.Serialize(req.Content ?? new { }));
            cmd.Parameters.AddWithValue("@uid", uid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("UPDATE sigm.clinical_notes SET status='AMENDED', updated_at=now() WHERE id=@nid AND tenant_id=@tid", c, tx))
        { cmd.Parameters.AddWithValue("@nid", id); cmd.Parameters.AddWithValue("@tid", tid); await cmd.ExecuteNonQueryAsync(ct); }
        await tx.CommitAsync(ct);
        return Ok(new { noteId = id, amendmentNumber = next, status = "AMENDED" });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("clinical.notes.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        object? note = null;
        await using (var cmd = new NpgsqlCommand("SELECT id,encounter_id,patient_id,author_user_id,note_type,status,current_version,signed_at,signed_by,created_at FROM sigm.clinical_notes WHERE id=@id AND tenant_id=@t", c))
        {
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct)) note = new { id = rd.GetGuid(0), encounterId = rd.GetGuid(1), patientId = rd.GetGuid(2), authorUserId = rd.GetGuid(3), noteType = rd.GetString(4), status = rd.GetString(5), currentVersion = rd.GetInt32(6), signedAt = rd.IsDBNull(7)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(7), signedBy = rd.IsDBNull(8)?(Guid?)null:rd.GetGuid(8), createdAt = rd.GetFieldValue<DateTimeOffset>(9) };
        }
        if (note is null) return NotFound();
        var versions = new List<object>();
        await using (var cmd = new NpgsqlCommand("SELECT id,version_number,content,author_user_id,created_at FROM sigm.clinical_note_versions WHERE tenant_id=@t AND note_id=@id ORDER BY version_number", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@id", id);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) versions.Add(new { id = rd.GetGuid(0), versionNumber = rd.GetInt32(1), content = rd.IsDBNull(2)?null:rd.GetString(2), authorUserId = rd.GetGuid(3), createdAt = rd.GetFieldValue<DateTimeOffset>(4) });
        }
        var amendments = new List<object>();
        await using (var cmd = new NpgsqlCommand("SELECT id,amendment_number,reason,content,author_user_id,signed_at,created_at FROM sigm.clinical_amendments WHERE tenant_id=@t AND note_id=@id ORDER BY amendment_number", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@id", id);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) amendments.Add(new { id = rd.GetGuid(0), amendmentNumber = rd.GetInt32(1), reason = rd.GetString(2), content = rd.IsDBNull(3)?null:rd.GetString(3), authorUserId = rd.GetGuid(4), signedAt = rd.IsDBNull(5)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(5), createdAt = rd.GetFieldValue<DateTimeOffset>(6) });
        }
        return Ok(new { note, versions, amendments });
    }
}
