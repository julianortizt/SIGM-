using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record CreateAppointmentRequest(Guid PatientId, Guid ProfessionalId, Guid SpecialtyId, Guid SiteId, Guid? MedicalOfficeId, Guid? AppointmentTypeId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason, string? Notes);

    [HttpGet]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> List([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? status, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,appointment_number,patient_id,professional_id,specialty_id,site_id,medical_office_id,starts_at,ends_at,status,reason,notes FROM sigm.appointments WHERE tenant_id=@t";
        if (from.HasValue) sql += " AND ends_at >= @from";
        if (to.HasValue) sql += " AND starts_at <= @to";
        if (!string.IsNullOrWhiteSpace(status)) sql += " AND status=@status";
        sql += " ORDER BY starts_at LIMIT 500";
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@t", tid);
        if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value);
        if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value);
        if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("@status", status);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), appointmentNumber = rd.GetString(1), patientId = rd.GetGuid(2), professionalId = rd.GetGuid(3), specialtyId = rd.GetGuid(4), siteId = rd.GetGuid(5), medicalOfficeId = rd.IsDBNull(6)?(Guid?)null:rd.GetGuid(6), startsAt = rd.GetFieldValue<DateTimeOffset>(7), endsAt = rd.GetFieldValue<DateTimeOffset>(8), status = rd.GetString(9), reason = rd.IsDBNull(10)?null:rd.GetString(10), notes = rd.IsDBNull(11)?null:rd.GetString(11) });
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("appointments.create")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.appointments(id,tenant_id,patient_id,professional_id,specialty_id,site_id,medical_office_id,appointment_type_id,starts_at,ends_at,status,reason,notes,booked_by)
                                VALUES(gen_random_uuid(),@tid,@pid,@pro,@spec,@site,@office,@type,@start,@end,'PENDING',@reason,@notes,@u)
                                RETURNING id, appointment_number", c);
            cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@pid", req.PatientId);
            cmd.Parameters.AddWithValue("@pro", req.ProfessionalId); cmd.Parameters.AddWithValue("@spec", req.SpecialtyId);
            cmd.Parameters.AddWithValue("@site", req.SiteId); cmd.Parameters.AddWithValue("@office", (object?)req.MedicalOfficeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@type", (object?)req.AppointmentTypeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@start", req.StartsAt.ToUniversalTime()); cmd.Parameters.AddWithValue("@end", req.EndsAt.ToUniversalTime());
            cmd.Parameters.AddWithValue("@reason", (object?)req.Reason ?? DBNull.Value); cmd.Parameters.AddWithValue("@notes", (object?)req.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            Guid id = Guid.Empty; string? number = null;
            if (await rd.ReadAsync(ct)) { id = rd.GetGuid(0); number = rd.GetString(1); }
            return Ok(new { id, appointmentNumber = number, status = "PENDING" });
        }
        catch (PostgresException ex) when (ex.SqlState == "23P01" || ex.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return Conflict(new { message = "Conflicto de horario: el profesional o consultorio ya tiene una cita en ese rango." });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { message = "Referencia inválida (paciente/profesional/especialidad/sede/tipo).", detail = ex.ConstraintName });
        }
    }

    [HttpPost("{id:guid}/status")]
    [RequirePermission("appointments.update")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] StatusRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        string? from = null;
        await using (var q = new NpgsqlCommand("SELECT status FROM sigm.appointments WHERE id=@id AND tenant_id=@tid", c, tx))
        { q.Parameters.AddWithValue("@id", id); q.Parameters.AddWithValue("@tid", tid); from = (string?)await q.ExecuteScalarAsync(ct); }
        if (from is null) { await tx.RollbackAsync(ct); return NotFound(); }
        // Transiciones válidas según el ciclo de vida del documento (Estados controlados: Pendiente, Confirmada, Reprogramada, Cancelada, Espera, En atención, Atendida, No asistió)
        var allowed = from switch
        {
            "PENDING" => new[] { "CONFIRMED", "RESCHEDULED", "CANCELLED", "WAITING", "NO_SHOW" },
            "CONFIRMED" => new[] { "IN_PROGRESS", "RESCHEDULED", "WAITING", "CANCELLED", "NO_SHOW" },
            "RESCHEDULED" => new[] { "CONFIRMED", "CANCELLED", "NO_SHOW" },
            "WAITING" => new[] { "IN_PROGRESS", "CANCELLED", "NO_SHOW" },
            "IN_PROGRESS" => new[] { "ATTENDED", "NO_SHOW" },
            _ => Array.Empty<string>(), // CANCELLED / ATTENDED / NO_SHOW son terminales
        };
        if (!allowed.Contains(req.Status))
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { message = $"Transición de estado inválida: {from} → {req.Status}." });
        }
        var extra = req.Status == "CANCELLED" ? ", cancelled_at=now(), cancellation_reason=@reason" : (req.Status == "CONFIRMED" ? ", confirmed_at=now()" : "");
        await using (var cmd = new NpgsqlCommand($"UPDATE sigm.appointments SET status=@s, updated_at=now(){extra} WHERE id=@id AND tenant_id=@tid", c, tx))
        {
            cmd.Parameters.AddWithValue("@s", req.Status); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid);
            if (req.Status == "CANCELLED") cmd.Parameters.AddWithValue("@reason", (object?)req.Reason ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        // El trigger sigm.fn_appointment_status_history() ya registra cada transición
        // (incluido changed_by y el motivo de cancelación). Insertarlo aquí duplicaba
        // el historial: cada cambio de estado aparecía dos veces.
        await tx.CommitAsync(ct);
        return Ok(new { id, status = req.Status, from });
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("appointments.cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest? req, CancellationToken ct)
        => await UpdateStatus(id, new StatusRequest("CANCELLED", req?.Reason), ct);

    public record StatusRequest(string Status, string? Reason = null);
    public record CancelRequest(string? Reason);
}
