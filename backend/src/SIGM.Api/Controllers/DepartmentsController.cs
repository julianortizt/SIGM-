using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission("tenant.sites.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,site_id,name,code,is_active FROM sigm.departments WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),siteId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),name=rd.GetString(2),code=rd.IsDBNull(3)?null:rd.GetString(3),isActive=rd.GetBoolean(4)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("tenant.sites.manage")]
    public async Task<IActionResult> Create([FromBody] Req req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.departments(id,tenant_id,site_id,name,code,is_active) VALUES(gen_random_uuid(),@t,@s,@n,@c,true)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@s",(object?)req.SiteId??DBNull.Value); cmd.Parameters.AddWithValue("@n",req.Name); cmd.Parameters.AddWithValue("@c",(object?)req.Code??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    public record Req(Guid? SiteId, string Name, string? Code);
}

[ApiController]
[Route("api/v1/resources")]
[Authorize]
public class ResourcesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission("tenant.sites.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,site_id,name,resource_type,is_active FROM sigm.resources WHERE tenant_id=@t ORDER BY name", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),siteId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),name=rd.GetString(2),resourceType=rd.IsDBNull(3)?null:rd.GetString(3),isActive=rd.GetBoolean(4)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("tenant.sites.manage")]
    public async Task<IActionResult> Create([FromBody] Req req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.resources(id,tenant_id,site_id,name,resource_type,is_active) VALUES(gen_random_uuid(),@t,@s,@n,@r,true)", c);
        cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@s",(object?)req.SiteId??DBNull.Value); cmd.Parameters.AddWithValue("@n",req.Name); cmd.Parameters.AddWithValue("@r",(object?)req.ResourceType??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    public record Req(Guid? SiteId, string Name, string? ResourceType);
}
