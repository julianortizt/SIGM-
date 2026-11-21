using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
[Authorize]
public class DocumentsController(TenantConnectionFactory db, ITenantContext tenant, IFileStorageService storage) : ControllerBase
{
    [HttpGet]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> List([FromQuery] Guid? patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var sql = "SELECT id,patient_id,encounter_id,title,description,mime_type,size_bytes,version,status,is_public_to_patient,created_at,storage_key FROM sigm.documents WHERE tenant_id=@t" + (patientId.HasValue?" AND patient_id=@p":"") + " ORDER BY created_at DESC LIMIT 200";
        await using var cmd = new NpgsqlCommand(sql,c); cmd.Parameters.AddWithValue("@t",tid); if(patientId.HasValue) cmd.Parameters.AddWithValue("@p",patientId.Value);
        var list=new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new{id=rd.GetGuid(0),patientId=rd.IsDBNull(1)?(Guid?)null:rd.GetGuid(1),encounterId=rd.IsDBNull(2)?(Guid?)null:rd.GetGuid(2),title=rd.GetString(3),description=rd.IsDBNull(4)?null:rd.GetString(4),mimeType=rd.GetString(5),sizeBytes=rd.GetInt64(6),version=rd.GetInt32(7),status=rd.GetString(8),isPublicToPatient=rd.GetBoolean(9),createdAt=rd.GetFieldValue<DateTimeOffset>(10),storageKey=rd.IsDBNull(11)?null:rd.GetString(11)});
        return Ok(list);
    }

    [HttpPost]
    [RequirePermission("documents.create")]
    public async Task<IActionResult> Register([FromBody] Req req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.documents(id,tenant_id,patient_id,encounter_id,title,description,storage_key,mime_type,size_bytes,status,is_public_to_patient,uploaded_by)
            VALUES(@id,@t,@p,@e,@title,@desc,@key,@mime,@size,'ACTIVE',@pub,@u)", c);
        cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@t",tid); cmd.Parameters.AddWithValue("@p",(object?)req.PatientId??DBNull.Value); cmd.Parameters.AddWithValue("@e",(object?)req.EncounterId??DBNull.Value);
        cmd.Parameters.AddWithValue("@title",req.Title); cmd.Parameters.AddWithValue("@desc",(object?)req.Description??DBNull.Value);
        var storedKey = req.StorageKey ?? $"pending/{id}";
        if (string.IsNullOrWhiteSpace(storedKey))
            return BadRequest(new { message = "storageKey requerido." });
        if (!storedKey.Equals("pending/" + id.ToString(), StringComparison.Ordinal) &&
            (storedKey.Contains("..", StringComparison.Ordinal) || storedKey.Contains("/", StringComparison.Ordinal) ||
             storedKey.Contains('\\', StringComparison.Ordinal) || storedKey.Contains('\0') ||
             Path.IsPathRooted(storedKey)))
            return BadRequest(new { message = "storageKey contiene caracteres o secuencias no permitidos." });
        cmd.Parameters.AddWithValue("@key", storedKey);
        cmd.Parameters.AddWithValue("@mime",req.MimeType ?? "application/octet-stream"); cmd.Parameters.AddWithValue("@size",req.SizeBytes); cmd.Parameters.AddWithValue("@pub",req.IsPublicToPatient);
        cmd.Parameters.AddWithValue("@u",(object?)tenant.UserId??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return Ok(new { id, status = "ACTIVE", storageKey = storedKey });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)]
    [RequirePermission("documents.create")]
    public async Task<IActionResult> Upload([FromForm] Guid? patientId, [FromForm] Guid? encounterId, [FromForm] string? title, [FromForm] string? description, [FromForm] bool isPublicToPatient, IFormFile file, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        if (file is null || file.Length == 0) return BadRequest(new { message = "Archivo requerido." });
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
        ms.Position = 0;
        var key = await storage.SaveAsync(tid.ToString(), file.FileName, ms, file.ContentType ?? "application/octet-stream", ct);
        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.documents(id,tenant_id,patient_id,encounter_id,title,description,storage_key,mime_type,size_bytes,checksum_sha256,version,status,is_public_to_patient,uploaded_by)
            VALUES(@id,@t,@p,@e,@title,@desc,@key,@mime,@size,@hash,1,'ACTIVE',@pub,@u)", c, tx))
        {
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
            cmd.Parameters.AddWithValue("@p", (object?)patientId ?? DBNull.Value); cmd.Parameters.AddWithValue("@e", (object?)encounterId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? file.FileName : title);
            cmd.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@key", key); cmd.Parameters.AddWithValue("@mime", file.ContentType ?? "application/octet-stream");
            cmd.Parameters.AddWithValue("@size", file.Length); cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@pub", isPublicToPatient); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.document_versions(id,tenant_id,document_id,version_number,storage_key,mime_type,size_bytes,checksum_sha256,uploaded_by)
            VALUES(gen_random_uuid(),@t,@d,1,@key,@mime,@size,@hash,@u)", c, tx))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", id); cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@mime", file.ContentType ?? "application/octet-stream"); cmd.Parameters.AddWithValue("@size", file.Length);
            cmd.Parameters.AddWithValue("@hash", hash); cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Ok(new { id, storageKey = key, version = 1, checksum = hash });
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        string? key = null, mime = null, title = null;
        await using (var cmd = new NpgsqlCommand("SELECT storage_key,mime_type,title FROM sigm.documents WHERE id=@id AND tenant_id=@t", c))
        {
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return NotFound();
            key = rd.GetString(0); mime = rd.GetString(1); title = rd.GetString(2);
        }
        var stream = await storage.OpenReadAsync(key!, ct);
        if (stream is null) return NotFound(new { message = "Contenido no encontrado en almacenamiento." });
        return File(stream, mime ?? "application/octet-stream", title);
    }

    [HttpGet("{id:guid}/versions")]
    [RequirePermission("documents.read")]
    public async Task<IActionResult> Versions(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id,version_number,storage_key,mime_type,size_bytes,checksum_sha256,uploaded_by,created_at FROM sigm.document_versions WHERE tenant_id=@t AND document_id=@d ORDER BY version_number DESC", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@d", id);
        var list = new List<object>(); await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct)) list.Add(new { id = rd.GetGuid(0), versionNumber = rd.GetInt32(1), storageKey = rd.GetString(2), mimeType = rd.GetString(3), sizeBytes = rd.GetInt64(4), checksum = rd.IsDBNull(5)?null:rd.GetString(5), uploadedBy = rd.IsDBNull(6)?(Guid?)null:rd.GetGuid(6), createdAt = rd.GetFieldValue<DateTimeOffset>(7) });
        return Ok(list);
    }

    public record Req(Guid? PatientId, Guid? EncounterId, string Title, string? Description, string? StorageKey, string? MimeType, long SizeBytes, bool IsPublicToPatient);
}
