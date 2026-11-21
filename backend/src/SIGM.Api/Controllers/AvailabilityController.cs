using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
[Authorize]
public class AvailabilityController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> Slots([FromQuery] Guid professionalId, [FromQuery] DateOnly date, [FromQuery] Guid? scheduleId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        var weekday = (short)((int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek);
        await using var c = await db.OpenAsync(ct);

        var rules = new List<(TimeSpan start, TimeSpan end, int slot)>();
        var rsql = "SELECT start_time,end_time,slot_duration_minutes FROM sigm.availability_rules r JOIN sigm.schedules s ON s.id=r.schedule_id AND s.tenant_id=r.tenant_id WHERE r.tenant_id=@t AND s.professional_id=@p AND r.weekday=@w AND r.is_active AND s.is_active" + (scheduleId.HasValue?" AND r.schedule_id=@sid":"");
        await using (var cmd = new NpgsqlCommand(rsql, c))
        {
            cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",professionalId); cmd.Parameters.AddWithValue("@w",weekday);
            if(scheduleId.HasValue) cmd.Parameters.AddWithValue("@sid",scheduleId.Value);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) rules.Add((rd.GetTimeSpan(0), rd.GetTimeSpan(1), rd.GetInt32(2)));
        }

        var busy = new List<(DateTimeOffset s, DateTimeOffset e)>();
        await using (var cmd = new NpgsqlCommand("SELECT starts_at, ends_at FROM sigm.appointments WHERE tenant_id=@t AND professional_id=@p AND status IN ('PENDING','CONFIRMED','RESCHEDULED','WAITING','IN_PROGRESS') AND starts_at::date <= @d AND ends_at::date >= @d", c))
        {
            cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",professionalId); cmd.Parameters.AddWithValue("@d",date);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) busy.Add((rd.GetFieldValue<DateTimeOffset>(0), rd.GetFieldValue<DateTimeOffset>(1)));
        }

        var blockedAllDay = false;
        await using (var cmd = new NpgsqlCommand("SELECT 1 FROM sigm.schedule_exceptions WHERE tenant_id=@t AND professional_id=@p AND exception_date=@d AND exception_type='BLOCK' AND is_all_day LIMIT 1", c))
        { cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",professionalId); cmd.Parameters.AddWithValue("@d",date); blockedAllDay = (await cmd.ExecuteScalarAsync(ct)) != null; }

        var slots = new List<object>();
        if (!blockedAllDay)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");
            foreach (var r in rules)
            {
                var cur = r.start; var step = TimeSpan.FromMinutes(Math.Max(5, r.slot));
                while (cur + step <= r.end)
                {
                    var startLocal = date.ToDateTime(TimeOnly.FromTimeSpan(cur));
                    var endLocal = date.ToDateTime(TimeOnly.FromTimeSpan(cur + step));
                    var start = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    var end = new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal));
                    var overlaps = busy.Any(b => start < b.e && end > b.s);
                    slots.Add(new { startsAt = start, endsAt = end, available = !overlaps });
                    cur += step;
                }
            }
        }
        return Ok(new { professionalId, date, weekday, slots = slots.OrderBy(s => ((dynamic)s).startsAt) });
    }
}
