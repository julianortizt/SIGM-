using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/clinical-extended")]
[Authorize]
public class ClinicalExtendedController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    // MEDICAL ORDERS
    [HttpGet("medical-orders")][RequirePermission("clinical.orders.manage")]
    public async Task<IActionResult> ListMedicalOrders([FromQuery] Guid? patientId, [FromQuery] Guid? encounterId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,encounter_id,patient_id,order_type,description,priority,status,ordered_by,ordered_at,notes FROM sigm.medical_orders WHERE tenant_id=@t";
        if (patientId.HasValue) sql += " AND patient_id=@p"; if (encounterId.HasValue) sql += " AND encounter_id=@e"; sql += " ORDER BY ordered_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid);
        if (patientId.HasValue) cmd.Parameters.AddWithValue("@p", patientId.Value); if (encounterId.HasValue) cmd.Parameters.AddWithValue("@e", encounterId.Value);
        return Ok(await ReadAll(cmd, ct));
    }
    private static readonly string[] OrderTypes = ["LABORATORY","IMAGING","PROCEDURE","REFERRAL","MEDICATION","OTHER"];
    private static readonly string[] Priorities = ["ROUTINE","URGENT","STAT"];
    private static readonly string[] ReportTypes = ["LABORATORY","IMAGING","PATHOLOGY","CARDIOLOGY","OTHER"];

    [HttpPost("medical-orders")][RequirePermission("clinical.orders.manage")]
    public async Task<IActionResult> CreateMedicalOrder([FromBody] MedicalOrderReq req, CancellationToken ct)
    {
        if (!OrderTypes.Contains(req.OrderType)) return BadRequest(new { message = $"order_type debe ser: {string.Join(", ", OrderTypes)}." });
        if (req.Priority is string pr && !Priorities.Contains(pr)) return BadRequest(new { message = $"priority debe ser: {string.Join(", ", Priorities)}." });
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.medical_orders(id,tenant_id,encounter_id,patient_id,order_type,description,priority,status,ordered_by,notes)
            VALUES(@id,@t,@e,@p,@type,@desc,@prio,@st,@u,@notes)", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@e", req.EncounterId); cmd.Parameters.AddWithValue("@p", req.PatientId);
        cmd.Parameters.AddWithValue("@type", req.OrderType); cmd.Parameters.AddWithValue("@desc", req.Description); cmd.Parameters.AddWithValue("@prio", req.Priority ?? "ROUTINE");
        cmd.Parameters.AddWithValue("@st", req.Status ?? "REQUESTED"); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value); cmd.Parameters.AddWithValue("@notes", (object?)req.Notes ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok(new { id });
    }

    // PROCEDURE ORDERS
    [HttpGet("procedure-orders")][RequirePermission("clinical.orders.manage")]
    public async Task<IActionResult> ListProcedureOrders([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,encounter_id,patient_id,procedure_catalog_id,quantity,status,ordered_by,performed_at,notes FROM sigm.procedure_orders WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY created_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid); if (patientId.HasValue) cmd.Parameters.AddWithValue("@p", patientId.Value);
        return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("procedure-orders")][RequirePermission("clinical.orders.manage")]
    public async Task<IActionResult> CreateProcedureOrder([FromBody] ProcedureOrderReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.procedure_orders(id,tenant_id,encounter_id,patient_id,procedure_catalog_id,quantity,status,ordered_by,notes)
            VALUES(gen_random_uuid(),@t,@e,@p,@pc,@q,'REQUESTED',@u,@notes)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@e", req.EncounterId); cmd.Parameters.AddWithValue("@p", req.PatientId);
        cmd.Parameters.AddWithValue("@pc", req.ProcedureCatalogId); cmd.Parameters.AddWithValue("@q", req.Quantity <= 0 ? 1 : req.Quantity);
        cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value); cmd.Parameters.AddWithValue("@notes", (object?)req.Notes ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    // DIAGNOSTIC REPORTS
    [HttpGet("diagnostic-reports")][RequirePermission("clinical.read")]
    public async Task<IActionResult> ListReports([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,encounter_id,report_type,title,content,document_id,status,reported_by,reported_at,created_at FROM sigm.diagnostic_reports WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY created_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid); if (patientId.HasValue) cmd.Parameters.AddWithValue("@p", patientId.Value);
        return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("diagnostic-reports")][RequirePermission("clinical.create")]
    public async Task<IActionResult> CreateReport([FromBody] DiagnosticReportReq req, CancellationToken ct)
    {
        if (!ReportTypes.Contains(req.ReportType)) return BadRequest(new { message = $"report_type debe ser: {string.Join(", ", ReportTypes)}." });
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.diagnostic_reports(id,tenant_id,patient_id,encounter_id,report_type,title,content,document_id,status,reported_by,reported_at)
            VALUES(gen_random_uuid(),@t,@p,@e,@type,@title,@content,@doc,'FINAL',@u,now())", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", req.PatientId); cmd.Parameters.AddWithValue("@e", (object?)req.EncounterId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@type", req.ReportType); cmd.Parameters.AddWithValue("@title", req.Title); cmd.Parameters.AddWithValue("@content", (object?)req.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@doc", (object?)req.DocumentId ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    // PROCEDURE CATALOG (read)
    [HttpGet("procedure-catalog")][AllowAnonymous]
    public async Task<IActionResult> Procedures([FromQuery] string? q, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        var sql = string.IsNullOrWhiteSpace(q)
            ? "SELECT id,code,name,chapter FROM sigm.procedure_catalog WHERE is_active ORDER BY code LIMIT 100"
            : "SELECT id,code,name,chapter FROM sigm.procedure_catalog WHERE is_active AND (code ILIKE @p OR name ILIKE @p) ORDER BY code LIMIT 100";
        await using var cmd = new NpgsqlCommand(sql, c);
        if (!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@p", $"%{q.Trim()}%");
        return Ok(await ReadAll(cmd, ct));
    }

    private static async Task<List<Dictionary<string, object?>>> ReadAll(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Dictionary<string, object?>>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) { var d = new Dictionary<string, object?>(); for (int i = 0; i < rd.FieldCount; i++) d[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i); list.Add(d); }
        return list;
    }

    public record MedicalOrderReq(Guid EncounterId, Guid PatientId, string OrderType, string Description, string? Priority, string? Status, string? Notes);
    public record ProcedureOrderReq(Guid EncounterId, Guid PatientId, Guid ProcedureCatalogId, int Quantity, string? Notes);
    public record DiagnosticReportReq(Guid PatientId, Guid? EncounterId, string ReportType, string Title, string? Content, Guid? DocumentId);
}
