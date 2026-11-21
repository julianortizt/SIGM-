using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Controllers;

/// <summary>
/// Panel oculto de superadministración de plataforma (soporte SaaS).
/// Permite listar/ver tenants, suspender o activar cuentas, activar/desactivar
/// módulos por cliente y consultar eventos de autenticación globales.
/// Requiere el permiso platform.tenants.manage (rol global PLATFORM_ADMIN).
/// </summary>
[ApiController]
[Route("api/v1/platform")]
[Authorize]
public class PlatformController(TenantConnectionFactory db) : ControllerBase
{
    /// <summary>Catálogo de módulos conmutables por tenant (feature_code -> nombre).</summary>
    public static readonly (string Code, string Name, string Description)[] Modules =
    [
        ("appointments", "Agenda / Citas", "Agendamiento institucional"),
        ("waiting_list", "Lista de espera", "Administración de lista de espera"),
        ("reminders", "Recordatorios", "Recordatorios automáticos de citas"),
        ("prescriptions", "Fórmulas médicas", "Prescripción electrónica"),
        ("laboratory", "Laboratorio", "Órdenes y resultados de laboratorio"),
        ("billing", "Facturación", "Facturación y cartera"),
        ("insurers", "EPS y aseguradores", "Contratos, autorizaciones y pagos"),
        ("documents", "Documentos", "Gestión documental clínica"),
        ("interoperability", "Interoperabilidad", "Intercambio RDA/RIPS con terceros"),
        ("audit", "Auditoría", "Consulta de logs y accesos del tenant"),
    ];

    [HttpGet("modules")]
    public IActionResult ModulesCatalog() => Ok(Modules.Select(m => new { code = m.Code, name = m.Name, description = m.Description }));

    [HttpGet("tenants")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> Tenants(CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT t.id, t.tenant_code, t.commercial_name, t.legal_name, t.tax_id, t.product_type, t.status,
                       t.timezone, t.created_at,
                       (SELECT count(*) FROM sigm.user_tenants ut WHERE ut.tenant_id=t.id AND ut.status='ACTIVE') AS users,
                       (SELECT count(*) FROM sigm.patients p WHERE p.tenant_id=t.id) AS patients,
                       (SELECT count(*) FROM sigm.appointments a WHERE a.tenant_id=t.id) AS appointments,
                       (SELECT count(*) FROM sigm.tenant_features f WHERE f.tenant_id=t.id AND f.enabled=false) AS disabled_modules,
                       (SELECT pl.name FROM sigm.tenant_subscriptions ts JOIN sigm.plans pl ON pl.id=ts.plan_id
                         WHERE ts.tenant_id=t.id ORDER BY ts.started_at DESC LIMIT 1) AS plan_name
                FROM sigm.tenants t
                ORDER BY t.created_at DESC", c);
        var list = new List<Dictionary<string, object?>>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < rd.FieldCount; i++) d[ToCamel(rd.GetName(i))] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            list.Add(d);
        }
        return Ok(list);
    }

    [HttpGet("tenants/{id:guid}")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> TenantDetail(Guid id, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT t.id, t.tenant_code, t.commercial_name, t.legal_name, t.tax_id, t.product_type, t.status,
                       t.timezone, t.locale, t.currency, t.created_at,
                       (SELECT count(*) FROM sigm.user_tenants ut WHERE ut.tenant_id=t.id AND ut.status='ACTIVE') AS users,
                       (SELECT count(*) FROM sigm.patients p WHERE p.tenant_id=t.id) AS patients,
                       (SELECT count(*) FROM sigm.appointments a WHERE a.tenant_id=t.id) AS appointments,
                       (SELECT count(*) FROM sigm.professionals pr WHERE pr.tenant_id=t.id) AS professionals,
                       (SELECT count(*) FROM sigm.sites s WHERE s.tenant_id=t.id) AS sites,
                       (SELECT pl.name FROM sigm.tenant_subscriptions ts JOIN sigm.plans pl ON pl.id=ts.plan_id
                         WHERE ts.tenant_id=t.id ORDER BY ts.started_at DESC LIMIT 1) AS plan_name,
                       (SELECT ts.status FROM sigm.tenant_subscriptions ts WHERE ts.tenant_id=t.id ORDER BY ts.started_at DESC LIMIT 1) AS subscription_status
                FROM sigm.tenants t WHERE t.id=@id", c);
        cmd.Parameters.AddWithValue("@id", id);
        var row = new Dictionary<string, object?>();
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await rd.ReadAsync(ct)) return NotFound(new { message = "Tenant no encontrado." });
            for (int i = 0; i < rd.FieldCount; i++) row[ToCamel(rd.GetName(i))] = rd.IsDBNull(i) ? null : rd.GetValue(i);
        }
        var features = await ReadFeatures(c, id, ct);
        return Ok(new { tenant = row, modules = features });
    }

    [HttpPost("tenants/{id:guid}/status")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] StatusReq req, CancellationToken ct)
    {
        var status = (req.Status ?? "").ToUpperInvariant();
        if (status is not ("TRIAL" or "ACTIVE" or "SUSPENDED" or "CANCELLED"))
            return BadRequest(new { message = "Estado inválido. Use TRIAL, ACTIVE, SUSPENDED o CANCELLED." });
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.tenants SET status=@s, updated_at=now() WHERE id=@id", c);
        cmd.Parameters.AddWithValue("@s", status); cmd.Parameters.AddWithValue("@id", id);
        var n = await cmd.ExecuteNonQueryAsync(ct);
        return n == 0 ? NotFound() : Ok(new { id, status });
    }

    [HttpGet("tenants/{id:guid}/features")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> Features(Guid id, CancellationToken ct)
        => Ok(await ReadFeatures(await db.OpenAsync(ct, platform: true), id, ct));

    /// <summary>Activa o desactiva un módulo para un tenant (soporte).</summary>
    [HttpPut("tenants/{id:guid}/features/{code}")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> SetFeature(Guid id, string code, [FromBody] FeatureReq req, CancellationToken ct)
    {
        code = code.ToLowerInvariant();
        if (!Modules.Any(m => m.Code == code)) return BadRequest(new { message = $"Módulo desconocido: {code}." });
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.tenant_features(tenant_id, feature_code, enabled, config, updated_at)
            VALUES(@t, @f, @e, '{}'::jsonb, now())
            ON CONFLICT (tenant_id, feature_code) DO UPDATE SET enabled=EXCLUDED.enabled, updated_at=now()", c);
        cmd.Parameters.AddWithValue("@t", id); cmd.Parameters.AddWithValue("@f", code); cmd.Parameters.AddWithValue("@e", req.Enabled);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { tenantId = id, featureCode = code, enabled = req.Enabled });
    }

    /// <summary>Eventos de autenticación globales (soporte/auditoría de plataforma).</summary>
    [HttpGet("audit")]
    [RequirePermission("platform.audit.view")]
    public async Task<IActionResult> Audit([FromQuery] int limit = 100, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        await using var c = await db.OpenAsync(ct, platform: true);
        // Los alias ya salen en camelCase para que el panel de plataforma los lea
        // directamente (la tabla usa snake_case y el camelizado solo toca la 1.ª letra).
        await using var cmd = new NpgsqlCommand(@"SELECT e.id, e.tenant_id AS ""tenantId"", t.commercial_name AS ""commercialName"", u.email, e.event_type AS ""eventType"", e.success, e.occurred_at AS ""createdAt""
            FROM sigm.authentication_events e
            LEFT JOIN sigm.tenants t ON t.id = e.tenant_id
            LEFT JOIN sigm.users u ON u.id = e.user_id
            ORDER BY e.occurred_at DESC LIMIT @l", c);
        cmd.Parameters.AddWithValue("@l", limit);
        var list = new List<Dictionary<string, object?>>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < rd.FieldCount; i++) d[ToCamel(rd.GetName(i))] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            list.Add(d);
        }
        return Ok(list);
    }

    [HttpGet("stats")]
    [RequirePermission("platform.tenants.manage")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT
              (SELECT count(*) FROM sigm.tenants) AS tenants,
              (SELECT count(*) FROM sigm.tenants WHERE status='ACTIVE') AS active_tenants,
              (SELECT count(*) FROM sigm.tenants WHERE status='SUSPENDED') AS suspended_tenants,
              (SELECT count(*) FROM sigm.users) AS users,
              (SELECT count(*) FROM sigm.patients) AS patients,
              (SELECT count(*) FROM sigm.appointments) AS appointments", c);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return Ok(new { });
        return Ok(new
        {
            tenants = rd.GetInt64(0), activeTenants = rd.GetInt64(1), suspendedTenants = rd.GetInt64(2),
            users = rd.GetInt64(3), patients = rd.GetInt64(4), appointments = rd.GetInt64(5)
        });
    }

    private static async Task<List<object>> ReadFeatures(NpgsqlConnection c, Guid tid, CancellationToken ct)
    {
        var result = new List<object>();
        await using var cmd = new NpgsqlCommand("SELECT feature_code, enabled, updated_at FROM sigm.tenant_features WHERE tenant_id=@t ORDER BY feature_code", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var rows = new Dictionary<string, (bool Enabled, DateTimeOffset At)>();
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
            while (await rd.ReadAsync(ct))
                rows[rd.GetString(0)] = (rd.GetBoolean(1), rd.GetFieldValue<DateTimeOffset>(2));
        // Un módulo sin fila está habilitado por defecto; se reporta el estado efectivo de todo el catálogo.
        foreach (var m in Modules)
        {
            var found = rows.TryGetValue(m.Code, out var r);
            result.Add(new { code = m.Code, name = m.Name, description = m.Description, enabled = !found || r.Enabled, updatedAt = found ? (DateTimeOffset?)r.At : null, configurable = true });
        }
        return result;
    }

    private static string ToCamel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    public record StatusReq(string? Status);
    public record FeatureReq(bool Enabled);
}
