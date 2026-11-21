using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/encounters")]
[Authorize]
public class EncountersController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record CreateEncounterRequest(Guid PatientId, Guid ProfessionalId, Guid SpecialtyId, Guid SiteId, Guid? AppointmentId, string EncounterType, string? ChiefComplaint);
    public record CreateNoteRequest(string NoteType, object Content);

    [HttpGet]
    [RequirePermission("clinical.read")]
    public async Task<IActionResult> List([FromQuery] Guid? patientId, [FromQuery] string? status, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,professional_id,specialty_id,site_id,appointment_id,encounter_type,encounter_date,status,chief_complaint,ended_at FROM sigm.encounters WHERE tenant_id=@t";
        if (patientId.HasValue) sql += " AND patient_id=@p";
        if (!string.IsNullOrWhiteSpace(status)) sql += " AND status=@s";
        sql += " ORDER BY encounter_date DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@t", tid);
        if (patientId.HasValue) cmd.Parameters.AddWithValue("@p", patientId.Value);
        if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("@s", status);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), patientId = rd.GetGuid(1), professionalId = rd.GetGuid(2), specialtyId = rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3), siteId = rd.IsDBNull(4)?(Guid?)null:rd.GetGuid(4), appointmentId = rd.IsDBNull(5)?(Guid?)null:rd.GetGuid(5), encounterType = rd.GetString(6), encounterDate = rd.GetFieldValue<DateTimeOffset>(7), status = rd.GetString(8), chiefComplaint = rd.IsDBNull(9)?null:rd.GetString(9), endedAt = rd.IsDBNull(10)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(10) });
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("clinical.create")]
    public async Task<IActionResult> Create([FromBody] CreateEncounterRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.encounters(id,tenant_id,patient_id,professional_id,specialty_id,site_id,appointment_id,encounter_type,encounter_date,started_at,status,chief_complaint,created_by)
                                VALUES(@id,@tid,@pid,@pro,@spec,@site,@apt,@type,now(),now(),'IN_PROGRESS',@cc,@u) RETURNING id", c);
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@pid", req.PatientId);
            cmd.Parameters.AddWithValue("@pro", req.ProfessionalId); cmd.Parameters.AddWithValue("@spec", req.SpecialtyId); cmd.Parameters.AddWithValue("@site", req.SiteId);
            cmd.Parameters.AddWithValue("@apt", (object?)req.AppointmentId ?? DBNull.Value); cmd.Parameters.AddWithValue("@type", req.EncounterType ?? "CONSULTATION");
            cmd.Parameters.AddWithValue("@cc", (object?)req.ChiefComplaint ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            await cmd.ExecuteScalarAsync(ct);
            return Ok(new { id, status = "IN_PROGRESS" });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { message = "Referencia inválida para el encuentro.", detail = ex.ConstraintName });
        }
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission("clinical.update")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"UPDATE sigm.encounters SET status='COMPLETED', ended_at=now(), updated_at=now()
            WHERE id=@id AND tenant_id=@t AND status IN ('IN_PROGRESS','OPEN')", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        return n == 0 ? NotFound(new { message = "Encuentro no encontrado o no está en progreso." }) : Ok(new { id, status = "COMPLETED" });
    }

    [HttpPost("{id:guid}/notes")]
    [RequirePermission("clinical.notes.create")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] CreateNoteRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid) return BadRequest(new { message = "Contexto de tenant/usuario no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        Guid patientId;
        await using (var cmd = new NpgsqlCommand("SELECT patient_id FROM sigm.encounters WHERE id=@id AND tenant_id=@t", c, tx))
        { cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid); var o = await cmd.ExecuteScalarAsync(ct); if (o is null) return NotFound(); patientId = (Guid)o; }
        var noteId = Guid.NewGuid();
        var contentJson = System.Text.Json.JsonSerializer.Serialize(req.Content ?? new { });
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.clinical_notes(id,tenant_id,encounter_id,patient_id,author_user_id,note_type,status,current_version)
                                VALUES(@id,@tid,@eid,@pid,@uid,@type,'DRAFT',1)", c, tx))
        {
            cmd.Parameters.AddWithValue("@id", noteId); cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@eid", id);
            cmd.Parameters.AddWithValue("@pid", patientId); cmd.Parameters.AddWithValue("@uid", uid); cmd.Parameters.AddWithValue("@type", req.NoteType);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.clinical_note_versions(id,tenant_id,note_id,version_number,content,author_user_id)
                                VALUES(gen_random_uuid(),@tid,@nid,1,@content::jsonb,@uid)", c, tx))
        {
            cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@nid", noteId);
            cmd.Parameters.AddWithValue("@content", contentJson); cmd.Parameters.AddWithValue("@uid", uid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new { id = noteId, status = "DRAFT", currentVersion = 1 });
    }

    [HttpGet("{id:guid}/notes")]
    [RequirePermission("clinical.notes.read")]
    public async Task<IActionResult> ListNotes(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT n.id,n.encounter_id,n.patient_id,n.author_user_id,n.note_type,n.status,n.current_version,n.signed_at,n.created_at,v.content
            FROM sigm.clinical_notes n
            LEFT JOIN sigm.clinical_note_versions v ON v.note_id=n.id AND v.tenant_id=n.tenant_id AND v.version_number=n.current_version
            WHERE n.tenant_id=@t AND n.encounter_id=@e ORDER BY n.note_type", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@e", id);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), encounterId = rd.GetGuid(1), patientId = rd.GetGuid(2), authorUserId = rd.GetGuid(3), noteType = rd.GetString(4), status = rd.GetString(5), currentVersion = rd.GetInt32(6), signedAt = rd.IsDBNull(7)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(7), createdAt = rd.GetFieldValue<DateTimeOffset>(8), content = ClinicalRecordExportController.FlattenJson(rd.IsDBNull(9) ? null : rd.GetString(9)) });
        return Ok(list);
    }
}
