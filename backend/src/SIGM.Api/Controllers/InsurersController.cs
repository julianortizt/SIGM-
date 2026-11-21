using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/insurers")]
[Authorize]
public class InsurersController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission("eps.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,code,name,nit,insurer_type,is_active FROM sigm.insurers ORDER BY name LIMIT 500", c);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),code=rd.IsDBNull(1)?null:rd.GetString(1),name=rd.GetString(2),nit=rd.IsDBNull(3)?null:rd.GetString(3),insurerType=rd.IsDBNull(4)?null:rd.GetString(4),isActive=rd.GetBoolean(5)});
        return Ok(list);
    }

    [HttpGet("/api/v1/contracts")]
    [RequirePermission("eps.read")]
    public async Task<IActionResult> Contracts(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,insurer_id,contract_number,name,status,valid_from,valid_to FROM sigm.contracts WHERE tenant_id=@t ORDER BY valid_from DESC NULLS LAST", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),insurerId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),contractNumber=rd.IsDBNull(2)?null:rd.GetString(2),name=rd.IsDBNull(3)?null:rd.GetString(3),status=rd.IsDBNull(4)?null:rd.GetString(4),validFrom=rd.IsDBNull(5)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(5),validTo=rd.IsDBNull(6)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(6)});
        return Ok(list);
    }

    [HttpGet("/api/v1/authorizations")]
    [RequirePermission("eps.read")]
    public async Task<IActionResult> Authorizations([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,authorization_number,status,requested_at,approved_at,valid_until FROM sigm.authorizations WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY requested_at DESC NULLS LAST LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(patientId.HasValue) cmd.Parameters.AddWithValue("@p",patientId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),patientId=rd.GetGuid(1),authorizationNumber=rd.IsDBNull(2)?null:rd.GetString(2),status=rd.GetString(3),requestedAt=rd.IsDBNull(4)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(4),approvedAt=rd.IsDBNull(5)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(5),validUntil=rd.IsDBNull(6)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(6)});
        return Ok(list);
    }

    [HttpGet("/api/v1/payments")]
    [RequirePermission("billing.read")]
    public async Task<IActionResult> Payments([FromQuery] Guid? invoiceId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,invoice_id,amount,payment_method,payment_date,reference,notes,received_by,created_at FROM sigm.payments WHERE tenant_id=@t" + (invoiceId.HasValue?" AND invoice_id=@i":"") + " ORDER BY payment_date DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(invoiceId.HasValue) cmd.Parameters.AddWithValue("@i",invoiceId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),invoiceId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),amount=rd.GetDecimal(2),paymentMethod=rd.GetString(3),paymentDate=rd.GetFieldValue<DateTimeOffset>(4),reference=rd.IsDBNull(5)?null:rd.GetString(5),notes=rd.IsDBNull(6)?null:rd.GetString(6),receivedBy=rd.IsDBNull(7)?(Guid?)null:rd.GetGuid(7),createdAt=rd.GetFieldValue<DateTimeOffset>(8)});
        return Ok(list);
    }

    [HttpPost("/api/v1/payments")]
    [RequirePermission("billing.payments.manage")]
    public async Task<IActionResult> CreatePayment([FromBody] PayReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.payments(id,tenant_id,invoice_id,amount,payment_method,payment_date,reference,received_by,notes)
            VALUES(gen_random_uuid(),@t,@i,@a,@m,COALESCE(@d,now()),@r,@u,@n)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@i",(object?)req.InvoiceId??DBNull.Value); cmd.Parameters.AddWithValue("@a",req.Amount);
        cmd.Parameters.AddWithValue("@m",(object?)req.PaymentMethod??"CASH"); cmd.Parameters.AddWithValue("@d",(object?)req.PaymentDate??DBNull.Value);
        cmd.Parameters.AddWithValue("@r",(object?)req.Reference??DBNull.Value); cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value); cmd.Parameters.AddWithValue("@n",(object?)req.Notes??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    public record PayReq(Guid? InvoiceId, decimal Amount, string? PaymentMethod, DateTimeOffset? PaymentDate, string? Reference, string? Notes);
}
