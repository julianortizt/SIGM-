using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Services;

/// <summary>
/// Comprueba que el JWT siga vigente frente al estado del usuario. La versión del
/// token (<c>tv</c>) permite revocar todas las sesiones al desactivar MFA, cambiar
/// la contraseña o restablecerla. El resultado se cachea unos segundos para no
/// consultar la base de datos en cada petición.
/// </summary>
public sealed class UserSessionValidator(TenantConnectionFactory db, IMemoryCache cache, ILogger<UserSessionValidator> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);

    public async Task<bool> IsCurrentAsync(Guid userId, int tokenVersion, CancellationToken ct)
    {
        var key = $"session-tv:{userId}";
        if (cache.TryGetValue<int>(key, out var cached)) return cached == tokenVersion;

        try
        {
            await using var c = await db.OpenAsync(ct, platform: true);
            await using var cmd = new NpgsqlCommand("SELECT COALESCE(token_version,0), is_active FROM sigm.users WHERE id=@u", c);
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return false;
            var current = rd.GetInt32(0);
            var active = rd.GetBoolean(1);
            cache.Set(key, current, CacheTtl);
            return active && current == tokenVersion;
        }
        catch (Exception ex)
        {
            // Si la base no responde no se debe tumbar la API: se deja pasar la
            // petición, que fallará en su propio acceso a datos si corresponde.
            logger.LogWarning(ex, "No se pudo validar la versión de sesión del usuario {UserId}", userId);
            return true;
        }
    }

    public void Invalidate(Guid userId) => cache.Remove($"session-tv:{userId}");
}
