using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize]
public class AuditController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("logs")]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> Logs([FromQuery] string? entityType, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,user_id,entity_type,entity_id,action,occurred_at,ip_address FROM sigm.audit_logs WHERE tenant_id=@t" + (entityType!=null?" AND entity_type=@e":"") + " ORDER BY occurred_at DESC LIMIT @n";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(entityType!=null) cmd.Parameters.AddWithValue("@e",entityType); cmd.Parameters.AddWithValue("@n",Math.Clamp(take,1,500));
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),userId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),entityType=rd.IsDBNull(2)?null:rd.GetString(2),entityId=rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3),action=rd.IsDBNull(4)?null:rd.GetString(4),occurredAt=rd.GetFieldValue<DateTimeOffset>(5),ipAddress=rd.IsDBNull(6)?null:rd.GetValue(6)?.ToString()});
        return Ok(list);
    }

    [HttpGet("access")]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> Access([FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,user_id,patient_id,resource_type,resource_id,action,justification,occurred_at,ip_address FROM sigm.access_logs WHERE tenant_id=@t ORDER BY occurred_at DESC LIMIT @n", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@n",Math.Clamp(take,1,500));
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),userId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),patientId=rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2),resourceType=rd.IsDBNull(3)?null:rd.GetString(3),resourceId=rd.IsDBNull(4)?(Guid?)null:rd.GetGuid(4),action=rd.IsDBNull(5)?null:rd.GetString(5),justification=rd.IsDBNull(6)?null:rd.GetString(6),occurredAt=rd.GetFieldValue<DateTimeOffset>(7),ipAddress=rd.IsDBNull(8)?null:rd.GetValue(8)?.ToString()});
        return Ok(list);
    }
}
