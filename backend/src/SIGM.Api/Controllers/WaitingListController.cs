using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/waiting-list")]
[Authorize]
public class WaitingListController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record Req(Guid PatientId, Guid SpecialtyId, Guid? ProfessionalId, Guid? SiteId, short Priority, DateOnly? DesiredFrom, DateOnly? DesiredTo, string? Notes);

    [HttpGet]
    [RequirePermission("appointments.waiting_list.manage")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,specialty_id,professional_id,site_id,priority,desired_from,desired_to,status,notes,created_at FROM sigm.waiting_list WHERE tenant_id=@t" + (status!=null?" AND status=@s":"") + " ORDER BY priority, created_at";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t", tenant.TenantId!); if(status!=null) cmd.Parameters.AddWithValue("@s", status);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),patientId=rd.GetGuid(1),specialtyId=rd.GetGuid(2),professionalId=rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3),siteId=rd.IsDBNull(4)?(Guid?)null:rd.GetGuid(4),priority=rd.GetInt16(5),desiredFrom=rd.IsDBNull(6)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(6),desiredTo=rd.IsDBNull(7)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(7),status=rd.GetString(8),notes=rd.IsDBNull(9)?null:rd.GetString(9),createdAt=rd.GetFieldValue<DateTimeOffset>(10)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("appointments.waiting_list.manage")]
    public async Task<IActionResult> Create([FromBody] Req req, CancellationToken ct)
    {
        if (req.Priority is < 1 or > 5) return BadRequest(new { message = "priority debe estar entre 1 y 5." });
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.waiting_list(id,tenant_id,patient_id,specialty_id,professional_id,site_id,priority,desired_from,desired_to,status,notes,created_by)
            VALUES(gen_random_uuid(),@t,@p,@sp,@pro,@s,@pr,@df,@dt,'WAITING',@n,@u)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@sp",req.SpecialtyId);
        cmd.Parameters.AddWithValue("@pro",(object?)req.ProfessionalId??DBNull.Value); cmd.Parameters.AddWithValue("@s",(object?)req.SiteId??DBNull.Value);
        cmd.Parameters.AddWithValue("@pr",req.Priority==0?(short)3:req.Priority); cmd.Parameters.AddWithValue("@df",(object?)req.DesiredFrom??DBNull.Value); cmd.Parameters.AddWithValue("@dt",(object?)req.DesiredTo??DBNull.Value);
        cmd.Parameters.AddWithValue("@n",(object?)req.Notes??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpPost("{id:guid}/status")]
    [RequirePermission("appointments.waiting_list.manage")]
    public async Task<IActionResult> Status(Guid id, [FromBody] StatusReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.waiting_list SET status=@s, updated_at=now() WHERE id=@id AND tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@s",req.Status); cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@t",tid);
        return await cmd.ExecuteNonQueryAsync(ct)==0 ? NotFound() : NoContent();
    }
    public record StatusReq(string Status);
}
