using System.Buffers.Binary;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using SIGM.Api.Contracts;
using SIGM.Api.Services;
using SIGM.Api.Validation;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(TenantConnectionFactory db, TokenService tokens, UserSessionValidator sessions,
    EmailSender mail, IConfiguration cfg, IWebHostEnvironment env, ILogger<AuthController> logger) : ControllerBase
{
    private const string MfaAad = "sigm-mfa";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        await using var c = await db.OpenAsync(ct, platform: true);

        Guid userId; string? fullName; string? passwordHash; bool isActive; bool isPlatformAdmin; int tokenVersion;
        await using (var cmd = new NpgsqlCommand("SELECT id,full_name,password_hash,is_active,COALESCE(is_platform_admin,false),COALESCE(token_version,0) FROM sigm.users WHERE email=@e LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@e", email);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return Unauthorized(new { message = "Credenciales inválidas." });
            userId = rd.GetGuid(0); fullName = rd.IsDBNull(1) ? null : rd.GetString(1); passwordHash = rd.IsDBNull(2) ? null : rd.GetString(2); isActive = rd.GetBoolean(3); isPlatformAdmin = rd.GetBoolean(4); tokenVersion = rd.GetInt32(5);
        }
        if (!isActive || !PasswordService.Verify(req.Password, passwordHash))
        {
            await LogAuthEvent(c, null, userId, "LOGIN_FAILED", false, ct);
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        // MFA: si está habilitado, el código se exige en el mismo login (antes se emitía
        // un token intermedio que ningún endpoint podía canjear y que servía para
        // autenticarse sin segundo factor).
        bool mfaEnabled = false; string? storedSecret = null; DateTimeOffset? mfaLockedUntil = null;
        await using (var mfaCmd = new NpgsqlCommand("SELECT COALESCE(mfa_enabled,false), mfa_secret_encrypted, mfa_locked_until FROM sigm.users WHERE id=@u", c))
        {
            mfaCmd.Parameters.AddWithValue("@u", userId);
            await using var mfaRd = await mfaCmd.ExecuteReaderAsync(ct);
            if (await mfaRd.ReadAsync(ct))
            {
                mfaEnabled = mfaRd.GetBoolean(0);
                storedSecret = mfaRd.IsDBNull(1) ? null : mfaRd.GetString(1);
                mfaLockedUntil = mfaRd.IsDBNull(2) ? null : mfaRd.GetFieldValue<DateTimeOffset>(2);
            }
        }
        if (mfaEnabled)
        {
            if (mfaLockedUntil is DateTimeOffset locked && locked > DateTimeOffset.UtcNow)
            {
                await LogAuthEvent(c, null, userId, "LOGIN_MFA_LOCKED", false, ct);
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    message = $"El segundo factor está bloqueado por intentos fallidos. Intente de nuevo después de {locked.LocalDateTime:HH:mm}.",
                    mfaRequired = true,
                    lockedUntil = locked
                });
            }
            if (string.IsNullOrWhiteSpace(req.MfaCode))
            {
                await LogAuthEvent(c, null, userId, "LOGIN_MFA_REQUIRED", false, ct);
                return Unauthorized(new { message = "Código de verificación requerido.", mfaRequired = true });
            }
            var secret = DecryptSecret(storedSecret);
            var totpOk = secret is not null && VerifyTotpCode(secret, req.MfaCode);
            if (!totpOk)
            {
                // Código de recuperación: un solo uso. Se consume solo si coincide.
                var consumed = await TryConsumeRecoveryCodeAsync(c, userId, req.MfaCode, ct);
                if (!consumed)
                {
                    var until = await RegisterMfaFailureAsync(c, userId, ct);
                    await LogAuthEvent(c, null, userId, "LOGIN_MFA_FAILED", false, ct);
                    return Unauthorized(new
                    {
                        message = until is null
                            ? "Código de verificación inválido."
                            : "Código inválido. El segundo factor quedó bloqueado temporalmente por demasiados intentos.",
                        mfaRequired = true,
                        lockedUntil = until
                    });
                }
                await ResetMfaFailuresAsync(c, userId, ct);
                await LogAuthEvent(c, null, userId, "LOGIN_MFA_RECOVERY_USED", true, ct);
            }
            else
            {
                await ResetMfaFailuresAsync(c, userId, ct);
            }
        }

        // La organización puede llegar como UUID o como código legible (p. ej. CLINIC-DEMO):
        // antes, cualquier texto que no fuese GUID rompía el binding y devolvía un 400 opaco.
        var (requestedTenant, tenantError) = await ResolveTenantAsync(req.TenantId, c, ct);
        if (tenantError is not null) return BadRequest(new { message = tenantError });

        Guid? tenantId = requestedTenant;
        // Superadministrador de plataforma: no requiere membresía de tenant.
        // Puede pasar req.TenantId para "ver como" un tenant (soporte), lo que
        // carga también los permisos de ese tenant.
        if (isPlatformAdmin)
        {
            var platformPerms = new List<string>();
            await using (var cmd = new NpgsqlCommand(@"SELECT DISTINCT p.code
                FROM sigm.role_permissions rp
                JOIN sigm.roles r ON r.id = rp.role_id AND r.tenant_id IS NULL
                JOIN sigm.permissions p ON p.id = rp.permission_id
                WHERE r.code = 'PLATFORM_ADMIN'", c))
            {
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) platformPerms.Add(rd.GetString(0));
            }
            Guid? impTid = null; string? impProduct = null, impCode = null, impName = null;
            if (tenantId is Guid wanted)
            {
                await using (var cmd = new NpgsqlCommand("SELECT product_type, tenant_code, commercial_name FROM sigm.tenants WHERE id=@t", c))
                {
                    cmd.Parameters.AddWithValue("@t", wanted);
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    if (!await rd.ReadAsync(ct)) return Forbid();
                    impProduct = rd.IsDBNull(0) ? null : rd.GetString(0);
                    impCode = rd.IsDBNull(1) ? null : rd.GetString(1);
                    impName = rd.IsDBNull(2) ? null : rd.GetString(2);
                    impTid = wanted;
                }
                await using (var cmd = new NpgsqlCommand(@"SELECT DISTINCT p.code
                    FROM sigm.user_roles ur
                    JOIN sigm.role_permissions rp ON rp.role_id=ur.role_id
                    JOIN sigm.permissions p ON p.id=rp.permission_id
                    WHERE ur.tenant_id=@t AND ur.user_id=@u", c))
                {
                    cmd.Parameters.AddWithValue("@t", wanted); cmd.Parameters.AddWithValue("@u", userId);
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    while (await rd.ReadAsync(ct)) platformPerms.Add(rd.GetString(0));
                }
            }
            var (ptoken, pexp) = tokens.Create(userId, email, fullName ?? email, impTid, impProduct, platformPerms, tokenVersion, isPlatformAdmin: true);
            await LogAuthEvent(c, impTid, userId, "PLATFORM_LOGIN_SUCCESS", true, ct);
            return Ok(new LoginResponse(ptoken, pexp, userId, impTid, fullName ?? email, platformPerms.Distinct().ToArray(), impCode, impName));
        }

        var memberships = new List<Guid>();
        await using (var cmd = new NpgsqlCommand("SELECT tenant_id FROM sigm.user_tenants WHERE user_id=@u AND status='ACTIVE'", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) memberships.Add(rd.GetGuid(0));
        }

        if (tenantId is Guid requested)
        {
            if (!memberships.Contains(requested)) return Forbid();
        }
        else if (memberships.Count == 1) tenantId = memberships[0];
        else if (memberships.Count > 1)
        {
            return Ok(new { requiresTenantSelection = true, tenants = memberships, userId, fullName });
        }

        string? productType = null, tenantCode = null, tenantName = null;
        var permissions = new List<string>();
        if (tenantId is Guid tid)
        {
            await using (var cmd = new NpgsqlCommand("SELECT product_type, tenant_code, commercial_name FROM sigm.tenants WHERE id=@t", c))
            {
                cmd.Parameters.AddWithValue("@t", tid);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                if (await rd.ReadAsync(ct))
                {
                    productType = rd.IsDBNull(0) ? null : rd.GetString(0);
                    tenantCode = rd.IsDBNull(1) ? null : rd.GetString(1);
                    tenantName = rd.IsDBNull(2) ? null : rd.GetString(2);
                }
            }
            // Defensa en profundidad: aunque exista una asignación de rol de plataforma
            // filtrada por error, un login de tenant nunca emite permisos platform.*.
            await using (var cmd = new NpgsqlCommand(@"SELECT DISTINCT p.code
                FROM sigm.user_roles ur
                JOIN sigm.roles r ON r.id=ur.role_id
                JOIN sigm.role_permissions rp ON rp.role_id=ur.role_id
                JOIN sigm.permissions p ON p.id=rp.permission_id
                WHERE ur.tenant_id=@t AND ur.user_id=@u
                  AND left(r.code, 9) <> 'PLATFORM_'
                  AND p.code NOT LIKE 'platform.%'", c))
            {
                cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@u", userId);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) permissions.Add(rd.GetString(0));
            }
        }

        var (token, expiresAt) = tokens.Create(userId, email, fullName ?? email, tenantId, productType, permissions, tokenVersion);
        await LogAuthEvent(c, tenantId, userId, "LOGIN_SUCCESS", true, ct);
        return Ok(new LoginResponse(token, expiresAt, userId, tenantId, fullName ?? email, permissions.ToArray(), tenantCode, tenantName));
    }

    /// <summary>
    /// Resuelve la organización indicada en el login. Acepta el UUID o el código
    /// (tenant_code, p. ej. CLINIC-DEMO) para que el usuario no tenga que memorizar
    /// un identificador. Devuelve null si no se indicó organización.
    /// </summary>
    private static async Task<(Guid? Id, string? Error)> ResolveTenantAsync(string? raw, NpgsqlConnection c, CancellationToken ct)
    {
        var (id, code) = TenantReference.Parse(raw);
        if (id is Guid guid) return (guid, null);
        if (code is null) return (null, null);

        await using var cmd = new NpgsqlCommand("SELECT id FROM sigm.tenants WHERE upper(tenant_code)=@v LIMIT 1", c);
        cmd.Parameters.AddWithValue("@v", code);
        if (await cmd.ExecuteScalarAsync(ct) is Guid found) return (found, null);

        return (null, "La organización indicada no existe. Escriba su código (por ejemplo CLINIC-DEMO) o su identificador.");
    }

    /// <summary>Envía un aviso de seguridad sin bloquear ni hacer fallar la operación.</summary>
    private async Task NotifyAsync(string to, string subject, string body, CancellationToken ct)
    {
        try { await mail.SendAsync(to, subject, body, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo enviar el aviso de seguridad a {Email}", to); }
    }

    /// <summary>
    /// Suma un intento fallido de MFA. A los 5 intentos bloquea el segundo factor
    /// durante 15 minutos; el bloqueo se aplica aunque la contraseña sea correcta.
    /// </summary>
    private static async Task<DateTimeOffset?> RegisterMfaFailureAsync(NpgsqlConnection c, Guid userId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"UPDATE sigm.users
            SET failed_mfa_attempts = COALESCE(failed_mfa_attempts,0) + 1,
                mfa_locked_until = CASE WHEN COALESCE(failed_mfa_attempts,0) + 1 >= 5
                                        THEN now() + interval '15 minutes' ELSE mfa_locked_until END
            WHERE id=@u
            RETURNING mfa_locked_until", c);
        cmd.Parameters.AddWithValue("@u", userId);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value switch
        {
            DateTimeOffset until => until,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => null
        };
    }

    private static async Task ResetMfaFailuresAsync(NpgsqlConnection c, Guid userId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("UPDATE sigm.users SET failed_mfa_attempts=0, mfa_locked_until=NULL WHERE id=@u", c);
        cmd.Parameters.AddWithValue("@u", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task LogAuthEvent(NpgsqlConnection c, Guid? tenantId, Guid? userId, string eventType, bool success, CancellationToken ct)
    {
        try
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.authentication_events(id,tenant_id,user_id,event_type,success,detail)
                VALUES(gen_random_uuid(),@t,@u,@e,@s,'{}'::jsonb)", c);
            cmd.Parameters.AddWithValue("@t", (object?)tenantId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", eventType);
            cmd.Parameters.AddWithValue("@s", success);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { /* non-blocking */ }
    }

    /// <summary>
    /// Genera un secreto TOTP nuevo y lo deja guardado (cifrado) pero SIN habilitar.
    /// La habilitación efectiva ocurre en verify-mfa, cuando el usuario demuestra
    /// que su aplicación autenticadora genera códigos válidos.
    /// </summary>
    [HttpPost("setup-mfa")]
    [Authorize]
    [EnableRateLimiting("mfa")]
    public async Task<ActionResult> SetupMfa(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });

        await using var c = await db.OpenAsync(ct, platform: true);
        string email;
        await using (var cmd = new NpgsqlCommand("SELECT email FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            var res = await cmd.ExecuteScalarAsync(ct);
            if (res is not string mail) return Unauthorized(new { message = "Usuario no encontrado." });
            email = mail;
        }

        var secret = GenerateMfaSecret();
        await using (var cmd = new NpgsqlCommand("UPDATE sigm.users SET mfa_secret_encrypted=@s, mfa_enabled=false WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@s", EncryptSecret(secret));
            cmd.Parameters.AddWithValue("@u", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var label = Uri.EscapeDataString($"SIGM Cloud:{email}");
        var provisioningUrl = $"otpauth://totp/{label}?secret={secret}&issuer=SIGM%20Cloud&algorithm=SHA1&digits=6&period=30";
        return Ok(new { secret, provisioningUrl });
    }

    /// <summary>
    /// Valida el primer código y habilita MFA para el usuario autenticado.
    /// </summary>
    [HttpPost("verify-mfa")]
    [Authorize]
    [EnableRateLimiting("mfa")]
    public async Task<ActionResult> VerifyMfa([FromBody] VerifyMfaRequest req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });

        await using var c = await db.OpenAsync(ct, platform: true);
        string? storedSecret = null; DateTimeOffset? lockedUntil = null;
        await using (var cmd = new NpgsqlCommand("SELECT mfa_secret_encrypted, mfa_locked_until FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return Unauthorized(new { message = "Usuario no encontrado." });
            storedSecret = rd.IsDBNull(0) ? null : rd.GetString(0);
            lockedUntil = rd.IsDBNull(1) ? null : rd.GetFieldValue<DateTimeOffset>(1);
        }

        if (lockedUntil is DateTimeOffset locked && locked > DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status423Locked, new { message = "El segundo factor está bloqueado temporalmente por intentos fallidos.", lockedUntil = locked });

        var secret = DecryptSecret(storedSecret);
        if (secret is null)
            return BadRequest(new { message = "MFA no iniciado. Ejecuta setup-mfa para generar un secreto." });

        if (!VerifyTotpCode(secret, req.Code))
        {
            var until = await RegisterMfaFailureAsync(c, userId, ct);
            await LogAuthEvent(c, null, userId, "MFA_VERIFY_FAILED", false, ct);
            return Unauthorized(new { message = "Código MFA inválido.", lockedUntil = until });
        }

        var recoveryCodes = GenerateRecoveryCodes();
        await using (var cmd = new NpgsqlCommand("UPDATE sigm.users SET mfa_enabled=true, mfa_recovery_codes=@codes::jsonb, failed_mfa_attempts=0, mfa_locked_until=NULL WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@codes", JsonSerializer.Serialize(recoveryCodes.Select(HashRecoveryCode)));
            cmd.Parameters.AddWithValue("@u", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await LogAuthEvent(c, null, userId, "MFA_VERIFY_SUCCESS", true, ct);
        return Ok(new { mfaEnabled = true, recoveryCodes });
    }

    /// <summary>Estado de MFA del usuario autenticado (para la pantalla de seguridad).</summary>
    [HttpGet("mfa-status")]
    [Authorize]
    public async Task<ActionResult> MfaStatus(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT COALESCE(mfa_enabled,false), mfa_secret_encrypted IS NOT NULL,
                COALESCE(jsonb_array_length(mfa_recovery_codes),0), email FROM sigm.users WHERE id=@u", c);
        cmd.Parameters.AddWithValue("@u", userId);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return NotFound(new { message = "Usuario no encontrado." });
        return Ok(new
        {
            mfaEnabled = rd.GetBoolean(0),
            configured = rd.GetBoolean(1),
            recoveryCodesRemaining = rd.GetInt32(2),
            email = rd.GetString(3)
        });
    }

    /// <summary>
    /// Desactiva MFA. Exige contraseña y un código TOTP vigente para evitar que
    /// una sesión robada pueda desactivar el segundo factor.
    /// </summary>
    [HttpPost("disable-mfa")]
    [Authorize]
    [EnableRateLimiting("mfa")]
    public async Task<ActionResult> DisableMfa([FromBody] DisableMfaRequest req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });
        await using var c = await db.OpenAsync(ct, platform: true);

        string? passwordHash; string? storedSecret; bool enabled; string email;
        await using (var cmd = new NpgsqlCommand("SELECT password_hash, mfa_secret_encrypted, COALESCE(mfa_enabled,false), email FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return Unauthorized(new { message = "Usuario no encontrado." });
            passwordHash = rd.IsDBNull(0) ? null : rd.GetString(0);
            storedSecret = rd.IsDBNull(1) ? null : rd.GetString(1);
            enabled = rd.GetBoolean(2);
            email = rd.GetString(3);
        }

        if (!enabled) return BadRequest(new { message = "La verificación en dos pasos no está activa." });
        if (!PasswordService.Verify(req.Password ?? string.Empty, passwordHash))
        {
            await LogAuthEvent(c, null, userId, "MFA_DISABLE_FAILED", false, ct);
            return Unauthorized(new { message = "Contraseña incorrecta." });
        }
        var secret = DecryptSecret(storedSecret);
        if (secret is null || !VerifyTotpCode(secret, req.Code ?? string.Empty))
        {
            await LogAuthEvent(c, null, userId, "MFA_DISABLE_FAILED", false, ct);
            return Unauthorized(new { message = "Código de verificación inválido." });
        }

        await using (var cmd = new NpgsqlCommand(@"UPDATE sigm.users
                SET mfa_enabled=false, mfa_secret_encrypted=NULL, mfa_recovery_codes=NULL,
                    token_version=COALESCE(token_version,0)+1
                WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        sessions.Invalidate(userId);
        await LogAuthEvent(c, null, userId, "MFA_DISABLED", true, ct);
        await NotifyAsync(email, "Verificación en dos pasos desactivada — SIGM Cloud",
            "Se desactivó la verificación en dos pasos de su cuenta. Si no reconoce este cambio, restablezca su contraseña y contacte a su institución.", ct);
        return Ok(new { mfaEnabled = false, message = "Verificación en dos pasos desactivada. Vuelva a iniciar sesión." });
    }


    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

    /// <summary>Cambio de contraseña del usuario autenticado. Revoca las demás sesiones.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("mfa")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest(new { message = "La nueva contraseña debe tener al menos 8 caracteres." });

        await using var c = await db.OpenAsync(ct, platform: true);
        string? hash; string email;
        await using (var cmd = new NpgsqlCommand("SELECT password_hash, email FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return Unauthorized(new { message = "Usuario no encontrado." });
            hash = rd.IsDBNull(0) ? null : rd.GetString(0);
            email = rd.GetString(1);
        }
        if (!PasswordService.Verify(req.CurrentPassword ?? string.Empty, hash))
        {
            await LogAuthEvent(c, null, userId, "PASSWORD_CHANGE_FAILED", false, ct);
            return Unauthorized(new { message = "La contraseña actual es incorrecta." });
        }

        await using (var cmd = new NpgsqlCommand(@"UPDATE sigm.users SET password_hash=@h,
                token_version=COALESCE(token_version,0)+1 WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@h", PasswordService.Hash(req.NewPassword));
            cmd.Parameters.AddWithValue("@u", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        sessions.Invalidate(userId);
        await LogAuthEvent(c, null, userId, "PASSWORD_CHANGED", true, ct);
        await NotifyAsync(email, "Contraseña actualizada — SIGM Cloud",
            "Su contraseña fue actualizada y las sesiones anteriores se cerraron. Si no realizó este cambio, solicite un restablecimiento de inmediato.", ct);
        return Ok(new { success = true, message = "Contraseña actualizada. Vuelva a iniciar sesión." });
    }

    public record ForgotPasswordRequest(string? Email);

    /// <summary>
    /// Solicita un enlace de restablecimiento. Responde siempre lo mismo para no
    /// revelar si el correo existe. En desarrollo devuelve el token para probar
    /// sin proveedor SMTP.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0) return BadRequest(new { message = "Indique su correo electrónico." });

        await using var c = await db.OpenAsync(ct, platform: true);
        Guid? userId = null; string? fullName = null; bool active = false;
        await using (var cmd = new NpgsqlCommand("SELECT id,full_name,is_active FROM sigm.users WHERE email=@e LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@e", email);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct)) { userId = rd.GetGuid(0); fullName = rd.IsDBNull(1) ? null : rd.GetString(1); active = rd.GetBoolean(2); }
        }

        string? devToken = null;
        if (userId is Guid uid && active)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.password_reset_tokens(id,user_id,token_hash,expires_at,requested_ip)
                    VALUES(gen_random_uuid(),@u,@h,now()+interval '30 minutes',CAST(@ip AS inet))", c))
            {
                cmd.Parameters.AddWithValue("@u", uid);
                cmd.Parameters.AddWithValue("@h", hash);
                cmd.Parameters.AddWithValue("@ip", (object?)HttpContext.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await LogAuthEvent(c, null, uid, "PASSWORD_RESET_REQUESTED", true, ct);

            var baseUrl = (cfg["App:PublicUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(raw)}";
            var sent = await mail.SendAsync(email, "Restablecer contraseña — SIGM Cloud",
                $"Hola {fullName ?? ""},\n\nPara restablecer su contraseña abra este enlace (válido 30 minutos):\n{link}\n\nSi no solicitó el cambio, ignore este correo.",
                ct);
            if (!sent && env.IsDevelopment()) devToken = raw;
            logger.LogInformation("Enlace de restablecimiento generado para {Email}; enviado={Sent}", email, sent);
        }

        return Ok(new
        {
            message = "Si el correo existe, enviamos un enlace para restablecer la contraseña.",
            devToken
        });
    }

    public record ResetPasswordRequest(string? Token, string? NewPassword);

    /// <summary>Restablece la contraseña con un token de un solo uso y revoca sesiones.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token)) return BadRequest(new { message = "Token requerido." });
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest(new { message = "La nueva contraseña debe tener al menos 8 caracteres." });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(req.Token.Trim()))).ToLowerInvariant();
        await using var c = await db.OpenAsync(ct, platform: true);
        Guid? userId = null; string? userEmail = null;
        await using (var cmd = new NpgsqlCommand(@"SELECT t.user_id, u.email FROM sigm.password_reset_tokens t
                JOIN sigm.users u ON u.id=t.user_id
                WHERE t.token_hash=@h AND t.used_at IS NULL AND t.expires_at > now() LIMIT 1", c))
        {
            cmd.Parameters.AddWithValue("@h", hash);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct)) { userId = rd.GetGuid(0); userEmail = rd.IsDBNull(1) ? null : rd.GetString(1); }
        }
        if (userId is not Guid uid)
        {
            await LogAuthEvent(c, null, null, "PASSWORD_RESET_FAILED", false, ct);
            return BadRequest(new { message = "El enlace no es válido o ya expiró. Solicite uno nuevo." });
        }

        await using (var tx = await c.BeginTransactionAsync(ct))
        {
            await using (var cmd = new NpgsqlCommand(@"UPDATE sigm.users SET password_hash=@p,
                    token_version=COALESCE(token_version,0)+1 WHERE id=@u", c, tx))
            {
                cmd.Parameters.AddWithValue("@p", PasswordService.Hash(req.NewPassword));
                cmd.Parameters.AddWithValue("@u", uid);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await using (var cmd = new NpgsqlCommand("UPDATE sigm.password_reset_tokens SET used_at=now() WHERE token_hash=@h", c, tx))
            {
                cmd.Parameters.AddWithValue("@h", hash);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        sessions.Invalidate(uid);
        await LogAuthEvent(c, null, uid, "PASSWORD_RESET_SUCCESS", true, ct);
        if (!string.IsNullOrWhiteSpace(userEmail))
            await NotifyAsync(userEmail!, "Contraseña restablecida — SIGM Cloud",
                "Su contraseña fue restablecida y las sesiones anteriores se cerraron. Si no realizó este cambio, contacte de inmediato a su institución.", ct);
        return Ok(new { success = true, message = "Contraseña restablecida. Inicie sesión con la nueva." });
    }

    /// <summary>Regenera los códigos de recuperación MFA (requiere contraseña actual).</summary>
    [HttpPost("recovery-codes")]
    [Authorize]
    [EnableRateLimiting("mfa")]
    public async Task<ActionResult> RegenerateRecoveryCodes([FromBody] DisableMfaRequest req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Usuario no identificado." });
        await using var c = await db.OpenAsync(ct, platform: true);
        string? hash; bool enabled;
        await using (var cmd = new NpgsqlCommand("SELECT password_hash, COALESCE(mfa_enabled,false) FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return NotFound(new { message = "Usuario no encontrado." });
            hash = rd.IsDBNull(0) ? null : rd.GetString(0);
            enabled = rd.GetBoolean(1);
        }
        if (!enabled) return BadRequest(new { message = "Active primero la verificación en dos pasos." });
        if (!PasswordService.Verify(req.Password ?? string.Empty, hash))
            return Unauthorized(new { message = "Contraseña incorrecta." });

        var codes = GenerateRecoveryCodes();
        await using (var cmd = new NpgsqlCommand("UPDATE sigm.users SET mfa_recovery_codes=@codes::jsonb WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@codes", JsonSerializer.Serialize(codes.Select(HashRecoveryCode)));
            cmd.Parameters.AddWithValue("@u", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await LogAuthEvent(c, null, userId, "MFA_RECOVERY_CODES_REGENERATED", true, ct);
        return Ok(new { recoveryCodes = codes });
    }

    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static List<string> GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var chars = new char[8];
            for (var j = 0; j < chars.Length; j++)
                chars[j] = RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
            codes.Add($"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}");
        }
        return codes;
    }

    private static string HashRecoveryCode(string code)
    {
        var normalized = code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static async Task<bool> TryConsumeRecoveryCodeAsync(NpgsqlConnection c, Guid userId, string input, CancellationToken ct)
    {
        string? stored;
        await using (var cmd = new NpgsqlCommand("SELECT mfa_recovery_codes::text FROM sigm.users WHERE id=@u", c))
        {
            cmd.Parameters.AddWithValue("@u", userId);
            stored = await cmd.ExecuteScalarAsync(ct) as string;
        }
        if (string.IsNullOrWhiteSpace(stored)) return false;

        List<string>? hashes;
        try { hashes = JsonSerializer.Deserialize<List<string>>(stored); } catch { return false; }
        if (hashes is null || hashes.Count == 0) return false;

        var candidate = HashRecoveryCode(input);
        var candidateBytes = Encoding.ASCII.GetBytes(candidate);
        var index = hashes.FindIndex(h => CryptographicOperations.FixedTimeEquals(candidateBytes, Encoding.ASCII.GetBytes(h)));
        if (index < 0) return false;

        hashes.RemoveAt(index);
        await using var update = new NpgsqlCommand("UPDATE sigm.users SET mfa_recovery_codes=@codes::jsonb WHERE id=@u", c);
        update.Parameters.AddWithValue("@codes", JsonSerializer.Serialize(hashes));
        update.Parameters.AddWithValue("@u", userId);
        await update.ExecuteNonQueryAsync(ct);
        return true;
    }

    private bool TryGetUserId(out Guid userId)
    {
        // ASP.NET Core / JwtBearer mapea 'sub' -> ClaimTypes.NameIdentifier y también lo
        // expone crudo como 'sub' y como el nombre xmlns de WS-Federation.
        userId = Guid.Empty;
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue("sub")
               ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
               ?? User.Identity?.Name;
        return Guid.TryParse(id, out userId);
    }

    /// <summary>Valida un código TOTP de 6 dígitos con tolerancia de ±1 paso (30 s).</summary>
    private static bool VerifyTotpCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = code.Trim().Replace(" ", "");
        if (normalized.Length != 6 || !normalized.All(char.IsAsciiDigit)) return false;

        byte[] key;
        try { key = FromBase32(base32Secret); } catch { return false; }
        if (key.Length == 0) return false;

        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var expected = Encoding.ASCII.GetBytes(normalized);
        for (var offset = -1L; offset <= 1L; offset++)
        {
            var candidate = Encoding.ASCII.GetBytes(GenerateTotp(key, step + offset));
            if (CryptographicOperations.FixedTimeEquals(candidate, expected)) return true;
        }
        return false;
    }

    private static string GenerateTotp(byte[] key, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        Span<byte> hash = stackalloc byte[20];
        using var hmac = new HMACSHA1(key);
        hmac.TryComputeHash(counter, hash, out _);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>Secreto aleatorio de 160 bits en base32 (compatible con apps autenticadoras).</summary>
    private static string GenerateMfaSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

    private string EncryptSecret(string secret)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(MfaKey(), 16);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(MfaAad));
        return $"v1.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(cipher)}.{Convert.ToBase64String(tag)}";
    }

    private string? DecryptSecret(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var parts = stored.Split('.');
        // Los secretos con el formato antiguo (base64 simple) no son recuperables:
        // el usuario debe volver a ejecutar setup-mfa.
        if (parts.Length != 4 || parts[0] != "v1") return null;
        try
        {
            var nonce = Convert.FromBase64String(parts[1]);
            var cipher = Convert.FromBase64String(parts[2]);
            var tag = Convert.FromBase64String(parts[3]);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(MfaKey(), 16);
            aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(MfaAad));
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }

    private byte[] MfaKey()
    {
        var material = cfg["Mfa:EncryptionKey"] is { Length: > 0 } configured ? configured : cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(material))
            throw new InvalidOperationException("Falta Mfa:EncryptionKey (o Jwt:Key) para cifrar los secretos MFA.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    private static string ToBase32(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0) sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] FromBase32(string input)
    {
        var clean = input.Trim().TrimEnd('=').Replace(" ", "").ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var ch in clean)
        {
            var value = Base32Alphabet.IndexOf(ch);
            if (value < 0) throw new FormatException("Secreto base32 inválido.");
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }
        return output.ToArray();
    }
}
