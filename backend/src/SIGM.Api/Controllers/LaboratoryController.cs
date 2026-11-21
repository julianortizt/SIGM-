using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/laboratory")]
[Authorize]
public class LaboratoryController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record OrderReq(Guid PatientId, Guid? EncounterId, string? Notes);
    public record ResultReq(Guid LaboratoryOrderItemId, string? ResultValue, string? ResultUnit, string? ReferenceRange, string? Interpretation, bool IsAbnormal);

    [HttpGet("orders")]
    [RequirePermission("laboratory.read")]
    public async Task<IActionResult> Orders([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,encounter_id,ordered_at,status,notes FROM sigm.laboratory_orders WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY ordered_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(patientId.HasValue) cmd.Parameters.AddWithValue("@p",patientId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),patientId=rd.GetGuid(1),encounterId=rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2),orderedAt=rd.GetFieldValue<DateTimeOffset>(3),status=rd.GetString(4),notes=rd.IsDBNull(5)?null:rd.GetString(5)});
        return Ok(list);
    }

    [HttpPost("orders")]
    [RequirePermission("laboratory.manage")]
    public async Task<IActionResult> CreateOrder([FromBody] OrderReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.laboratory_orders(id,tenant_id,patient_id,encounter_id,status,notes,ordered_by) VALUES(@id,@t,@p,@e,'REQUESTED',@n,@u)", c);
        cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@e",(object?)req.EncounterId??DBNull.Value);
        cmd.Parameters.AddWithValue("@n",(object?)req.Notes??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok(new{id});
    }

    [HttpGet("tests")]
    [RequirePermission("laboratory.read")]
    public async Task<IActionResult> Tests([FromQuery] string? q, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct);
        var sql = string.IsNullOrWhiteSpace(q)
            ? "SELECT id,code,name,sample_type,unit,is_active FROM sigm.laboratory_tests ORDER BY name LIMIT 100"
            : "SELECT id,code,name,sample_type,unit,is_active FROM sigm.laboratory_tests WHERE (code ILIKE @p OR name ILIKE @p) ORDER BY name LIMIT 100";
        await using var cmd = new NpgsqlCommand(sql,c); if(!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@p",$"%{q.Trim()}%");
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),code=rd.IsDBNull(1)?null:rd.GetString(1),name=rd.GetString(2),sampleType=rd.IsDBNull(3)?null:rd.GetString(3),unit=rd.IsDBNull(4)?null:rd.GetString(4),isActive=rd.GetBoolean(5)});
        return Ok(list);
    }

    [HttpPost("results")]
    [RequirePermission("laboratory.results")]
    public async Task<IActionResult> AddResult([FromBody] ResultReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        if (string.IsNullOrWhiteSpace(req.ResultValue))
            return BadRequest(new { message = "El valor del resultado es obligatorio." });

        // El INSERT anterior usaba columnas que no existen (laboratory_order_item_id,
        // result_unit, interpretation, is_abnormal, reported_at...): fallaba siempre con 42703.
        var flag = (req.Interpretation ?? "").Trim().ToUpperInvariant();
        if (flag is not ("NORMAL" or "HIGH" or "LOW" or "CRITICAL" or "ABNORMAL"))
            flag = req.IsAbnormal ? "ABNORMAL" : "NORMAL";

        await using var c = await db.OpenAsync(ct);
        await using (var owns = new NpgsqlCommand(@"SELECT 1 FROM sigm.laboratory_order_items
            WHERE tenant_id=@t AND id=@i LIMIT 1", c))
        {
            owns.Parameters.AddWithValue("@t", tid); owns.Parameters.AddWithValue("@i", req.LaboratoryOrderItemId);
            if (await owns.ExecuteScalarAsync(ct) is null)
                return NotFound(new { message = "El ítem de la orden no existe en esta organización." });
        }
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.laboratory_results(id,tenant_id,order_item_id,result_value,unit,reference_range,result_flag,resulted_at)
            VALUES(gen_random_uuid(),@t,@i,@v,@u,@r,@flag,now())", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@i",req.LaboratoryOrderItemId); cmd.Parameters.AddWithValue("@v",req.ResultValue.Trim());
        cmd.Parameters.AddWithValue("@u",(object?)req.ResultUnit??DBNull.Value); cmd.Parameters.AddWithValue("@r",(object?)req.ReferenceRange??DBNull.Value);
        cmd.Parameters.AddWithValue("@flag",flag);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
}
