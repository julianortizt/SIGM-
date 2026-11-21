using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/schedules")]
[Authorize]
public class SchedulesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record CreateScheduleRequest(Guid ProfessionalId, Guid SiteId, Guid? MedicalOfficeId, string Name, string? Timezone);
    public record AvailabilityRuleRequest(Guid ScheduleId, short Weekday, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes, Guid? SpecialtyId);
    public record ExceptionRequest(Guid? ScheduleId, Guid ProfessionalId, DateOnly ExceptionDate, TimeOnly? StartTime, TimeOnly? EndTime, bool IsAllDay, string ExceptionType, string? Reason);
    public record AppointmentTypeRequest(string Code, string Name, int DurationMinutes, Guid? SpecialtyId);

    [HttpGet]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,professional_id,site_id,medical_office_id,name,timezone,is_active FROM sigm.schedules WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id=rd.GetGuid(0), professionalId=rd.GetGuid(1), siteId=rd.GetGuid(2), medicalOfficeId=rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3), name=rd.GetString(4), timezone=rd.GetString(5), isActive=rd.GetBoolean(6) });
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> Create([FromBody] CreateScheduleRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.schedules(id,tenant_id,professional_id,site_id,medical_office_id,name,timezone) VALUES(@id,@t,@p,@s,@o,@n,@tz)", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", req.ProfessionalId); cmd.Parameters.AddWithValue("@s", req.SiteId);
        cmd.Parameters.AddWithValue("@o", (object?)req.MedicalOfficeId ?? DBNull.Value); cmd.Parameters.AddWithValue("@n", req.Name); cmd.Parameters.AddWithValue("@tz", (object?)req.Timezone ?? "America/Bogota");
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { id });
    }

    [HttpGet("availability-rules")]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> Rules([FromQuery] Guid? scheduleId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,schedule_id,weekday,start_time,end_time,slot_duration_minutes,specialty_id,is_active FROM sigm.availability_rules WHERE tenant_id=@t" + (scheduleId.HasValue?" AND schedule_id=@s":"") + " ORDER BY weekday,start_time";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid); if(scheduleId.HasValue) cmd.Parameters.AddWithValue("@s", scheduleId.Value);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id=rd.GetGuid(0), scheduleId=rd.GetGuid(1), weekday=rd.GetInt16(2), startTime=rd.GetTimeSpan(3).ToString(@"hh\:mm"), endTime=rd.GetTimeSpan(4).ToString(@"hh\:mm"), slotDurationMinutes=rd.GetInt32(5), specialtyId=rd.IsDBNull(6)?(Guid?)null:rd.GetGuid(6), isActive=rd.GetBoolean(7) });
        return Ok(list);
    }

    [HttpPost("availability-rules")]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> AddRule([FromBody] AvailabilityRuleRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.availability_rules(id,tenant_id,schedule_id,weekday,start_time,end_time,slot_duration_minutes,specialty_id,is_active)
            VALUES(gen_random_uuid(),@t,@s,@w,@st,@et,@d,@sp,true)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@s", req.ScheduleId); cmd.Parameters.AddWithValue("@w", req.Weekday);
        cmd.Parameters.AddWithValue("@st", req.StartTime.ToTimeSpan()); cmd.Parameters.AddWithValue("@et", req.EndTime.ToTimeSpan());
        cmd.Parameters.AddWithValue("@d", req.SlotDurationMinutes); cmd.Parameters.AddWithValue("@sp", (object?)req.SpecialtyId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok();
    }

    [HttpGet("exceptions")]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> Exceptions([FromQuery] Guid? professionalId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,schedule_id,professional_id,exception_date,start_time,end_time,is_all_day,exception_type,reason FROM sigm.schedule_exceptions WHERE tenant_id=@t" + (professionalId.HasValue?" AND professional_id=@p":"") + " ORDER BY exception_date DESC";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid); if(professionalId.HasValue) cmd.Parameters.AddWithValue("@p", professionalId.Value);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id=rd.GetGuid(0), scheduleId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1), professionalId=rd.GetGuid(2), exceptionDate=rd.GetFieldValue<DateOnly>(3), startTime=rd.IsDBNull(4)?null:rd.GetTimeSpan(4).ToString(@"hh\:mm"), endTime=rd.IsDBNull(5)?null:rd.GetTimeSpan(5).ToString(@"hh\:mm"), isAllDay=rd.GetBoolean(6), exceptionType=rd.GetString(7), reason=rd.IsDBNull(8)?null:rd.GetString(8) });
        return Ok(list);
    }

    [HttpPost("exceptions")]
    [RequirePermission("appointments.availability.manage")]
    public async Task<IActionResult> AddException([FromBody] ExceptionRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.schedule_exceptions(id,tenant_id,schedule_id,professional_id,exception_date,start_time,end_time,is_all_day,exception_type,reason)
            VALUES(gen_random_uuid(),@t,@s,@p,@d,@st,@et,@all,@type,@r)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@s", (object?)req.ScheduleId ?? DBNull.Value); cmd.Parameters.AddWithValue("@p", req.ProfessionalId);
        cmd.Parameters.AddWithValue("@d", req.ExceptionDate); cmd.Parameters.AddWithValue("@st", (object?)req.StartTime?.ToTimeSpan() ?? DBNull.Value); cmd.Parameters.AddWithValue("@et", (object?)req.EndTime?.ToTimeSpan() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@all", req.IsAllDay); cmd.Parameters.AddWithValue("@type", req.ExceptionType ?? "BLOCK"); cmd.Parameters.AddWithValue("@r", (object?)req.Reason ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok();
    }

    [HttpGet("/api/v1/appointment-types")]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> Types(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,code,name,duration_minutes,specialty_id,is_active FROM sigm.appointment_types WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id=rd.GetGuid(0), code=rd.GetString(1), name=rd.GetString(2), durationMinutes=rd.GetInt32(3), specialtyId=rd.IsDBNull(4)?(Guid?)null:rd.GetGuid(4), isActive=rd.GetBoolean(5) });
        return Ok(list);
    }

    [HttpPost("/api/v1/appointment-types")]
    [RequirePermission("appointments.manage")]
    public async Task<IActionResult> CreateType([FromBody] AppointmentTypeRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.appointment_types(id,tenant_id,code,name,duration_minutes,specialty_id) VALUES(gen_random_uuid(),@t,@c,@n,@d,@s)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@c", req.Code); cmd.Parameters.AddWithValue("@n", req.Name); cmd.Parameters.AddWithValue("@d", req.DurationMinutes); cmd.Parameters.AddWithValue("@s", (object?)req.SpecialtyId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok();
    }
}
