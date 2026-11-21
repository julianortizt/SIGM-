using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/medical-offices")]
[Authorize]
public class MedicalOfficesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission("tenant.sites.read")]
    public async Task<IActionResult> List([FromQuery] Guid? siteId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,site_id,name,code,floor,capacity,is_active FROM sigm.medical_offices WHERE tenant_id=@t" + (siteId.HasValue?" AND site_id=@s":"") + " ORDER BY name";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(siteId.HasValue) cmd.Parameters.AddWithValue("@s",siteId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),siteId=rd.GetGuid(1),name=rd.GetString(2),code=rd.IsDBNull(3)?null:rd.GetString(3),floor=rd.IsDBNull(4)?null:rd.GetString(4),capacity=rd.IsDBNull(5)?(int?)null:rd.GetInt32(5),isActive=rd.GetBoolean(6)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("tenant.sites.manage")]
    public async Task<IActionResult> Create([FromBody] Req req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.medical_offices(id,tenant_id,site_id,name,code,floor,capacity,is_active) VALUES(gen_random_uuid(),@t,@s,@n,@c,@f,@cap,true)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@s",req.SiteId); cmd.Parameters.AddWithValue("@n",req.Name);
        cmd.Parameters.AddWithValue("@c",(object?)req.Code??DBNull.Value); cmd.Parameters.AddWithValue("@f",(object?)req.Floor??DBNull.Value); cmd.Parameters.AddWithValue("@cap",(object?)req.Capacity??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    public record Req(Guid SiteId, string Name, string? Code, string? Floor, int? Capacity);
}
