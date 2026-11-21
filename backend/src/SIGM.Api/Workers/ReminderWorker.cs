using Npgsql;
using SIGM.Api.Services;

namespace SIGM.Api.Workers;

/// <summary>
/// Background worker: procesa recordatorios PENDING vencidos y los entrega con el
/// proveedor configurado (hoy SMTP). Solo se marca SENT cuando el proveedor
/// confirma la entrega; si no hay proveedor, los recordatorios permanecen PENDING.
/// </summary>
public sealed class ReminderWorker(IServiceScopeFactory scopeFactory, ReminderSender sender, ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var warned = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var deliveryEnabled = cfg.GetValue("Reminders:DeliveryEnabled", false);
                if (!deliveryEnabled || !sender.IsConfigured)
                {
                    if (!warned)
                    {
                        logger.LogWarning("ReminderWorker: entrega deshabilitada o sin proveedor (DeliveryEnabled={Enabled}, provider={Provider}). " +
                            "Los recordatorios quedan en PENDING y no se marcan como enviados.",
                            deliveryEnabled, sender.ProviderName);
                        warned = true;
                    }
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var cs = cfg.GetConnectionString("Platform") ?? cfg.GetConnectionString("Default")!;
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync(stoppingToken);

                var due = new List<(Guid Id, Guid TenantId, Guid AppointmentId, string Channel, string? Recipient, DateTimeOffset StartsAt, string? PatientName, string TenantName)>();
                await using (var cmd = new NpgsqlCommand(@"SELECT r.id, r.tenant_id, r.appointment_id, r.channel,
                        COALESCE(r.recipient, p.email, p.mobile_phone), a.starts_at, p.full_name, t.commercial_name
                    FROM sigm.reminders r
                    JOIN sigm.appointments a ON a.id = r.appointment_id AND a.tenant_id = r.tenant_id
                    JOIN sigm.tenants t ON t.id = r.tenant_id
                    LEFT JOIN sigm.patients p ON p.id = a.patient_id
                    WHERE r.status='PENDING' AND r.scheduled_at <= now()
                    ORDER BY r.scheduled_at LIMIT 50", conn))
                await using (var rd = await cmd.ExecuteReaderAsync(stoppingToken))
                {
                    while (await rd.ReadAsync(stoppingToken))
                        due.Add((rd.GetGuid(0), rd.GetGuid(1), rd.GetGuid(2), rd.GetString(3),
                            rd.IsDBNull(4) ? null : rd.GetString(4),
                            rd.GetFieldValue<DateTimeOffset>(5),
                            rd.IsDBNull(6) ? null : rd.GetString(6),
                            rd.IsDBNull(7) ? "su institución" : rd.GetString(7)));
                }

                var sent = 0; var failed = 0;
                foreach (var r in due)
                {
                    var body = $"Estimado(a) {r.PatientName ?? "paciente"}: le recordamos su cita en {r.TenantName} " +
                               $"el {r.StartsAt:yyyy-MM-dd HH:mm}. Si no puede asistir, comuníquese con la institución.";
                    var message = new ReminderMessage(r.Id, r.TenantId, r.AppointmentId, r.Channel, r.Recipient,
                        $"Recordatorio de cita · {r.TenantName}", body);
                    var result = await sender.SendAsync(message, stoppingToken);

                    await using var up = new NpgsqlCommand(result.Delivered
                        ? "UPDATE sigm.reminders SET status='SENT', sent_at=now(), error_message=NULL, updated_at=now() WHERE id=@id AND status='PENDING'"
                        : "UPDATE sigm.reminders SET status='FAILED', error_message=@err, updated_at=now() WHERE id=@id AND status='PENDING'", conn);
                    up.Parameters.AddWithValue("@id", r.Id);
                    if (!result.Delivered) up.Parameters.AddWithValue("@err", result.Error ?? "Entrega fallida");
                    var n = await up.ExecuteNonQueryAsync(stoppingToken);
                    if (n > 0 && result.Delivered) sent++; else if (n > 0) failed++;
                }
                if (sent > 0 || failed > 0)
                    logger.LogInformation("ReminderWorker: {Total} recordatorios procesados (SENT={Sent}, FAILED={Failed}) vía {Provider}.",
                        due.Count, sent, failed, sender.ProviderName);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "ReminderWorker: error en ciclo");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
