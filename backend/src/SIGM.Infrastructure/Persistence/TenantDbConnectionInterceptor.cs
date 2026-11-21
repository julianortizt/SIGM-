using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SIGM.Shared.Tenancy;

namespace SIGM.Infrastructure.Persistence;

public sealed class TenantDbConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (connection is NpgsqlConnection npg)
        {
            await using (var clear = npg.CreateCommand())
            {
                clear.CommandText = "SELECT set_config('app.tenant_id', '', false), set_config('app.user_id', '', false)";
                await clear.ExecuteNonQueryAsync(cancellationToken);
            }
            if (tenant.TenantId is Guid tid)
                await using (var cmd = npg.CreateCommand()) { cmd.CommandText = "SELECT set_config('app.tenant_id', @v, false)"; cmd.Parameters.AddWithValue("@v", tid.ToString()); await cmd.ExecuteNonQueryAsync(cancellationToken); }
            if (tenant.UserId is Guid uid)
                await using (var cmd = npg.CreateCommand()) { cmd.CommandText = "SELECT set_config('app.user_id', @v, false)"; cmd.Parameters.AddWithValue("@v", uid.ToString()); await cmd.ExecuteNonQueryAsync(cancellationToken); }
        }
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
