using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/interoperability")]
[Authorize]
public class InteroperabilityController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("events")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,event_type,entity_type,entity_id,status,occurred_at,detail FROM sigm.interoperability_events WHERE tenant_id=@t ORDER BY occurred_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),eventType=rd.GetString(1),entityType=rd.IsDBNull(2)?null:rd.GetString(2),entityId=rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3),status=rd.GetString(4),occurredAt=rd.GetFieldValue<DateTimeOffset>(5),detail=rd.IsDBNull(6)?null:rd.GetValue(6).ToString()});
        return Ok(list);
    }

    [HttpGet("transmissions")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Transmissions(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,message_id,destination,http_status,attempted_at,success,error_message FROM sigm.transmission_logs WHERE tenant_id=@t ORDER BY attempted_at DESC NULLS LAST, id DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),messageId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),destination=rd.IsDBNull(2)?null:rd.GetString(2),httpStatus=rd.IsDBNull(3)?(int?)null:rd.GetInt32(3),attemptedAt=rd.IsDBNull(4)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(4),success=rd.IsDBNull(5)?(bool?)null:rd.GetBoolean(5),errorMessage=rd.IsDBNull(6)?null:rd.GetString(6)});
        return Ok(list);
    }

    [HttpGet("fhir-mappings")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> FhirMappings(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT id,resource_type,local_table,local_field,fhir_path,mapping,is_active
            FROM sigm.fhir_mappings WHERE tenant_id IS NULL OR tenant_id=@t ORDER BY resource_type LIMIT 500", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),resourceType=rd.GetString(1),localTable=rd.IsDBNull(2)?null:rd.GetString(2),localField=rd.IsDBNull(3)?null:rd.GetString(3),fhirPath=rd.IsDBNull(4)?null:rd.GetString(4),mapping=rd.IsDBNull(5)?null:rd.GetString(5),isActive=rd.IsDBNull(6)?(bool?)null:rd.GetBoolean(6)});
        return Ok(list);
    }

    [HttpGet("rda")]
    [RequirePermission("reports.read")]
    public async Task<IActionResult> Rda([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,rda_uuid,status,generated_at,transmitted_at FROM sigm.rda_documents WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY generated_at DESC NULLS LAST LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(patientId.HasValue) cmd.Parameters.AddWithValue("@p",patientId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),patientId=rd.GetGuid(1),rdaUuid=rd.IsDBNull(2)?null:rd.GetString(2),status=rd.GetString(3),generatedAt=rd.IsDBNull(4)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(4),transmittedAt=rd.IsDBNull(5)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(5)});
        return Ok(list);
    }
}
