using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SIGM.Shared.Tenancy;

namespace SIGM.Infrastructure.Persistence;

public sealed class TenantConnectionFactory(IConfiguration cfg, ITenantContext tenant)
{
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default, bool platform = true)
    {
        var cs = platform
            ? (cfg.GetConnectionString("Platform") ?? cfg.GetConnectionString("Default")!)
            : (cfg.GetConnectionString("App") ?? cfg.GetConnectionString("Default")!);
        var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await ApplyContextAsync(conn, ct);
        return conn;
    }

    public async Task ApplyContextAsync(DbConnection conn, CancellationToken ct = default)
    {
        if (conn is not NpgsqlConnection npg) return;
        if (tenant.TenantId is Guid tid)
        {
            await using var cmd = npg.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.tenant_id', @v, true)";
            cmd.Parameters.AddWithValue("@v", tid.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        if (tenant.UserId is Guid uid)
        {
            await using var cmd = npg.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.user_id', @v, true)";
            cmd.Parameters.AddWithValue("@v", uid.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
