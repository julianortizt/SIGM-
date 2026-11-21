using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/prescriptions")]
[Authorize]
public class PrescriptionsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record ItemReq(Guid? MedicationCatalogId, string MedicationName, string? Dose, string? Frequency, string? Route, int? DurationDays, string? Quantity, string? Instructions);
    public record CreateReq(Guid EncounterId, Guid PatientId, string? Notes, ItemReq[] Items);

    [HttpGet]
    [RequirePermission("clinical.prescriptions.manage")]
    public async Task<IActionResult> List([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,encounter_id,patient_id,prescription_number,status,issued_at,notes FROM sigm.prescriptions WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY issued_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(patientId.HasValue) cmd.Parameters.AddWithValue("@p",patientId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),encounterId=rd.GetGuid(1),patientId=rd.GetGuid(2),prescriptionNumber=rd.GetString(3),status=rd.GetString(4),issuedAt=rd.GetFieldValue<DateTimeOffset>(5),notes=rd.IsDBNull(6)?null:rd.GetString(6)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("clinical.prescriptions.manage")]
    public async Task<IActionResult> Create([FromBody] CreateReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        var id = Guid.NewGuid(); string? number = null;
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.prescriptions(id,tenant_id,encounter_id,patient_id,status,issued_by,notes)
            VALUES(@id,@t,@e,@p,'ISSUED',@u,@n) RETURNING prescription_number", c, tx))
        { cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@e",req.EncounterId); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value); cmd.Parameters.AddWithValue("@n",(object?)req.Notes??DBNull.Value); number=(string?)await cmd.ExecuteScalarAsync(ct); }
        foreach (var it in req.Items ?? [])
        {
            await using var ic = new NpgsqlCommand(@"INSERT INTO sigm.prescription_items(id,tenant_id,prescription_id,medication_catalog_id,medication_name,dose,frequency,route,duration_days,quantity,instructions)
                VALUES(gen_random_uuid(),@t,@pid,@mid,@name,@dose,@freq,@route,@dur,@q,@ins)", c, tx);
            ic.Parameters.AddWithValue("@t",tid); ic.Parameters.AddWithValue("@pid",id); ic.Parameters.AddWithValue("@mid",(object?)it.MedicationCatalogId??DBNull.Value);
            ic.Parameters.AddWithValue("@name",it.MedicationName); ic.Parameters.AddWithValue("@dose",(object?)it.Dose??DBNull.Value); ic.Parameters.AddWithValue("@freq",(object?)it.Frequency??DBNull.Value);
            ic.Parameters.AddWithValue("@route",(object?)it.Route??DBNull.Value); ic.Parameters.AddWithValue("@dur",(object?)it.DurationDays??DBNull.Value); ic.Parameters.AddWithValue("@q",(object?)it.Quantity??DBNull.Value); ic.Parameters.AddWithValue("@ins",(object?)it.Instructions??DBNull.Value);
            await ic.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct); return Ok(new{id, prescriptionNumber=number});
    }

    [HttpGet("{id:guid}/items")]
    [RequirePermission("clinical.prescriptions.manage")]
    public async Task<IActionResult> Items(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,medication_name,dose,frequency,route,duration_days,quantity,instructions FROM sigm.prescription_items WHERE tenant_id=@t AND prescription_id=@p", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",id);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),medicationName=rd.GetString(1),dose=rd.IsDBNull(2)?null:rd.GetString(2),frequency=rd.IsDBNull(3)?null:rd.GetString(3),route=rd.IsDBNull(4)?null:rd.GetString(4),durationDays=rd.IsDBNull(5)?null:(int?)rd.GetInt32(5),quantity=rd.IsDBNull(6)?null:(string?)rd.GetString(6),instructions=rd.IsDBNull(7)?null:rd.GetString(7)});
        return Ok(list);
    }
}
