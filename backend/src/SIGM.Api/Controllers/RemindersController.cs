using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/reminders")]
[Authorize]
public class RemindersController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record Req(Guid AppointmentId, string Channel, DateTimeOffset ScheduledAt, string? Recipient);

    private static readonly string[] Channels = ["EMAIL","SMS","WHATSAPP","PUSH","PHONE"];

    [HttpGet]
    [RequirePermission("appointments.read")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,appointment_id,channel,scheduled_at,sent_at,status,recipient,error_message FROM sigm.reminders WHERE tenant_id=@t" + (status!=null?" AND status=@s":"") + " ORDER BY scheduled_at DESC LIMIT 300";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t", tenant.TenantId!); if(status!=null) cmd.Parameters.AddWithValue("@s", status);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),appointmentId=rd.GetGuid(1),channel=rd.GetString(2),scheduledAt=rd.GetFieldValue<DateTimeOffset>(3),sentAt=rd.IsDBNull(4)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(4),status=rd.GetString(5),recipient=rd.IsDBNull(6)?null:rd.GetString(6),errorMessage=rd.IsDBNull(7)?null:rd.GetString(7)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("appointments.manage")]
    public async Task<IActionResult> Create([FromBody] Req req, CancellationToken ct)
    {
        if (!Channels.Contains(req.Channel)) return BadRequest(new { message = $"channel debe ser: {string.Join(", ", Channels)}." });
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.reminders(id,tenant_id,appointment_id,channel,scheduled_at,status,recipient)
            VALUES(gen_random_uuid(),@t,@a,@ch,@at,'PENDING',@r)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@a",req.AppointmentId); cmd.Parameters.AddWithValue("@ch",req.Channel);
        cmd.Parameters.AddWithValue("@at",req.ScheduledAt.ToUniversalTime()); cmd.Parameters.AddWithValue("@r",(object?)req.Recipient??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("appointments.manage")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.reminders SET status='CANCELLED', updated_at=now() WHERE id=@id AND tenant_id=@t AND status='PENDING'", c);
        cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@t",tid);
        return await cmd.ExecuteNonQueryAsync(ct)==0 ? NotFound() : NoContent();
    }
}
