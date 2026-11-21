using System.Net;
using System.Net.Mail;

namespace SIGM.Api.Services;

/// <summary>
/// Envío de correo transaccional (restablecimiento de contraseña, avisos de
/// seguridad). Usa la misma configuración SMTP que los recordatorios; si no hay
/// proveedor configurado devuelve <c>false</c> y el llamador decide (en
/// desarrollo se registra el enlace en el log).
/// </summary>
public sealed class EmailSender(IConfiguration cfg, ILogger<EmailSender> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(cfg["Reminders:Smtp:Host"])
                                && !string.IsNullOrWhiteSpace(cfg["Reminders:Smtp:FromAddress"]);

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Correo no enviado a {Recipient}: no hay SMTP configurado (Reminders:Smtp).", to);
            return false;
        }

        try
        {
            var host = cfg["Reminders:Smtp:Host"]!;
            var port = cfg.GetValue("Reminders:Smtp:Port", 587);
            var fromAddress = cfg["Reminders:Smtp:FromAddress"]!;
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
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mail.To.Add(to);
            await client.SendMailAsync(mail, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el correo a {Recipient}", to);
            return false;
        }
    }
}
