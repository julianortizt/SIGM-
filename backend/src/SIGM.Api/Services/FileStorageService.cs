using Npgsql;
using SIGM.Infrastructure.Persistence;

namespace SIGM.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(string tenantId, string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>
/// Local/dev implementation (filesystem). In production, replace with S3-compatible private storage.
/// </summary>
public sealed class LocalFileStorageService(IConfiguration cfg, IWebHostEnvironment env) : IFileStorageService
{
    private static readonly string[] ForbiddenPatterns = new[] { "..", Environment.NewLine, "\0" };

    private string RootRaw => cfg["Storage:LocalPath"] ?? Path.Combine(env.ContentRootPath, "storage");
    private string Root => Path.GetFullPath(RootRaw);

    public async Task<string> SaveAsync(string tenantId, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file";
        if (ForbiddenPatterns.Any(p => safeName.Contains(p, StringComparison.Ordinal)))
            throw new ArgumentException("nombre de archivo no válido", nameof(fileName));
        var safe = safeName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var key = $"{tenantId}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}-{safe}";
        var full = Path.Combine(Root, key.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (dir is null || !Path.GetFullPath(dir).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.GetFullPath(dir).Equals(Root, StringComparison.Ordinal))
            throw new InvalidOperationException("ruta de almacenamiento fuera del directorio permitido");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveFullPath(storageKey);
        if (!File.Exists(full)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(full));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var full = ResolveFullPath(storageKey);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("clave de almacenamiento requerida", nameof(storageKey));
        if (ForbiddenPatterns.Any(p => storageKey.Contains(p, StringComparison.Ordinal)) || storageKey.StartsWith("./") || storageKey.StartsWith("../"))
            throw new ArgumentException("clave de almacenamiento no válida", nameof(storageKey));
        var candidate = Path.GetFullPath(Path.Combine(Root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new InvalidOperationException("clave de almacenamiento no válida (ruta fuera del directorio permitido)");
        return candidate;
    }
}

/// <summary>
/// Almacenamiento durable en PostgreSQL para despliegues serverless (Vercel).
/// El filesystem de una función es efímero: con LocalFileStorageService los
/// archivos subidos desaparecían tras el primer cold start. Los blobs viven en
/// sigm.file_blobs y heredan la durabilidad y los backups de Neon.
/// </summary>
public sealed class DatabaseFileStorageService(TenantConnectionFactory db) : IFileStorageService
{
    public async Task<string> SaveAsync(string tenantId, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            throw new ArgumentException("Identificador de organización no válido.", nameof(tenantId));
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file";
        if (safeName.Contains("..", StringComparison.Ordinal) || safeName.Contains('\0'))
            throw new ArgumentException("nombre de archivo no válido", nameof(fileName));

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var key = $"{tenantId}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}-{safeName}";

        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.file_blobs
            (storage_key,tenant_id,file_name,content_type,content,created_at)
            VALUES(@k,@t,@n,@ct,@b,now())", c);
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@t", tid);
        cmd.Parameters.AddWithValue("@n", safeName);
        cmd.Parameters.AddWithValue("@ct", string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        cmd.Parameters.AddWithValue("@b", buffer.ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
        return key;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("SELECT content FROM sigm.file_blobs WHERE storage_key=@k", c);
        cmd.Parameters.AddWithValue("@k", storageKey);
        var bytes = await cmd.ExecuteScalarAsync(ct) as byte[];
        return bytes is null ? null : new MemoryStream(bytes, writable: false);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new NpgsqlCommand("DELETE FROM sigm.file_blobs WHERE storage_key=@k", c);
        cmd.Parameters.AddWithValue("@k", storageKey);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
