using System.Diagnostics;
using System.Security.Cryptography;
using Npgsql;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Services;

/// <summary>
/// Creación, verificación y retención de respaldos de la base de datos.
/// Vive fuera del controlador para que tanto la consola como el trabajo
/// programado usen exactamente el mismo procedimiento y las mismas garantías:
/// pg_dump en formato custom, checksum SHA-256, prueba de lectura con
/// pg_restore y poda según <c>Database:BackupRetentionCount</c>.
/// </summary>
public sealed class DatabaseBackupService(TenantConnectionFactory db, IConfiguration cfg, ILogger<DatabaseBackupService> logger)
{
    public sealed record BackupVerification(bool Valid, bool ChecksumMatches, int Objects, string Message);
    public sealed record BackupCreation(object? Backup, string? Error, BackupVerification? Verification);

    public string Directory => cfg["Database:BackupPath"] is { Length: > 0 } p
        ? p
        : Path.Combine(AppContext.BaseDirectory, "backups");

    public int RetentionCount => Math.Clamp(cfg.GetValue("Database:BackupRetentionCount", 30), 1, 365);

    public bool AutoBackupEnabled => cfg.GetValue("Database:AutoBackupEnabled", true);

    public int AutoBackupIntervalHours => Math.Clamp(cfg.GetValue("Database:AutoBackupIntervalHours", 24), 1, 24 * 30);

    public async Task<BackupCreation> CreateAsync(string note, Guid? userId, CancellationToken ct, bool verify = true)
    {
        var pgDump = FindPgDump();
        if (pgDump is null) return new BackupCreation(null, "pg_dump no está disponible en el servidor", null);

        try
        {
            var cs = new NpgsqlConnectionStringBuilder(cfg.GetConnectionString("Platform") ?? cfg.GetConnectionString("Default")!);
            System.IO.Directory.CreateDirectory(Directory);
            var fileName = $"sigm_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.dump";
            var path = Path.Combine(Directory, fileName);

            var psi = new ProcessStartInfo
            {
                FileName = pgDump,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(cs.Host ?? "127.0.0.1");
            psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(cs.Port.ToString());
            psi.ArgumentList.Add("-U"); psi.ArgumentList.Add(cs.Username ?? "postgres");
            psi.ArgumentList.Add("-d"); psi.ArgumentList.Add(cs.Database ?? "postgres");
            psi.ArgumentList.Add("-Fc");
            psi.ArgumentList.Add("--no-owner");
            psi.ArgumentList.Add("--no-privileges");
            psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(path);
            psi.Environment["PGPASSWORD"] = cs.Password ?? "";
            psi.Environment["PGCONNECT_TIMEOUT"] = "20";

            using var process = Process.Start(psi);
            if (process is null) return new BackupCreation(null, "no se pudo iniciar pg_dump", null);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            await process.WaitForExitAsync(timeout.Token);

            if (process.ExitCode != 0 || !System.IO.File.Exists(path))
                return new BackupCreation(null, $"pg_dump falló: {Trim(stderr, 500)}", null);

            var size = new FileInfo(path).Length;
            var checksum = Convert.ToHexString(SHA256.HashData(await System.IO.File.ReadAllBytesAsync(path, ct))).ToLowerInvariant();
            var id = Guid.NewGuid();
            await using (var c = await db.OpenAsync(ct, platform: true))
            {
                await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.database_backups(id,file_name,size_bytes,checksum,note,created_by)
                        VALUES(@id,@f,@s,@h,@n,@u)", c);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@f", fileName);
                cmd.Parameters.AddWithValue("@s", size);
                cmd.Parameters.AddWithValue("@h", checksum);
                cmd.Parameters.AddWithValue("@n", note);
                cmd.Parameters.AddWithValue("@u", (object?)userId ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await PruneAsync(ct);

            BackupVerification? verification = null;
            if (verify)
            {
                verification = await VerifyFileAsync(path, checksum, ct);
                if (!verification.Valid)
                    logger.LogError("El respaldo {File} se creó pero no superó la verificación: {Message}", fileName, verification.Message);
            }

            return new BackupCreation(new
            {
                id,
                fileName,
                sizeBytes = size,
                checksum,
                note,
                createdAt = DateTimeOffset.Now
            }, null, verification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo crear el respaldo");
            return new BackupCreation(null, ex.Message, null);
        }
    }

    public async Task<BackupVerification?> VerifyAsync(Guid id, CancellationToken ct)
    {
        string? fileName = null, expectedChecksum = null;
        await using (var c = await db.OpenAsync(ct, platform: true))
        {
            await using var cmd = new NpgsqlCommand("SELECT file_name, checksum FROM sigm.database_backups WHERE id=@id", c);
            cmd.Parameters.AddWithValue("@id", id);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                fileName = rd.GetString(0);
                expectedChecksum = rd.IsDBNull(1) ? null : rd.GetString(1);
            }
        }
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var path = Path.Combine(Directory, Path.GetFileName(fileName));
        if (!System.IO.File.Exists(path))
            return new BackupVerification(false, false, 0, "El archivo del respaldo ya no está en el servidor.");

        var actual = Convert.ToHexString(SHA256.HashData(await System.IO.File.ReadAllBytesAsync(path, ct))).ToLowerInvariant();
        var checksumMatches = expectedChecksum is null || string.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase);

        var result = await VerifyFileAsync(path, actual, ct);
        return result with { ChecksumMatches = checksumMatches, Valid = result.Valid && checksumMatches };
    }

    public async Task<DateTimeOffset?> LastAutomaticBackupAtAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await db.OpenAsync(ct, platform: true);
            await using var cmd = new NpgsqlCommand(
                "SELECT max(created_at) FROM sigm.database_backups WHERE note LIKE 'Respaldo automático%'", c);
            var value = await cmd.ExecuteScalarAsync(ct);
            return value is DateTimeOffset dt ? dt : value is DateTime d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo consultar el último respaldo automático");
            return null;
        }
    }

    private static async Task<BackupVerification> VerifyFileAsync(string path, string checksum, CancellationToken ct)
    {
        var pgRestore = FindPgRestore();
        if (pgRestore is null)
            return new BackupVerification(true, true, 0,
                "El archivo coincide con su checksum, pero pg_restore no está disponible para la prueba de lectura.");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pgRestore,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("--list");
            psi.ArgumentList.Add(path);
            using var process = Process.Start(psi);
            if (process is null) return new BackupVerification(false, true, 0, "No se pudo iniciar pg_restore.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var objects = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Count(l => !l.TrimStart().StartsWith(';') && l.Trim().Length > 0);

            if (process.ExitCode != 0)
                return new BackupVerification(false, true, objects, $"pg_restore no pudo leer el archivo: {Trim(stderr, 400)}");

            return new BackupVerification(objects > 0, true, objects,
                objects > 0 ? $"Respaldo legible: {objects} objetos en el volcado." : "El volcado no contiene objetos legibles.");
        }
        catch (Exception ex)
        {
            return new BackupVerification(false, true, 0, ex.Message);
        }
    }

    /// <summary>Elimina los respaldos más antiguos para que no crezcan sin límite.</summary>
    private async Task PruneAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await db.OpenAsync(ct, platform: true);
            var stale = new List<(Guid Id, string FileName)>();
            await using (var cmd = new NpgsqlCommand(@"SELECT id, file_name FROM sigm.database_backups
                    ORDER BY created_at DESC OFFSET @keep", c))
            {
                cmd.Parameters.AddWithValue("@keep", RetentionCount);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct)) stale.Add((rd.GetGuid(0), rd.GetString(1)));
            }
            if (stale.Count == 0) return;

            foreach (var (id, fileName) in stale)
            {
                var path = Path.Combine(Directory, Path.GetFileName(fileName));
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
                catch (Exception ex) { logger.LogWarning(ex, "No se pudo borrar el respaldo {File}", fileName); continue; }
                await using var del = new NpgsqlCommand("DELETE FROM sigm.database_backups WHERE id=@id", c);
                del.Parameters.AddWithValue("@id", id);
                await del.ExecuteNonQueryAsync(ct);
            }
            logger.LogInformation("Retención de respaldos: eliminados {Count} archivos antiguos (máximo {Keep}).", stale.Count, RetentionCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo aplicar la retención de respaldos");
        }
    }

    public static string? FindPgDump()
    {
        if (Environment.GetEnvironmentVariable("PG_DUMP_PATH") is { Length: > 0 } configured && System.IO.File.Exists(configured))
            return configured;
        foreach (var candidate in new[] { "/usr/bin/pg_dump", "/usr/local/bin/pg_dump", "/usr/lib/postgresql/18/bin/pg_dump" })
            if (System.IO.File.Exists(candidate)) return candidate;
        return null;
    }

    public static string? FindPgRestore()
    {
        if (Environment.GetEnvironmentVariable("PG_RESTORE_PATH") is { Length: > 0 } configured && System.IO.File.Exists(configured))
            return configured;
        foreach (var candidate in new[] { "/usr/bin/pg_restore", "/usr/local/bin/pg_restore", "/usr/lib/postgresql/18/bin/pg_restore" })
            if (System.IO.File.Exists(candidate)) return candidate;
        return null;
    }

    private static string Trim(string? value, int max)
        => string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];
}
