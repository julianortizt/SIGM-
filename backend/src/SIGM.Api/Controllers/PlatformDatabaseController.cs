using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

/// <summary>
/// Consola de base de datos del superadministrador de plataforma.
/// Flujo seguro: cargar script versionado -> validar en seco (siempre se revierte)
/// -> aplicar con respaldo previo, bloqueo asesor, transacción única y bitácora.
/// Nunca se acepta SQL sin confirmación explícita ni sin re-autenticación.
/// </summary>
[ApiController]
[Route("api/v1/platform/database")]
[Authorize]
[RequirePermission("platform.database.manage")]
public class PlatformDatabaseController(TenantConnectionFactory db, ITenantContext tenant, IConfiguration cfg,
    DatabaseBackupService backups, ILogger<PlatformDatabaseController> logger) : ControllerBase
{
    private const int MaxScriptBytes = 2 * 1024 * 1024;

    // ---------------------------------------------------------------------
    // Estado y respaldos
    // ---------------------------------------------------------------------
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT current_database(), current_setting('server_version'),
                pg_size_pretty(pg_database_size(current_database())),
                (SELECT count(*) FROM information_schema.tables WHERE table_schema='sigm'),
                (SELECT count(*) FROM sigm.db_scripts),
                (SELECT count(*) FROM sigm.database_backups),
                (SELECT max(created_at) FROM sigm.database_backups WHERE note LIKE 'Respaldo automático%')",
            c);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return Ok(new { });
        return Ok(new
        {
            database = rd.GetString(0),
            serverVersion = rd.IsDBNull(1) ? null : rd.GetString(1),
            size = rd.IsDBNull(2) ? null : rd.GetString(2),
            schemaTables = rd.GetInt64(3),
            scripts = rd.GetInt64(4),
            backups = rd.GetInt64(5),
            backupPath = BackupDirectory(),
            backupRetention = RetentionCount(),
            autoBackupEnabled = backups.AutoBackupEnabled,
            autoBackupIntervalHours = backups.AutoBackupIntervalHours,
            lastAutomaticBackupAt = rd.IsDBNull(6) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(6),
            pgDumpAvailable = DatabaseBackupService.FindPgDump() is not null,
            pgRestoreAvailable = DatabaseBackupService.FindPgRestore() is not null,
            maxScriptBytes = MaxScriptBytes
        });
    }

    [HttpGet("scripts")]
    public async Task<IActionResult> Scripts([FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT id,name,description,checksum,status,destructive,statements,length(sql_text),
                created_by,created_at,validated_at,applied_at,last_error,approved_by,approved_at
            FROM sigm.db_scripts ORDER BY created_at DESC LIMIT @n", c);
        cmd.Parameters.AddWithValue("@n", take);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new
            {
                id = rd.GetGuid(0),
                name = rd.GetString(1),
                description = rd.IsDBNull(2) ? null : rd.GetString(2),
                checksum = rd.GetString(3),
                status = rd.GetString(4),
                destructive = rd.GetBoolean(5),
                statements = rd.GetInt32(6),
                sizeBytes = rd.GetInt32(7),
                createdBy = rd.IsDBNull(8) ? (Guid?)null : rd.GetGuid(8),
                createdAt = rd.GetFieldValue<DateTimeOffset>(9),
                validatedAt = rd.IsDBNull(10) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(10),
                appliedAt = rd.IsDBNull(11) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(11),
                lastError = rd.IsDBNull(12) ? null : rd.GetString(12),
                approvedBy = rd.IsDBNull(13) ? (Guid?)null : rd.GetGuid(13),
                approvedAt = rd.IsDBNull(14) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(14)
            });
        return Ok(list);
    }

    [HttpGet("scripts/{id:guid}")]
    public async Task<IActionResult> Script(Guid id, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        object? script = null;
        await using (var cmd = new NpgsqlCommand(@"SELECT id,name,description,sql_text,checksum,status,destructive,statements,created_at,validated_at,applied_at,last_error,approved_by,approved_at
                FROM sigm.db_scripts WHERE id=@id", c))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
                script = new
                {
                    id = rd.GetGuid(0),
                    name = rd.GetString(1),
                    description = rd.IsDBNull(2) ? null : rd.GetString(2),
                    sqlText = rd.GetString(3),
                    checksum = rd.GetString(4),
                    status = rd.GetString(5),
                    destructive = rd.GetBoolean(6),
                    statements = rd.GetInt32(7),
                    createdAt = rd.GetFieldValue<DateTimeOffset>(8),
                    validatedAt = rd.IsDBNull(9) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(9),
                    appliedAt = rd.IsDBNull(10) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(10),
                    lastError = rd.IsDBNull(11) ? null : rd.GetString(11),
                    approvedBy = rd.IsDBNull(12) ? (Guid?)null : rd.GetGuid(12),
                    approvedAt = rd.IsDBNull(13) ? (DateTimeOffset?)null : rd.GetFieldValue<DateTimeOffset>(13)
                };
        }
        if (script is null) return NotFound(new { message = "Script no encontrado." });

        var runs = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"SELECT mode,success,message,statements,duration_ms,executed_by,client_ip,executed_at
                FROM sigm.db_script_runs WHERE script_id=@id ORDER BY executed_at DESC LIMIT 50", c))
        {
            cmd.Parameters.AddWithValue("@id", id);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                runs.Add(new
                {
                    mode = rd.GetString(0),
                    success = rd.GetBoolean(1),
                    message = rd.IsDBNull(2) ? null : rd.GetString(2),
                    statements = rd.GetInt32(3),
                    durationMs = rd.GetInt32(4),
                    executedBy = rd.IsDBNull(5) ? (Guid?)null : rd.GetGuid(5),
                    clientIp = rd.IsDBNull(6) ? null : rd.GetString(6),
                    executedAt = rd.GetFieldValue<DateTimeOffset>(7)
                });
        }
        return Ok(new { script, runs });
    }

    /// <summary>Carga un archivo .sql (multipart) o el texto del script.</summary>
    [HttpPost("scripts")]
    [RequestSizeLimit(4_194_304)]
    public async Task<IActionResult> Upload([FromForm] string? name, [FromForm] string? description, [FromForm] string? sql, IFormFile? file, CancellationToken ct)
    {
        string text;
        string scriptName;
        if (file is not null && file.Length > 0)
        {
            if (file.Length > MaxScriptBytes) return BadRequest(new { message = $"El script supera {MaxScriptBytes / 1024 / 1024} MB." });
            using var reader = new StreamReader(file.OpenReadStream());
            text = await reader.ReadToEndAsync(ct);
            scriptName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name!;
        }
        else
        {
            text = sql ?? string.Empty;
            scriptName = name ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { message = "Adjunte un archivo .sql o envíe el texto del script." });
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxScriptBytes) return BadRequest(new { message = $"El script supera {MaxScriptBytes / 1024 / 1024} MB." });
        if (string.IsNullOrWhiteSpace(scriptName)) return BadRequest(new { message = "Indique un nombre para el script." });

        var analysis = SqlScriptSafety.Analyze(text);
        var checksum = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        var id = Guid.NewGuid();

        await using var c = await db.OpenAsync(ct, platform: true);
        try
        {
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.db_scripts(id,name,description,sql_text,checksum,status,destructive,statements,created_by)
                    VALUES(@id,@name,@desc,@sql,@hash,'PENDING',@destructive,@statements,@u)", c);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", scriptName.Trim()[..Math.Min(160, scriptName.Trim().Length)]);
            cmd.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sql", text);
            cmd.Parameters.AddWithValue("@hash", checksum);
            cmd.Parameters.AddWithValue("@destructive", analysis.Destructive);
            cmd.Parameters.AddWithValue("@statements", analysis.Statements);
            cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { message = "Ese mismo contenido ya fue cargado (mismo checksum). Consulte el historial." });
        }

        return Ok(new { id, name = scriptName, checksum, analysis });
    }

    [HttpDelete("scripts/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("DELETE FROM sigm.db_scripts WHERE id=@id AND status <> 'APPLIED'", c);
        cmd.Parameters.AddWithValue("@id", id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
            return Conflict(new { message = "El script no existe o ya fue aplicado: su historial es inmutable." });
        return NoContent();
    }

    // ---------------------------------------------------------------------
    // Validación en seco (siempre se revierte)
    // ---------------------------------------------------------------------
    [HttpPost("scripts/{id:guid}/validate")]
    public async Task<IActionResult> Validate(Guid id, CancellationToken ct)
    {
        var (script, error) = await LoadScriptAsync(id, ct);
        if (error is not null) return error;
        var analysis = SqlScriptSafety.Analyze(script!.SqlText);
        if (!analysis.Allowed)
        {
            await RecordRunAsync(id, "VALIDATE", false, analysis.Reason, 0, 0, ct);
            return BadRequest(new { message = analysis.Reason, analysis });
        }

        var sw = Stopwatch.StartNew();
        await using var c = await OpenRawAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        long rows = 0;
        try
        {
            await SetTimeoutsAsync(c, tx, ct);
            await using var cmd = new NpgsqlCommand(script.SqlText, c, tx) { CommandTimeout = 60 };
            rows = await cmd.ExecuteNonQueryAsync(ct);
            await tx.RollbackAsync(ct);
        }
        catch (PostgresException ex)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { }
            var msg = $"Error SQLSTATE {ex.SqlState}: {ex.MessageText}";
            await RecordRunAsync(id, "VALIDATE", false, msg, 0, (int)sw.ElapsedMilliseconds, ct);
            await using (var up = new NpgsqlCommand("UPDATE sigm.db_scripts SET status='FAILED', last_error=@e WHERE id=@id", c))
            { up.Parameters.AddWithValue("@e", Trim(msg, 2000)); up.Parameters.AddWithValue("@id", id); await up.ExecuteNonQueryAsync(ct); }
            return BadRequest(new { message = msg, detail = ex.Detail, hint = ex.Hint, line = ex.Line });
        }
        catch (Exception ex)
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { }
            var msg = ex.Message;
            await RecordRunAsync(id, "VALIDATE", false, msg, 0, (int)sw.ElapsedMilliseconds, ct);
            return BadRequest(new { message = msg });
        }
        sw.Stop();

        await using (var up = new NpgsqlCommand("UPDATE sigm.db_scripts SET status=CASE WHEN status='APPLIED' THEN 'APPLIED' ELSE 'VALIDATED' END, validated_at=now(), last_error=NULL WHERE id=@id", c))
        { up.Parameters.AddWithValue("@id", id); await up.ExecuteNonQueryAsync(ct); }

        var message = $"Validación correcta en seco (revertida). Sentencias: {analysis.Statements}." + (rows >= 0 ? $" Filas afectadas: {rows}." : "");
        await RecordRunAsync(id, "VALIDATE", true, message, analysis.Statements, (int)sw.ElapsedMilliseconds, ct);
        return Ok(new { success = true, message, rowsAffected = rows, durationMs = sw.ElapsedMilliseconds, analysis });
    }

    // ---------------------------------------------------------------------
    // Aplicación real
    // ---------------------------------------------------------------------
    public record ApplyRequest(string? Confirmation, string? Password, bool SkipBackup, string? Note);

    public record ApproveRequest(string? Email, string? Password, string? Note);

    /// <summary>
    /// Doble aprobación: un script destructivo solo se aplica si otro administrador
    /// de plataforma lo aprueba con su propio correo y contraseña. Quien aprueba no
    /// puede ser el autor ni quien aplica (regla de dos personas).
    /// </summary>
    [HttpPost("scripts/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest req, CancellationToken ct)
    {
        var (script, error) = await LoadScriptAsync(id, ct);
        if (error is not null) return error;
        if (!script!.Destructive)
            return BadRequest(new { message = "Solo los scripts destructivos requieren aprobación." });
        if (script.Status == "APPLIED")
            return Conflict(new { message = "El script ya fue aplicado." });
        if (script.ApprovedAt is not null)
            return Conflict(new { message = "El script ya tiene una aprobación vigente." });

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Indique el correo y la contraseña del administrador que aprueba." });
        if (tenant.UserId is not Guid currentUserId) return Unauthorized(new { message = "Usuario no identificado." });

        Guid? approverId = null; string? approverHash = null;
        await using (var c = await db.OpenAsync(ct, platform: true))
        {
            await using var cmd = new NpgsqlCommand(@"SELECT u.id, u.password_hash
                FROM sigm.users u
                WHERE lower(u.email)=@e AND u.is_active AND COALESCE(u.is_platform_admin,false)
                  AND EXISTS (
                    SELECT 1 FROM sigm.role_permissions rp
                    JOIN sigm.permissions p ON p.id=rp.permission_id
                    JOIN sigm.roles r ON r.id=rp.role_id AND r.tenant_id IS NULL
                    WHERE r.code='PLATFORM_ADMIN' AND p.code='platform.database.manage')
                LIMIT 1", c);
            cmd.Parameters.AddWithValue("@e", email);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                approverId = rd.GetGuid(0);
                approverHash = rd.IsDBNull(1) ? null : rd.GetString(1);
            }
        }

        if (approverId is not Guid approver || !PasswordService.Verify(req.Password, approverHash))
        {
            logger.LogWarning("Aprobación rechazada del script {ScriptId}: credenciales del aprobador inválidas ({Email})", id, email);
            return Unauthorized(new { message = "El administrador aprobador no existe, no tiene permisos de plataforma o la contraseña es incorrecta." });
        }
        if (approver == currentUserId || approver == script.CreatedBy)
            return BadRequest(new { message = "La aprobación debe provenir de una persona distinta del autor y de quien aplica." });

        await using (var c = await db.OpenAsync(ct, platform: true))
        {
            await using var cmd = new NpgsqlCommand(@"UPDATE sigm.db_scripts
                SET approved_by=@a, approved_at=now(), approval_note=@n
                WHERE id=@id AND status <> 'APPLIED' AND approved_at IS NULL", c);
            cmd.Parameters.AddWithValue("@a", approver);
            cmd.Parameters.AddWithValue("@n", (object?)Trim(req.Note, 500) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);
            if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                return Conflict(new { message = "No se pudo registrar la aprobación: el script cambió de estado." });
        }

        logger.LogInformation("Script destructivo {ScriptId} aprobado por {ApproverId}", id, approver);
        return Ok(new { approvedBy = approver, approvedAt = DateTimeOffset.UtcNow, message = "Aprobación registrada. Ya puede aplicarse el script." });
    }

    [HttpPost("scripts/{id:guid}/apply")]
    public async Task<IActionResult> Apply(Guid id, [FromBody] ApplyRequest req, CancellationToken ct)
    {
        var (script, loadError) = await LoadScriptAsync(id, ct);
        if (loadError is not null) return loadError;
        if (script!.Status == "APPLIED") return Conflict(new { message = "El script ya fue aplicado. Cargue una nueva versión con otro contenido." });

        var analysis = SqlScriptSafety.Analyze(script.SqlText);
        if (!analysis.Allowed) return BadRequest(new { message = analysis.Reason, analysis });

        var expected = $"EJECUTAR {script.Name}";
        if (!string.Equals((req.Confirmation ?? "").Trim(), expected, StringComparison.Ordinal))
            return BadRequest(new { message = $"Confirmación incorrecta. Escriba exactamente: {expected}" });

        if (string.IsNullOrWhiteSpace(req.Password) || !await VerifyPasswordAsync(req.Password, ct))
            return Unauthorized(new { message = "Contraseña incorrecta. La aplicación de scripts exige re-autenticación." });

        if (req.SkipBackup && analysis.Destructive)
            return BadRequest(new { message = "Los scripts destructivos exigen respaldo previo: no se puede omitir." });

        if (analysis.Destructive)
        {
            if (script.ApprovedAt is null)
                return BadRequest(new
                {
                    message = "Este script es destructivo y requiere la aprobación de otro administrador de plataforma antes de aplicarse.",
                    requiresApproval = true
                });
            if (tenant.UserId is not Guid currentUserId)
                return Unauthorized(new { message = "Usuario no identificado." });
            if (script.ApprovedBy == currentUserId || script.ApprovedBy == script.CreatedBy)
                return BadRequest(new { message = "La aprobación no es válida: debe provenir de una persona distinta del autor y de quien aplica." });
        }

        object? backup = null;
        DatabaseBackupService.BackupVerification? backupVerification = null;
        if (!req.SkipBackup)
        {
            var backupResult = await backups.CreateAsync($"Antes de aplicar «{script.Name}»", tenant.UserId, ct);
            if (backupResult.Error is not null)
                return BadRequest(new { message = $"No se pudo crear el respaldo previo: {backupResult.Error}. La aplicación se canceló." });
            if (backupResult.Verification is { Valid: false } invalid)
                return BadRequest(new { message = $"El respaldo previo no superó la verificación: {invalid.Message}. La aplicación se canceló." });
            backup = backupResult.Backup;
            backupVerification = backupResult.Verification;
        }

        var sw = Stopwatch.StartNew();
        await using var c = await OpenRawAsync(ct);
        var locked = false;
        try
        {
            await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(hashtext('sigm_sql_apply'))", c))
                await lockCmd.ExecuteNonQueryAsync(ct);
            locked = true;

            await using var tx = await c.BeginTransactionAsync(ct);
            await SetTimeoutsAsync(c, tx, ct);
            long rows;
            try
            {
                await using var cmd = new NpgsqlCommand(script.SqlText, c, tx) { CommandTimeout = 300 };
                rows = await cmd.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (PostgresException ex)
            {
                try { await tx.RollbackAsync(CancellationToken.None); } catch { }
                sw.Stop();
                var msg = $"Error SQLSTATE {ex.SqlState}: {ex.MessageText}";
                await RecordRunAsync(id, "APPLY", false, msg, 0, (int)sw.ElapsedMilliseconds, ct);
                await using (var up = new NpgsqlCommand("UPDATE sigm.db_scripts SET status='FAILED', last_error=@e WHERE id=@id", c))
                { up.Parameters.AddWithValue("@e", Trim(msg, 2000)); up.Parameters.AddWithValue("@id", id); await up.ExecuteNonQueryAsync(ct); }
                return BadRequest(new { message = msg, detail = ex.Detail, hint = ex.Hint, line = ex.Line });
            }

            sw.Stop();
            await using (var up = new NpgsqlCommand("UPDATE sigm.db_scripts SET status='APPLIED', applied_at=now(), last_error=NULL WHERE id=@id", c))
            { up.Parameters.AddWithValue("@id", id); await up.ExecuteNonQueryAsync(ct); }
            var message = $"Aplicado correctamente. Sentencias: {analysis.Statements}." + (rows >= 0 ? $" Filas afectadas: {rows}." : "");
            await RecordRunAsync(id, "APPLY", true, Trim((string?)req.Note is null ? message : $"{message} Nota: {req.Note}", 2000), analysis.Statements, (int)sw.ElapsedMilliseconds, ct);
            return Ok(new { success = true, message, rowsAffected = rows, durationMs = sw.ElapsedMilliseconds, backup, backupVerification, checksum = script.Checksum });
        }
        finally
        {
            if (locked)
            {
                try
                {
                    await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtext('sigm_sql_apply'))", c);
                    await unlock.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch { }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Respaldos
    // ---------------------------------------------------------------------
    [HttpGet("backups")]
    public async Task<IActionResult> Backups([FromQuery] int take = 30, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT id,file_name,size_bytes,checksum,note,created_by,created_at
                FROM sigm.database_backups ORDER BY created_at DESC LIMIT @n", c);
        cmd.Parameters.AddWithValue("@n", take);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new
            {
                id = rd.GetGuid(0),
                fileName = rd.GetString(1),
                sizeBytes = rd.GetInt64(2),
                checksum = rd.IsDBNull(3) ? null : rd.GetString(3),
                note = rd.IsDBNull(4) ? null : rd.GetString(4),
                createdBy = rd.IsDBNull(5) ? (Guid?)null : rd.GetGuid(5),
                createdAt = rd.GetFieldValue<DateTimeOffset>(6)
            });
        return Ok(new { backupPath = BackupDirectory(), items = list });
    }

    [HttpPost("backups")]
    public async Task<IActionResult> CreateBackup([FromBody] BackupRequest? req, CancellationToken ct)
    {
        var result = await backups.CreateAsync(req?.Note ?? "Respaldo manual", tenant.UserId, ct);
        return result.Error is null
            ? Ok(new
            {
                backup = result.Backup,
                verification = result.Verification,
                message = result.Verification is { Valid: true }
                    ? $"Respaldo creado y verificado: {result.Verification.Message}"
                    : "Respaldo creado. No se pudo verificar su integridad; revísielo antes de confiar en él."
            })
            : BadRequest(new { message = result.Error });
    }

    [HttpGet("backups/{id:guid}/download")]
    public async Task<IActionResult> DownloadBackup(Guid id, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        string? fileName = null;
        await using (var cmd = new NpgsqlCommand("SELECT file_name FROM sigm.database_backups WHERE id=@id", c))
        {
            cmd.Parameters.AddWithValue("@id", id);
            fileName = await cmd.ExecuteScalarAsync(ct) as string;
        }
        if (string.IsNullOrWhiteSpace(fileName)) return NotFound(new { message = "Respaldo no encontrado." });
        var path = Path.Combine(BackupDirectory(), Path.GetFileName(fileName));
        if (!System.IO.File.Exists(path)) return NotFound(new { message = "El archivo del respaldo ya no está en el servidor." });
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }

    /// <summary>
    /// Verifica un respaldo sin tocar la base en uso: comprueba que el archivo
    /// siga en disco, que su SHA-256 coincida con el registrado y que pg_restore
    /// pueda leer el archivo (prueba de integridad del volcado).
    /// </summary>
    [HttpPost("backups/{id:guid}/verify")]
    public async Task<IActionResult> VerifyBackup(Guid id, CancellationToken ct)
    {
        var result = await backups.VerifyAsync(id, ct);
        if (result is null) return NotFound(new { message = "Respaldo no encontrado." });
        return Ok(new { valid = result.Valid, checksumMatches = result.ChecksumMatches, objects = result.Objects, message = result.Message });
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------
    private sealed record ScriptRow(Guid Id, string Name, string SqlText, string Checksum, string Status, bool Destructive,
        Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

    private async Task<(ScriptRow? Script, IActionResult? Error)> LoadScriptAsync(Guid id, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"SELECT id,name,sql_text,checksum,status,destructive,created_by,approved_by,approved_at
                FROM sigm.db_scripts WHERE id=@id", c);
        cmd.Parameters.AddWithValue("@id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return (null, NotFound(new { message = "Script no encontrado." }));
        return (new ScriptRow(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetBoolean(5),
            rd.IsDBNull(6) ? null : rd.GetGuid(6),
            rd.IsDBNull(7) ? null : rd.GetGuid(7),
            rd.IsDBNull(8) ? null : rd.GetFieldValue<DateTimeOffset>(8)), null);
    }

    private async Task<NpgsqlConnection> OpenRawAsync(CancellationToken ct)
    {
        var cs = cfg.GetConnectionString("Platform") ?? cfg.GetConnectionString("Default")!;
        var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        return conn;
    }

    private async Task SetTimeoutsAsync(NpgsqlConnection c, NpgsqlTransaction tx, CancellationToken ct)
    {
        var seconds = Math.Clamp(cfg.GetValue("Database:ScriptTimeoutSeconds", 120), 5, 900);
        await using var cmd = new NpgsqlCommand($"SET LOCAL statement_timeout = {seconds * 1000}; SET LOCAL lock_timeout = 10000", c, tx);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> VerifyPasswordAsync(string password, CancellationToken ct)
    {
        if (tenant.UserId is not Guid uid) return false;
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("SELECT password_hash FROM sigm.users WHERE id=@u AND is_active", c);
        cmd.Parameters.AddWithValue("@u", uid);
        var hash = await cmd.ExecuteScalarAsync(ct) as string;
        return PasswordService.Verify(password, hash);
    }

    private async Task RecordRunAsync(Guid scriptId, string mode, bool success, string? message, int statements, int durationMs, CancellationToken ct)
    {
        try
        {
            await using var c = await db.OpenAsync(ct, platform: true);
            await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.db_script_runs(id,script_id,mode,success,message,statements,duration_ms,executed_by,client_ip)
                    VALUES(gen_random_uuid(),@s,@m,@ok,@msg,@st,@ms,@u,CAST(@ip AS inet))", c);
            cmd.Parameters.AddWithValue("@s", scriptId);
            cmd.Parameters.AddWithValue("@m", mode);
            cmd.Parameters.AddWithValue("@ok", success);
            cmd.Parameters.AddWithValue("@msg", (object?)Trim(message, 2000) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@st", statements);
            cmd.Parameters.AddWithValue("@ms", durationMs);
            cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ip", (object?)HttpContext.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            // La bitácora no debe ocultar el resultado principal, pero dejamos rastro.
            logger.LogWarning(ex, "No se pudo registrar la ejecución {Mode} del script {ScriptId}", mode, scriptId);
        }
    }

    private string BackupDirectory() => backups.Directory;

    private int RetentionCount() => backups.RetentionCount;

    private static string Trim(string? value, int max)
        => string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];

    public record BackupRequest(string? Note);
}
