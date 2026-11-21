using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/pricing")]
[Authorize]
public class PricingAndAdminController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("price-lists")][RequirePermission("billing.read")]
    public async Task<IActionResult> PriceLists(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,name,insurer_id,contract_id,valid_from,valid_to,is_active FROM sigm.price_lists WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("price-lists")][RequirePermission("billing.manage")]
    public async Task<IActionResult> CreatePriceList([FromBody] PriceListReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.price_lists(id,tenant_id,name,insurer_id,contract_id,valid_from,valid_to,is_active)
            VALUES(gen_random_uuid(),@t,@n,@ins,@con,@vf,@vt,true)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@n", req.Name); cmd.Parameters.AddWithValue("@ins", (object?)req.InsurerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@con", (object?)req.ContractId ?? DBNull.Value); cmd.Parameters.AddWithValue("@vf", (object?)req.ValidFrom ?? DBNull.Value); cmd.Parameters.AddWithValue("@vt", (object?)req.ValidTo ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    [HttpGet("price-lists/{id:guid}/items")][RequirePermission("billing.read")]
    public async Task<IActionResult> PriceListItems(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,service_id,price,discount_percent FROM sigm.price_list_items WHERE tenant_id=@t AND price_list_id=@l", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@l", id); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("price-lists/{id:guid}/items")][RequirePermission("billing.manage")]
    public async Task<IActionResult> AddPriceListItem(Guid id, [FromBody] PriceItemReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.price_list_items(id,tenant_id,price_list_id,service_id,price,discount_percent)
            VALUES(gen_random_uuid(),@t,@l,@s,@p,@d)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@l", id); cmd.Parameters.AddWithValue("@s", req.ServiceId);
        cmd.Parameters.AddWithValue("@p", req.Price); cmd.Parameters.AddWithValue("@d", req.DiscountPercent);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("/api/v1/document-categories")][RequirePermission("documents.read")]
    public async Task<IActionResult> DocCategories(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,code,name,description,is_system,is_active FROM sigm.document_categories WHERE tenant_id=@t OR tenant_id IS NULL ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }

    [HttpGet("/api/v1/credit-debit-notes")][RequirePermission("billing.read")]
    public async Task<IActionResult> CreditDebitNotes([FromQuery] Guid? invoiceId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,invoice_id,note_number,note_type,reason,amount,issued_at,status FROM sigm.credit_debit_notes WHERE tenant_id=@t" + (invoiceId.HasValue?" AND invoice_id=@i":"") + " ORDER BY issued_at DESC";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", tid); if (invoiceId.HasValue) cmd.Parameters.AddWithValue("@i", invoiceId.Value);
        return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("/api/v1/credit-debit-notes")][RequirePermission("billing.manage")]
    public async Task<IActionResult> CreateCreditDebitNote([FromBody] CreditNoteReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.credit_debit_notes(id,tenant_id,invoice_id,note_number,note_type,reason,amount,status,created_by)
            VALUES(gen_random_uuid(),@t,@i,@num,@type,@reason,@amt,'ISSUED',@u)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@i", req.InvoiceId); cmd.Parameters.AddWithValue("@num", req.NoteNumber ?? ("ND-" + Guid.NewGuid().ToString("N")[..8].ToUpper()));
        cmd.Parameters.AddWithValue("@type", req.NoteType ?? "CREDIT"); cmd.Parameters.AddWithValue("@reason", req.Reason); cmd.Parameters.AddWithValue("@amt", req.Amount); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("/api/v1/rips/reports")][RequirePermission("rips.manage")]
    public async Task<IActionResult> RipsReports(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,period_start,period_end,report_type,status,total_records,total_amount,generated_at,transmitted_at FROM sigm.rips_reports WHERE tenant_id=@t ORDER BY created_at DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }
    [HttpPost("/api/v1/rips/reports")][RequirePermission("rips.manage")]
    public async Task<IActionResult> CreateRipsReport([FromBody] RipsReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.rips_reports(id,tenant_id,period_start,period_end,report_type,status,total_records,total_amount,response)
            VALUES(gen_random_uuid(),@t,@ps,@pe,@type,'DRAFT',0,0,'{}'::jsonb)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@ps", req.PeriodStart); cmd.Parameters.AddWithValue("@pe", req.PeriodEnd); cmd.Parameters.AddWithValue("@type", req.ReportType ?? "GENERAL");
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("/api/v1/subscription-payments")][RequirePermission("tenant.settings.read")]
    public async Task<IActionResult> SubscriptionPayments(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,subscription_id,amount,currency,period_start,period_end,status,paid_at,reference FROM sigm.subscription_payments WHERE tenant_id=@t ORDER BY created_at DESC", c);
        cmd.Parameters.AddWithValue("@t", tid); return Ok(await ReadAll(cmd, ct));
    }

    private static async Task<List<Dictionary<string, object?>>> ReadAll(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Dictionary<string, object?>>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) { var d = new Dictionary<string, object?>(); for (int i = 0; i < rd.FieldCount; i++) d[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i); list.Add(d); }
        return list;
    }

    public record PriceListReq(string Name, Guid? InsurerId, Guid? ContractId, DateOnly? ValidFrom, DateOnly? ValidTo);
    public record PriceItemReq(Guid ServiceId, decimal Price, decimal DiscountPercent);
    public record CreditNoteReq(Guid InvoiceId, string? NoteNumber, string? NoteType, string Reason, decimal Amount);
    public record RipsReq(DateOnly PeriodStart, DateOnly PeriodEnd, string? ReportType);
}
