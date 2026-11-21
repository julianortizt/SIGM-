using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Controllers;

/// <summary>
/// Contenido de la pagina publica de bienvenida. La lectura es anonima (sitio
/// publico) y la escritura requiere el permiso de superadministrador de
/// plataforma. El contenido se guarda en sigm.platform_settings ('welcome').
/// </summary>
[ApiController]
public class WelcomeController(TenantConnectionFactory db) : ControllerBase
{
    private const string DefaultTitle = "Bienvenido a SIGM Cloud";
    private const string DefaultMessage =
        "Plataforma integral de gestion medica para clinicas, consultorios y pacientes.";

    [HttpGet("/api/v1/public/welcome")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            await using var c = await db.OpenAsync(ct, platform: true);
            await using var cmd = new NpgsqlCommand(
                "SELECT value::text, updated_at FROM sigm.platform_settings WHERE setting_key='welcome'", c);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct))
                return Ok(new { title = DefaultTitle, message = DefaultMessage, updatedAt = (DateTimeOffset?)null });

            var updatedAt = rd.IsDBNull(1) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(1);
            using var doc = JsonDocument.Parse(rd.GetString(0));
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : DefaultTitle;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : DefaultMessage;
            return Ok(new { title, message, updatedAt });
        }
        catch (Exception ex) when (ex is JsonException or NpgsqlException)
        {
            // Si la tabla aun no existe o el contenido es invalido, el sitio publico
            // conserva el contenido por defecto en lugar de fallar.
            return Ok(new { title = DefaultTitle, message = DefaultMessage, updatedAt = (DateTimeOffset?)null });
        }
    }

    [HttpPut("/api/v1/platform/welcome")]
    [Authorize]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> Update([FromBody] WelcomeReq req, CancellationToken ct)
    {
        var title = string.IsNullOrWhiteSpace(req.Title) ? DefaultTitle : req.Title.Trim();
        var message = string.IsNullOrWhiteSpace(req.Message) ? DefaultMessage : req.Message.Trim();
        if (title.Length > 120 || message.Length > 600)
            return BadRequest(new { message = "El titulo admite hasta 120 caracteres y el mensaje hasta 600." });

        var json = JsonSerializer.Serialize(new { title, message });
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO sigm.platform_settings(setting_key, value, updated_at)
            VALUES('welcome', @v::jsonb, now())
            ON CONFLICT (setting_key) DO UPDATE SET value=EXCLUDED.value, updated_at=now()", c);
        cmd.Parameters.AddWithValue("@v", json);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { title, message });
    }

    public record WelcomeReq(string? Title, string? Message);
}
