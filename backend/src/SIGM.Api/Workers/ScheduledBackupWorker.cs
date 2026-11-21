using SIGM.Api.Services;

namespace SIGM.Api.Workers;

/// <summary>
/// Respaldo automático de la base de datos. Se ejecuta según
/// <c>Database:AutoBackupIntervalHours</c> (24 h por defecto) y usa el mismo
/// servicio que la consola, de modo que cada archivo queda registrado, verificado
/// con pg_restore y sujeto a la retención configurada.
/// </summary>
public sealed class ScheduledBackupWorker(IServiceScopeFactory scopes, ILogger<ScheduledBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Espera inicial para no competir con el arranque de la API.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = TimeSpan.FromMinutes(30);
            try
            {
                using var scope = scopes.CreateScope();
                var backups = scope.ServiceProvider.GetRequiredService<DatabaseBackupService>();

                if (!backups.AutoBackupEnabled)
                {
                    wait = TimeSpan.FromMinutes(30);
                }
                else
                {
                    var interval = TimeSpan.FromHours(backups.AutoBackupIntervalHours);
                    var last = await backups.LastAutomaticBackupAtAsync(stoppingToken);
                    var now = DateTimeOffset.UtcNow;
                    var next = last is DateTimeOffset lastAt ? lastAt + interval : now;

                    if (next > now)
                    {
                        wait = next - now;
                    }
                    else
                    {
                        logger.LogInformation("Iniciando respaldo automático programado (cada {Hours} h).", backups.AutoBackupIntervalHours);
                        var result = await backups.CreateAsync("Respaldo automático programado", null, stoppingToken);
                        if (result.Error is not null)
                            logger.LogError("Respaldo automático falló: {Error}", result.Error);
                        else
                            logger.LogInformation("Respaldo automático completado y verificado: {Verification}", result.Verification?.Message ?? "sin verificación");

                        wait = interval;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El trabajo de respaldos automáticos falló; se reintentará en 30 minutos.");
                wait = TimeSpan.FromMinutes(30);
            }

            // Recalcula al menos cada hora para reaccionar a cambios de configuración.
            if (wait > TimeSpan.FromHours(1)) wait = TimeSpan.FromHours(1);
            if (wait < TimeSpan.FromSeconds(10)) wait = TimeSpan.FromSeconds(10);
            try { await Task.Delay(wait, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
