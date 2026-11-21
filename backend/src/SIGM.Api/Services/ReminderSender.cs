using System.Net;
using System.Net.Mail;

namespace SIGM.Api.Services;

public sealed record ReminderMessage(
    Guid Id,
    Guid TenantId,
    Guid AppointmentId,
    string Channel,
    string? Recipient,
    string? Subject,
    string? Body);

public sealed record ReminderDeliveryResult(bool Delivered, string? Error);

/// <summary>
/// Entrega de recordatorios. Hoy soporta correo por SMTP; SMS/WhatsApp/PUSH deben
/// implementarse con su proveedor y devolver <c>Delivered=false</c> con el motivo
/// mientras no exista integración, para no marcar como enviado algo que no salió.
/// </summary>
public sealed class ReminderSender(IConfiguration cfg, ILogger<ReminderSender> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(cfg["Reminders:Smtp:Host"]);

    public string ProviderName => IsConfigured ? "smtp" : "none";

    public async Task<ReminderDeliveryResult> SendAsync(ReminderMessage message, CancellationToken ct)
    {
        if (!IsConfigured)
            return new ReminderDeliveryResult(false, "Sin proveedor de entrega configurado (Reminders:Smtp:Host).");

        if (!string.Equals(message.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
            return new ReminderDeliveryResult(false, $"Canal {message.Channel} sin proveedor integrado; solo EMAIL está disponible.");

        if (string.IsNullOrWhiteSpace(message.Recipient))
            return new ReminderDeliveryResult(false, "El recordatorio no tiene destinatario.");

        try
        {
            var host = cfg["Reminders:Smtp:Host"]!;
            var port = cfg.GetValue("Reminders:Smtp:Port", 587);
            var fromAddress = cfg["Reminders:Smtp:FromAddress"];
            if (string.IsNullOrWhiteSpace(fromAddress))
                return new ReminderDeliveryResult(false, "Falta Reminders:Smtp:FromAddress.");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = cfg.GetValue("Reminders:Smtp:UseStartTls", true),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20_000
            };
            var user = cfg["Reminders:Smtp:User"];
            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, cfg["Reminders:Smtp:Password"] ?? string.Empty);

            using var mail = new MailMessage
            {
                From = new MailAddress(fromAddress, cfg["Reminders:Smtp:FromName"] ?? "SIGM Cloud"),
                Subject = message.Subject ?? "Recordatorio de cita",
                Body = message.Body ?? "Tiene una cita programada.",
                IsBodyHtml = false
            };
            mail.To.Add(message.Recipient);
            await client.SendMailAsync(mail, ct);
            return new ReminderDeliveryResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo entregar el recordatorio {ReminderId} por {Channel}", message.Id, message.Channel);
            return new ReminderDeliveryResult(false, ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
        }
    }
}
