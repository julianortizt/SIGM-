using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/specialty-forms")]
[Authorize]
public class SpecialtyFormsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    [HttpGet][RequirePermission("tenant.specialties.read")]
    public async Task<IActionResult> List([FromQuery] Guid? specialtyId, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        var sql = "SELECT id,tenant_id,specialty_id,name,version,description,schema,is_active FROM sigm.specialty_forms WHERE (tenant_id IS NULL OR tenant_id=@t)";
        if (specialtyId.HasValue) sql += " AND specialty_id=@s"; sql += " ORDER BY name, version DESC";
        await using var cmd = new NpgsqlCommand(sql, c); cmd.Parameters.AddWithValue("@t", (object?)tenant.TenantId ?? DBNull.Value);
        if (specialtyId.HasValue) cmd.Parameters.AddWithValue("@s", specialtyId.Value);
        return Ok(await ReadAll(cmd, ct));
    }

    [HttpPost][RequirePermission("tenant.specialties.manage")]
    public async Task<IActionResult> Create([FromBody] FormReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.specialty_forms(id,tenant_id,specialty_id,name,version,description,schema,is_active)
            VALUES(@id,@t,@s,@n,@v,@d,COALESCE(@sch::jsonb,'{}'::jsonb),true)", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@s", req.SpecialtyId);
        cmd.Parameters.AddWithValue("@n", req.Name); cmd.Parameters.AddWithValue("@v", req.Version <= 0 ? 1 : req.Version);
        cmd.Parameters.AddWithValue("@d", (object?)req.Description ?? DBNull.Value); cmd.Parameters.AddWithValue("@sch", (object?)req.Schema ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok(new { id });
    }

    [HttpGet("{formId:guid}/fields")][RequirePermission("tenant.specialties.read")]
    public async Task<IActionResult> Fields(Guid formId, CancellationToken ct)
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var cmd = new NpgsqlCommand(@"SELECT id,form_id,field_key,label,field_type,required,options,config,sort_order
            FROM sigm.specialty_form_fields
            WHERE form_id=@f AND (tenant_id IS NULL OR tenant_id=@t) ORDER BY sort_order, label", c);
        cmd.Parameters.AddWithValue("@f", formId); cmd.Parameters.AddWithValue("@t", tid);
        return Ok(await ReadAll(cmd, ct));
    }

    [HttpPost("{formId:guid}/fields")][RequirePermission("tenant.specialties.manage")]
    public async Task<IActionResult> AddField(Guid formId, [FromBody] FieldReq req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        if (string.IsNullOrWhiteSpace(req.FieldKey) || string.IsNullOrWhiteSpace(req.Label))
            return BadRequest(new { message = "La clave y la etiqueta del campo son obligatorias." });
        await using var c = await db.OpenAsync(ct);

        // Verificar propiedad del formulario antes de insertar: sin esto, un formId de
        // otro tenant (o inexistente) terminaba en un 500 opaco por la FK/el trigger.
        await using (var owns = new NpgsqlCommand(@"SELECT 1 FROM sigm.specialty_forms
            WHERE id=@f AND (tenant_id IS NULL OR tenant_id=@t) LIMIT 1", c))
        {
            owns.Parameters.AddWithValue("@f", formId); owns.Parameters.AddWithValue("@t", tid);
            if (await owns.ExecuteScalarAsync(ct) is null)
                return NotFound(new { message = "El formulario no existe en esta organización." });
        }

        static object JsonOrEmpty(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DBNull.Value;
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('[')) return trimmed;
            return DBNull.Value; // texto libre no es JSON válido: se guarda '{}'
        }

        await using var cmd = new NpgsqlCommand(@"INSERT INTO sigm.specialty_form_fields(id,tenant_id,form_id,field_key,label,field_type,required,options,config,sort_order)
            VALUES(gen_random_uuid(),@t,@f,@k,@l,@type,@req,COALESCE(@opt::jsonb,'{}'::jsonb),COALESCE(@cfg::jsonb,'{}'::jsonb),@ord)", c);
        cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@f", formId); cmd.Parameters.AddWithValue("@k", req.FieldKey.Trim());
        cmd.Parameters.AddWithValue("@l", req.Label.Trim()); cmd.Parameters.AddWithValue("@type", req.FieldType ?? "text"); cmd.Parameters.AddWithValue("@req", req.Required);
        cmd.Parameters.AddWithValue("@opt", JsonOrEmpty(req.Options)); cmd.Parameters.AddWithValue("@cfg", JsonOrEmpty(req.Config)); cmd.Parameters.AddWithValue("@ord", req.SortOrder);
        try { await cmd.ExecuteNonQueryAsync(ct); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "22P02")
        {
            return BadRequest(new { message = "options/config deben ser un objeto JSON válido." });
        }
        return Ok();
    }

    private static async Task<List<Dictionary<string, object?>>> ReadAll(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Dictionary<string, object?>>(); await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) { var d = new Dictionary<string, object?>(); for (int i = 0; i < rd.FieldCount; i++) d[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i); list.Add(d); }
        return list;
    }

    public record FormReq(Guid SpecialtyId, string Name, int Version, string? Description, string? Schema);
    public record FieldReq(string FieldKey, string Label, string? FieldType, bool Required, string? Options, string? Config, int SortOrder);
}
