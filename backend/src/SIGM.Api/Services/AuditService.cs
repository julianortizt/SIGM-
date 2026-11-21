using System.Text.Json;
using Npgsql;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Services;

public sealed class AuditService(TenantConnectionFactory db, ITenantContext tenant, ILogger<AuditService> logger)
{
    public async Task LogAsync(string entityType, Guid? entityId, string action, object? before = null, object? after = null, string? ip = null, CancellationToken ct = default)
    {
        if (tenant.TenantId is not Guid tid) return;
        try
        {
            var metadata = JsonSerializer.Serialize(new { before, after });
            await using var c = await db.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.audit_logs(id,tenant_id,user_id,entity_type,entity_id,action,result,metadata,ip_address,occurred_at)
                VALUES(gen_random_uuid(),@t,@u,@et,@eid,@a,'SUCCESS',@meta::jsonb,CAST(@ip AS inet),now())", c);
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@et", (object?)entityType ?? DBNull.Value); cmd.Parameters.AddWithValue("@eid", (object?)entityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", action);
            cmd.Parameters.AddWithValue("@meta", metadata);
            cmd.Parameters.AddWithValue("@ip", (object?)ip ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la operación principal, pero sí dejar rastro.
            logger.LogWarning(ex, "No se pudo registrar auditoría {EntityType}/{Action}", entityType, action);
        }
    }
}
