using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/tenant-config")]
[Authorize]
public class TenantConfigController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission("tenant.settings.read")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT setting_key, value, updated_at FROM sigm.tenant_settings WHERE tenant_id=@t ORDER BY setting_key", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { key = rd.GetString(0), value = rd.IsDBNull(1) ? null : rd.GetString(1), updatedAt = rd.GetFieldValue<DateTimeOffset>(2) });
        return Ok(list);
    }

    [HttpPut("settings/{key}")]
    [RequirePermission("tenant.settings.manage")]
    public async Task<IActionResult> UpsertSetting(string key, [FromBody] JsonElement value, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.tenant_settings(tenant_id,setting_key,value,updated_by,updated_at)
            VALUES(@t,@k,@v::jsonb,@u,now())
            ON CONFLICT (tenant_id,setting_key) DO UPDATE SET value=EXCLUDED.value, updated_by=EXCLUDED.updated_by, updated_at=now()", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value.GetRawText()); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("branding")]
    [RequirePermission("tenant.settings.read")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT tenant_id,logo_key,favicon_key,primary_color,secondary_color,accent_color,email_from_name,email_from_address,custom_css,updated_at FROM sigm.tenant_branding WHERE tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@t", tid);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return Ok(new { tenantId = tid });
        return Ok(new { tenantId = rd.GetGuid(0), logoKey = rd.IsDBNull(1)?null:rd.GetString(1), faviconKey = rd.IsDBNull(2)?null:rd.GetString(2), primaryColor = rd.IsDBNull(3)?null:rd.GetString(3), secondaryColor = rd.IsDBNull(4)?null:rd.GetString(4), accentColor = rd.IsDBNull(5)?null:rd.GetString(5), emailFromName = rd.IsDBNull(6)?null:rd.GetString(6), emailFromAddress = rd.IsDBNull(7)?null:rd.GetValue(7)?.ToString(), customCss = rd.IsDBNull(8)?null:rd.GetString(8), updatedAt = rd.GetFieldValue<DateTimeOffset>(9) });
    }

    [HttpPut("branding")]
    [RequirePermission("tenant.settings.manage")]
    public async Task<IActionResult> UpsertBranding([FromBody] BrandingReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.tenant_branding(tenant_id,logo_key,favicon_key,primary_color,secondary_color,accent_color,email_from_name,email_from_address,custom_css,updated_at)
            VALUES(@t,@logo,@fav,@p,@s,@a,@efn,@efa,@css,now())
            ON CONFLICT (tenant_id) DO UPDATE SET logo_key=EXCLUDED.logo_key,favicon_key=EXCLUDED.favicon_key,primary_color=EXCLUDED.primary_color,secondary_color=EXCLUDED.secondary_color,accent_color=EXCLUDED.accent_color,email_from_name=EXCLUDED.email_from_name,email_from_address=EXCLUDED.email_from_address,custom_css=EXCLUDED.custom_css,updated_at=now()", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@logo", (object?)req.LogoKey ?? DBNull.Value); cmd.Parameters.AddWithValue("@fav", (object?)req.FaviconKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p", (object?)req.PrimaryColor ?? DBNull.Value); cmd.Parameters.AddWithValue("@s", (object?)req.SecondaryColor ?? DBNull.Value); cmd.Parameters.AddWithValue("@a", (object?)req.AccentColor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@efn", (object?)req.EmailFromName ?? DBNull.Value); cmd.Parameters.AddWithValue("@efa", (object?)req.EmailFromAddress ?? DBNull.Value); cmd.Parameters.AddWithValue("@css", (object?)req.CustomCss ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("features")]
    [RequirePermission("tenant.settings.read")]
    public async Task<IActionResult> GetFeatures(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT feature_code, enabled, limit_value, config, updated_at FROM sigm.tenant_features WHERE tenant_id=@t ORDER BY feature_code", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { featureCode = rd.GetString(0), enabled = rd.GetBoolean(1), limitValue = rd.IsDBNull(2)?(int?)null:rd.GetInt32(2), config = rd.IsDBNull(3)?null:rd.GetString(3), updatedAt = rd.GetFieldValue<DateTimeOffset>(4) });
        return Ok(list);
    }

    [HttpPut("features/{featureCode}")]
    [RequirePermission("tenant.settings.manage")]
    public async Task<IActionResult> UpsertFeature(string featureCode, [FromBody] FeatureReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);

        // Un módulo bloqueado por soporte de plataforma no puede ser reactivado
        // desde la configuración del tenant (el flag viaja en config.platformManaged).
        await using (var guard = new NpgsqlCommand(@"SELECT config->>'platformManaged' FROM sigm.tenant_features
            WHERE tenant_id=@t AND feature_code=@f", c))
        {
            guard.Parameters.AddWithValue("@t", tid); guard.Parameters.AddWithValue("@f", featureCode);
            if (string.Equals(await guard.ExecuteScalarAsync(ct) as string, "true", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Este módulo está gestionado por el soporte de plataforma. Contacte con administración." });
        }

        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.tenant_features(tenant_id,feature_code,enabled,limit_value,config,updated_at)
            VALUES(@t,@f,@e,@l,COALESCE(@c::jsonb,'{}'::jsonb),now())
            ON CONFLICT (tenant_id,feature_code) DO UPDATE SET enabled=EXCLUDED.enabled, limit_value=EXCLUDED.limit_value, config=EXCLUDED.config, updated_at=now()", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@f", featureCode); cmd.Parameters.AddWithValue("@e", req.Enabled);
        cmd.Parameters.AddWithValue("@l", (object?)req.LimitValue ?? DBNull.Value); cmd.Parameters.AddWithValue("@c", (object?)req.Config ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    [HttpGet("domains")]
    [RequirePermission("tenant.settings.read")]
    public async Task<IActionResult> GetDomains(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,domain,is_primary,verified_at,created_at FROM sigm.tenant_domains WHERE tenant_id=@t ORDER BY is_primary DESC, domain", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var list = new List<object>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), domain = rd.GetValue(1)?.ToString(), isPrimary = rd.GetBoolean(2), verifiedAt = rd.IsDBNull(3)?(DateTimeOffset?)null:rd.GetFieldValue<DateTimeOffset>(3), createdAt = rd.GetFieldValue<DateTimeOffset>(4) });
        return Ok(list);
    }

    [HttpPost("domains")]
    [RequirePermission("tenant.settings.manage")]
    public async Task<IActionResult> AddDomain([FromBody] DomainReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO sigm.tenant_domains(id,tenant_id,domain,is_primary) VALUES(gen_random_uuid(),@t,@d,@p)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", req.Domain); cmd.Parameters.AddWithValue("@p", req.IsPrimary);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }

    /// <summary>Códigos de módulos desactivados para el tenant del usuario actual.
    /// Endpoint ligero para cualquier usuario autenticado: los portales ocultan
    /// en el menú los módulos apagados por el superadmin de plataforma.</summary>
    [HttpGet("modules")]
    [Authorize]
    public async Task<IActionResult> GetDisabledModules(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("SELECT feature_code FROM sigm.tenant_features WHERE tenant_id=@t AND enabled=false ORDER BY feature_code", c);
        cmd.Parameters.AddWithValue("@t", tid);
        var disabled = new List<string>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) disabled.Add(rd.GetString(0));
        return Ok(new { disabled });
    }

    /// <summary>Preferencia del panel de inicio del usuario actual (dashboard personalizable).</summary>
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid) return BadRequest();
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("SELECT value FROM sigm.user_preferences WHERE tenant_id=@t AND user_id=@u AND pref_key='dashboard'", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@u", uid);
        var res = await cmd.ExecuteScalarAsync(ct);
        if (res is null) return Ok(new { hidden = Array.Empty<string>() });
        try
        {
            // Antes se devolvía el JSON como string: los portales hacían JSON Parse
            // sobre una respuesta ya serializada y perdían las preferencias 'hidden'.
            using var doc = JsonDocument.Parse(res.ToString()!);
            return Ok(doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return Ok(new { hidden = Array.Empty<string>() });
        }
    }

    [HttpPut("dashboard")]
    [Authorize]
    public async Task<IActionResult> SaveDashboard([FromBody] DashboardReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid) return BadRequest();
        var json = System.Text.Json.JsonSerializer.Serialize(new { hidden = req.Hidden ?? [] });
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.user_preferences(tenant_id, user_id, pref_key, value, updated_at)
            VALUES(@t, @u, 'dashboard', @v::jsonb, now())
            ON CONFLICT (tenant_id, user_id, pref_key) DO UPDATE SET value=EXCLUDED.value, updated_at=now()", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@u", uid); cmd.Parameters.AddWithValue("@v", json);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { saved = true, hidden = req.Hidden });
    }

    public record DashboardReq(string[]? Hidden);

    public record BrandingReq(string? LogoKey, string? FaviconKey, string? PrimaryColor, string? SecondaryColor, string? AccentColor, string? EmailFromName, string? EmailFromAddress, string? CustomCss);
    public record FeatureReq(bool Enabled, int? LimitValue, string? Config);
    public record DomainReq(string Domain, bool IsPrimary);
}
