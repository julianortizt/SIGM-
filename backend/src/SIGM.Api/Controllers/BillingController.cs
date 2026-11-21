using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class BillingController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("services")]
    [RequirePermission("billing.read")]
    public async Task<IActionResult> Services(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,code,name,description,is_active FROM sigm.service_catalog WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),code=rd.IsDBNull(1)?null:rd.GetString(1),name=rd.GetString(2),description=rd.IsDBNull(3)?null:rd.GetString(3),isActive=rd.GetBoolean(4)});
        return Ok(list);
    }

    [HttpPost("services")]
    [RequirePermission("billing.manage")]
    public async Task<IActionResult> CreateService([FromBody] ServiceReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.service_catalog(id,tenant_id,code,name,description,is_active) VALUES(gen_random_uuid(),@t,COALESCE(@c,'SVC-'||substr(replace(gen_random_uuid()::text,'-',''),1,6)),@n,@d,true)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@c",(object?)req.Code??DBNull.Value); cmd.Parameters.AddWithValue("@n",req.Name); cmd.Parameters.AddWithValue("@d",(object?)req.Description??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("invoices")]
    [RequirePermission("billing.read")]
    public async Task<IActionResult> Invoices([FromQuery] string? status, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = @"SELECT i.id,i.invoice_number,i.patient_id,i.status,i.currency,i.issue_date,i.due_date,
            i.total,
            GREATEST(i.total - COALESCE((SELECT SUM(p.amount) FROM sigm.payments p WHERE p.tenant_id=i.tenant_id AND p.invoice_id=i.id),0),0),
            i.created_at FROM sigm.invoices i WHERE i.tenant_id=@t" + (status!=null?" AND i.status=@s":"") + " ORDER BY i.created_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(status!=null) cmd.Parameters.AddWithValue("@s",status);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),invoiceNumber=rd.GetString(1),patientId=rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2),status=rd.GetString(3),currency=rd.GetString(4),issueDate=rd.GetFieldValue<DateOnly>(5),dueDate=rd.IsDBNull(6)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(6),totalAmount=rd.GetDecimal(7),balanceAmount=rd.GetDecimal(8),createdAt=rd.GetFieldValue<DateTimeOffset>(9)});
        return Ok(list);
    }

    [HttpGet("invoices/{id:guid}")]
    [RequirePermission("billing.read")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        object? inv = null;
        await using (var cmd = new NpgsqlCommand(@"SELECT id,invoice_number,patient_id,encounter_id,status,currency,issue_date,due_date,
            total, GREATEST(total - COALESCE((SELECT SUM(p.amount) FROM sigm.payments p WHERE p.tenant_id=@t AND p.invoice_id=@id),0),0), created_at
            FROM sigm.invoices WHERE id=@id AND tenant_id=@t", c))
        {
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct)) inv = new { id = rd.GetGuid(0), invoiceNumber = rd.GetString(1), patientId = rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2), encounterId = rd.IsDBNull(3)?(Guid?)null:rd.GetGuid(3), status = rd.GetString(4), currency = rd.GetString(5), issueDate = rd.GetFieldValue<DateOnly>(6), dueDate = rd.IsDBNull(7)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(7), totalAmount = rd.GetDecimal(8), balanceAmount = rd.GetDecimal(9), createdAt = rd.GetFieldValue<DateTimeOffset>(10) };
        }
        if (inv is null) return NotFound();
        var items = new List<object>();
        await using (var cmd = new NpgsqlCommand("SELECT id,description,quantity,unit_price,discount_amount,tax_amount,line_total FROM sigm.invoice_items WHERE tenant_id=@t AND invoice_id=@id ORDER BY created_at", c))
        { cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@id", id); await using var rd = await cmd.ExecuteReaderAsync(ct); while (await rd.ReadAsync(ct)) items.Add(new { id = rd.GetGuid(0), description = rd.GetString(1), quantity = rd.GetDecimal(2), unitPrice = rd.GetDecimal(3), discountAmount = rd.GetDecimal(4), taxAmount = rd.GetDecimal(5), lineTotal = rd.GetDecimal(6) }); }
        var payments = new List<object>();
        await using (var cmd = new NpgsqlCommand("SELECT id,payment_date,amount,payment_method,reference,notes FROM sigm.payments WHERE tenant_id=@t AND invoice_id=@id ORDER BY payment_date DESC", c))
        { cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@id", id); await using var rd = await cmd.ExecuteReaderAsync(ct); while (await rd.ReadAsync(ct)) payments.Add(new { id = rd.GetGuid(0), paymentDate = rd.GetFieldValue<DateTimeOffset>(1), amount = rd.GetDecimal(2), paymentMethod = rd.GetString(3), reference = rd.IsDBNull(4)?null:rd.GetString(4), notes = rd.IsDBNull(5)?null:rd.GetString(5) }); }
        return Ok(new { invoice = inv, items, payments });
    }

    [HttpPost("invoices")]
    [RequirePermission("billing.create")]
    public async Task<IActionResult> CreateInvoice([FromBody] InvoiceReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var invId = Guid.NewGuid();
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.invoices(id,tenant_id,invoice_number,patient_id,encounter_id,status,currency,issue_date,due_date,created_by)
            VALUES(@id,@t,'INV-'||substr(replace(gen_random_uuid()::text,'-',''),1,10),@p,@e,'DRAFT','COP',CURRENT_DATE,@due,@u) RETURNING invoice_number", c, tx))
        {
            cmd.Parameters.AddWithValue("@id",invId); cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",(object?)req.PatientId??DBNull.Value);
            cmd.Parameters.AddWithValue("@e",(object?)req.EncounterId??DBNull.Value); cmd.Parameters.AddWithValue("@due",(object?)req.DueDate??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
            var number = (string?)await cmd.ExecuteScalarAsync(ct);
            foreach (var it in req.Items ?? [])
            {
                await using var ic = new NpgsqlCommand(@"INSERT INTO sigm.invoice_items(id,tenant_id,invoice_id,description,quantity,unit_price,discount_amount,tax_rate,tax_amount)
                    VALUES(gen_random_uuid(),@t,@inv,@d,@q,@u,@disc,
                        CASE WHEN (@q*@u-@disc) > 0 THEN ROUND(@tax/(@q*@u-@disc)*100,2) ELSE 0 END,
                        @tax)", c, tx);
                ic.Parameters.AddWithValue("@t",tid); ic.Parameters.AddWithValue("@inv",invId); ic.Parameters.AddWithValue("@d",it.Description); ic.Parameters.AddWithValue("@q",it.Quantity);
                ic.Parameters.AddWithValue("@u",it.UnitPrice); ic.Parameters.AddWithValue("@disc",it.DiscountAmount); ic.Parameters.AddWithValue("@tax",it.TaxAmount);
                await ic.ExecuteNonQueryAsync(ct);
            }
            // Totales de la factura a partir de los ítems
            await using (var tot = new NpgsqlCommand(@"UPDATE sigm.invoices i SET subtotal=t.s, discount_total=t.dsc, tax_total=t.tx,
                total=t.s-t.dsc+t.tx, updated_at=now()
                FROM (SELECT COALESCE(SUM(quantity*unit_price),0) s, COALESCE(SUM(discount_amount),0) dsc, COALESCE(SUM(tax_amount),0) tx
                      FROM sigm.invoice_items WHERE tenant_id=@t AND invoice_id=@id) t
                WHERE i.id=@id AND i.tenant_id=@t", c, tx))
            { tot.Parameters.AddWithValue("@t",tid); tot.Parameters.AddWithValue("@id",invId); await tot.ExecuteNonQueryAsync(ct); }
            await tx.CommitAsync(ct);
            return Ok(new { id = invId, invoiceNumber = number, status = "DRAFT" });
        }
    }

    [HttpPost("invoices/{id:guid}/status")]
    [RequirePermission("billing.manage")]
    public async Task<IActionResult> UpdateInvoiceStatus(Guid id, [FromBody] InvoiceStatusReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.invoices SET status=@s, updated_at=now() WHERE id=@id AND tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@s", req.Status); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        return n == 0 ? NotFound() : Ok(new { id, status = req.Status });
    }

    [HttpPost("invoices/{id:guid}/payments")]
    [RequirePermission("billing.payments.manage")]
    public async Task<IActionResult> AddPayment(Guid id, [FromBody] PaymentReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        if (req.Amount <= 0) return BadRequest(new { message = "El monto del pago debe ser mayor que cero." });

        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.payments(id,tenant_id,invoice_id,payment_date,amount,payment_method,reference,received_by,notes)
            VALUES(gen_random_uuid(),@t,@inv,COALESCE(@date,now()),@amt,@method,@ref,@u,@notes)", c, tx);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@inv", id); cmd.Parameters.AddWithValue("@date", (object?)req.PaymentDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@amt", req.Amount); cmd.Parameters.AddWithValue("@method", req.PaymentMethod ?? "CASH");
        cmd.Parameters.AddWithValue("@ref", (object?)req.Reference ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value); cmd.Parameters.AddWithValue("@notes", (object?)req.Notes ?? DBNull.Value);
        try { await cmd.ExecuteNonQueryAsync(ct); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            await tx.RollbackAsync(ct);
            return NotFound(new { message = "Factura no encontrada." });
        }
        // El saldo se calcula sobre la marcha: total - pagos. Solo se marca PAID cuando cubre el total.
        await using var up = new NpgsqlCommand(@"UPDATE sigm.invoices i SET
            status = CASE WHEN (i.total - COALESCE((SELECT SUM(p.amount) FROM sigm.payments p WHERE p.tenant_id=@t AND p.invoice_id=@id),0)) <= 0 AND i.status NOT IN ('VOID') THEN 'PAID' ELSE i.status END,
            updated_at=now() WHERE i.id=@id AND i.tenant_id=@t", c, tx);
        up.Parameters.AddWithValue("@t", tid); up.Parameters.AddWithValue("@id", id);
        await up.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return Ok();
    }

    [HttpGet("accounts-receivable")]
    [RequirePermission("billing.read")]
    public async Task<IActionResult> Ar(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM sigm.v_accounts_receivable WHERE tenant_id=@t ORDER BY issue_date DESC LIMIT 200", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<Dictionary<string,object?>>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)){ var d=new Dictionary<string,object?>(); for(int i=0;i<rd.FieldCount;i++) d[rd.GetName(i)]=rd.IsDBNull(i)?null:rd.GetValue(i); list.Add(d);} 
        return Ok(list);
    }

    public record ServiceReq(string? Code, string Name, string? Description);
    public record InvoiceReq(Guid? PatientId, Guid? EncounterId, DateOnly? DueDate, InvoiceItemReq[] Items);
    public record InvoiceItemReq(string Description, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TaxAmount);
    public record InvoiceStatusReq(string Status);
    public record PaymentReq(DateTimeOffset? PaymentDate, decimal Amount, string? PaymentMethod, string? Reference, string? Notes);
}
