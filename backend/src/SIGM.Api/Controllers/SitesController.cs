using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/sites")]
[Authorize]
public class SitesController(SigmDbContext db, TenantConnectionFactory cf, ITenantContext tenant) : ControllerBase
{
    public record CreateSiteRequest(string Name, string? Code, string? AddressLine, string? City, string? Department, string? Phone, string? Email);

    [HttpGet]
    [RequirePermission("tenant.sites.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var items = await db.Sites.AsNoTracking().Where(s => s.TenantId == tid).OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("tenant.sites.manage")]
    public async Task<IActionResult> Create([FromBody] CreateSiteRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var id = Guid.NewGuid();
        await using var conn = await cf.OpenAsync(ct);
        Guid? orgId = null;
        await using (var q = conn.CreateCommand())
        {
            q.CommandText = "SELECT id FROM sigm.organizations WHERE tenant_id=@tid LIMIT 1";
            q.Parameters.AddWithValue("@tid", tid);
            var o = await q.ExecuteScalarAsync(ct);
            if (o is Guid g) orgId = g;
        }
        if (orgId is null) return BadRequest(new { message = "El tenant no tiene organización configurada." });
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO sigm.sites(id,tenant_id,organization_id,code,name,address_line,city,department,phone,email,is_active)
                            VALUES(@id,@tid,@org,@code,@name,@addr,@city,@dep,@phone,@email,true) RETURNING id";
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid); cmd.Parameters.AddWithValue("@org", orgId.Value);
        cmd.Parameters.AddWithValue("@name", req.Name.Trim()); cmd.Parameters.AddWithValue("@code", (object?)req.Code ?? ("SITE-" + id.ToString("N")[..6].ToUpperInvariant()));
        cmd.Parameters.AddWithValue("@addr", (object?)req.AddressLine ?? DBNull.Value); cmd.Parameters.AddWithValue("@city", (object?)req.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dep", (object?)req.Department ?? DBNull.Value); cmd.Parameters.AddWithValue("@phone", (object?)req.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email", (object?)req.Email ?? DBNull.Value);
        await cmd.ExecuteScalarAsync(ct);
        return Ok(new { id });
    }
}
