using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/clinical")]
[Authorize]
public class ClinicalDataController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record VitalsRequest(Guid EncounterId, Guid PatientId, decimal? TemperatureC, int? HeartRate, int? RespiratoryRate, int? SystolicBp, int? DiastolicBp, decimal? OxygenSaturation, decimal? WeightKg, decimal? HeightCm, decimal? GlucoseMgDl, short? PainScale, string? Notes);
    public record DiagnosisRequest(Guid EncounterId, Guid PatientId, Guid DiagnosisCatalogId, string DiagnosisType, string Certainty, string? Notes);
    public record AllergyRequest(Guid PatientId, string Allergen, string AllergenType, string? Reaction, string Severity);
    public record HistoryRequest(Guid PatientId, Guid? EncounterId, string HistoryType, string Description, DateOnly? OnsetDate, string? Status);

    [HttpGet("patients/{patientId:guid}/summary")]
    [RequirePermission("clinical.read")]
    public async Task<IActionResult> Summary(Guid patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        async Task<List<Dictionary<string,object?>>> Q(string sql, params (string,object)[] ps){ await using var cmd=new NpgsqlCommand(sql,c); foreach(var p in ps) cmd.Parameters.AddWithValue(p.Item1,p.Item2); var list=new List<Dictionary<string,object?>>(); await using var rd=await cmd.ExecuteReaderAsync(ct); while(await rd.ReadAsync(ct)){ var d=new Dictionary<string,object?>(); for(int i=0;i<rd.FieldCount;i++) d[rd.GetName(i)]=rd.IsDBNull(i)?null:rd.GetValue(i); list.Add(d);} return list; }
        var allergies = await Q("SELECT id,allergen,allergen_type,reaction,severity,status FROM sigm.allergies WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", ("@t",tid),("@p",patientId));
        var diagnoses = await Q("SELECT d.id,dc.code,dc.description,d.diagnosis_type,d.certainty,d.created_at FROM sigm.diagnoses d JOIN sigm.diagnosis_catalog dc ON dc.id=d.diagnosis_catalog_id WHERE d.tenant_id=@t AND d.patient_id=@p ORDER BY d.created_at DESC LIMIT 50", ("@t",tid),("@p",patientId));
        var vitals = await Q("SELECT id,measured_at,temperature_c,heart_rate,respiratory_rate,systolic_bp,diastolic_bp,oxygen_saturation,weight_kg,height_cm,bmi FROM sigm.vital_signs WHERE tenant_id=@t AND patient_id=@p ORDER BY measured_at DESC LIMIT 20", ("@t",tid),("@p",patientId));
        var history = await Q("SELECT id,history_type,description,onset_date,status FROM sigm.medical_history WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC LIMIT 50", ("@t",tid),("@p",patientId));
        return Ok(new { allergies, diagnoses, vitals, history });
    }

    [HttpPost("vitals")]
    [RequirePermission("clinical.vitals.manage")]
    public async Task<IActionResult> AddVitals([FromBody] VitalsRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.vital_signs(id,tenant_id,encounter_id,patient_id,temperature_c,heart_rate,respiratory_rate,systolic_bp,diastolic_bp,oxygen_saturation,weight_kg,height_cm,glucose_mg_dl,pain_scale,notes,recorded_by)
            VALUES(gen_random_uuid(),@t,@e,@p,@temp,@hr,@rr,@sbp,@dbp,@spo2,@w,@h,@glu,@pain,@notes,@u)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@e",req.EncounterId); cmd.Parameters.AddWithValue("@p",req.PatientId);
        cmd.Parameters.AddWithValue("@temp",(object?)req.TemperatureC??DBNull.Value); cmd.Parameters.AddWithValue("@hr",(object?)req.HeartRate??DBNull.Value); cmd.Parameters.AddWithValue("@rr",(object?)req.RespiratoryRate??DBNull.Value);
        cmd.Parameters.AddWithValue("@sbp",(object?)req.SystolicBp??DBNull.Value); cmd.Parameters.AddWithValue("@dbp",(object?)req.DiastolicBp??DBNull.Value); cmd.Parameters.AddWithValue("@spo2",(object?)req.OxygenSaturation??DBNull.Value);
        cmd.Parameters.AddWithValue("@w",(object?)req.WeightKg??DBNull.Value); cmd.Parameters.AddWithValue("@h",(object?)req.HeightCm??DBNull.Value); cmd.Parameters.AddWithValue("@glu",(object?)req.GlucoseMgDl??DBNull.Value);
        cmd.Parameters.AddWithValue("@pain",(object?)req.PainScale??DBNull.Value); cmd.Parameters.AddWithValue("@notes",(object?)req.Notes??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("diagnosis-catalog")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchDx([FromQuery] string? q, [FromQuery] int take = 30, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        var sql = string.IsNullOrWhiteSpace(q)
            ? "SELECT id,code,description,chapter FROM sigm.diagnosis_catalog WHERE is_active ORDER BY code LIMIT @n"
            : "SELECT id,code,description,chapter FROM sigm.diagnosis_catalog WHERE is_active AND (code ILIKE @p OR description ILIKE @p) ORDER BY code LIMIT @n";
        await using var cmd = new NpgsqlCommand(sql,c); if(!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@p",$"%{q.Trim()}%"); cmd.Parameters.AddWithValue("@n",Math.Clamp(take,1,100));
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),code=rd.GetString(1),description=rd.GetString(2),chapter=rd.IsDBNull(3)?null:rd.GetString(3)});
        return Ok(list);
    }

    [HttpPost("diagnoses")]
    [RequirePermission("clinical.diagnoses.manage")]
    public async Task<IActionResult> AddDiagnosis([FromBody] DiagnosisRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.diagnoses(id,tenant_id,encounter_id,patient_id,diagnosis_catalog_id,diagnosis_type,certainty,notes,recorded_by)
            VALUES(gen_random_uuid(),@t,@e,@p,@cat,@type,@cert,@notes,@u)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@e",req.EncounterId); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@cat",req.DiagnosisCatalogId);
        cmd.Parameters.AddWithValue("@type",req.DiagnosisType); cmd.Parameters.AddWithValue("@cert",req.Certainty); cmd.Parameters.AddWithValue("@notes",(object?)req.Notes??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpPost("allergies")]
    [RequirePermission("clinical.allergies.manage")]
    public async Task<IActionResult> AddAllergy([FromBody] AllergyRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.allergies(id,tenant_id,patient_id,allergen,allergen_type,reaction,severity,status,recorded_by)
            VALUES(gen_random_uuid(),@t,@p,@a,@at,@r,@s,'ACTIVE',@u)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@a",req.Allergen); cmd.Parameters.AddWithValue("@at",req.AllergenType);
        cmd.Parameters.AddWithValue("@r",(object?)req.Reaction??DBNull.Value); cmd.Parameters.AddWithValue("@s",req.Severity); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpPost("history")]
    [RequirePermission("clinical.history.manage")]
    public async Task<IActionResult> AddHistory([FromBody] HistoryRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.medical_history(id,tenant_id,patient_id,encounter_id,history_type,description,onset_date,status,recorded_by)
            VALUES(gen_random_uuid(),@t,@p,@e,@ht,@d,@od,@st,@u)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",req.PatientId); cmd.Parameters.AddWithValue("@e",(object?)req.EncounterId??DBNull.Value);
        cmd.Parameters.AddWithValue("@ht",req.HistoryType); cmd.Parameters.AddWithValue("@d",req.Description); cmd.Parameters.AddWithValue("@od",(object?)req.OnsetDate??DBNull.Value);
        cmd.Parameters.AddWithValue("@st",req.Status??"ACTIVE"); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
}
