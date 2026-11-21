using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

/// <summary>
/// Portal del paciente. Expone únicamente la información del paciente vinculado
/// a la identidad autenticada (mismo correo del usuario dentro de la organización)
/// y nunca permite modificar datos clínicos.
/// </summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize]
public class PortalController(TenantConnectionFactory db, ITenantContext tenant, IFileStorageService storage, AuditService audit) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "profile", "READ");

        await using var cmd = new NpgsqlCommand(@"SELECT p.id,p.patient_number,p.full_name,p.document_type,p.document_number,p.birth_date,p.sex_at_birth,
                p.email,p.phone,p.mobile_phone,p.address_line,p.city,p.department,t.commercial_name,t.tenant_code
            FROM sigm.patients p JOIN sigm.tenants t ON t.id=p.tenant_id
            WHERE p.id=@p AND p.tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@p", patientId!.Value);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return NotFound(new { message = "Paciente no encontrado." });

        return Ok(new
        {
            id = rd.GetGuid(0),
            patientNumber = rd.IsDBNull(1) ? null : rd.GetString(1),
            fullName = rd.IsDBNull(2) ? null : rd.GetString(2),
            documentType = rd.IsDBNull(3) ? null : rd.GetString(3),
            documentNumber = rd.IsDBNull(4) ? null : rd.GetString(4),
            birthDate = rd.IsDBNull(5) ? (DateOnly?)null : rd.GetFieldValue<DateOnly>(5),
            sexAtBirth = rd.IsDBNull(6) ? null : rd.GetString(6),
            email = rd.IsDBNull(7) ? null : rd.GetString(7),
            phone = rd.IsDBNull(8) ? null : rd.GetString(8),
            mobilePhone = rd.IsDBNull(9) ? null : rd.GetString(9),
            addressLine = rd.IsDBNull(10) ? null : rd.GetString(10),
            city = rd.IsDBNull(11) ? null : rd.GetString(11),
            department = rd.IsDBNull(12) ? null : rd.GetString(12),
            tenantName = rd.IsDBNull(13) ? "SIGM Cloud" : rd.GetString(13),
            tenantCode = rd.IsDBNull(14) ? null : rd.GetString(14)
        });
    }

    [HttpGet("me/appointments")]
    public async Task<IActionResult> Appointments(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "appointments", "READ");

        await using var cmd = new NpgsqlCommand(@"SELECT a.id,a.appointment_number,a.starts_at,a.ends_at,a.status,a.reason,
                pr.display_name,sp.name,s.name
            FROM sigm.appointments a
            LEFT JOIN sigm.professionals pr ON pr.id=a.professional_id
            LEFT JOIN sigm.specialties sp ON sp.id=a.specialty_id
            LEFT JOIN sigm.sites s ON s.id=a.site_id
            WHERE a.tenant_id=@t AND a.patient_id=@p
            ORDER BY a.starts_at DESC LIMIT 100", c);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
        cmd.Parameters.AddWithValue("@p", patientId!.Value);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new
            {
                id = rd.GetGuid(0),
                appointmentNumber = rd.IsDBNull(1) ? null : rd.GetString(1),
                startsAt = rd.GetFieldValue<DateTimeOffset>(2),
                endsAt = rd.GetFieldValue<DateTimeOffset>(3),
                status = rd.GetString(4),
                reason = rd.IsDBNull(5) ? null : rd.GetString(5),
                professional = rd.IsDBNull(6) ? null : rd.GetString(6),
                specialty = rd.IsDBNull(7) ? null : rd.GetString(7),
                site = rd.IsDBNull(8) ? null : rd.GetString(8)
            });
        return Ok(list);
    }

    [HttpGet("me/documents")]
    public async Task<IActionResult> Documents(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "documents", "READ");

        await using var cmd = new NpgsqlCommand(@"SELECT id,title,description,mime_type,size_bytes,version,created_at
            FROM sigm.documents WHERE tenant_id=@t AND patient_id=@p AND is_public_to_patient AND status='ACTIVE'
            ORDER BY created_at DESC LIMIT 100", c);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
        cmd.Parameters.AddWithValue("@p", patientId!.Value);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new
            {
                id = rd.GetGuid(0),
                title = rd.GetString(1),
                description = rd.IsDBNull(2) ? null : rd.GetString(2),
                mimeType = rd.GetString(3),
                sizeBytes = rd.GetInt64(4),
                version = rd.GetInt32(5),
                createdAt = rd.GetFieldValue<DateTimeOffset>(6)
            });
        return Ok(list);
    }

    [HttpGet("me/documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "documents", "READ");

        string? key = null, mime = null, title = null;
        await using (var cmd = new NpgsqlCommand(@"SELECT storage_key,mime_type,title FROM sigm.documents
                WHERE id=@id AND tenant_id=@t AND patient_id=@p AND is_public_to_patient AND status='ACTIVE'", c))
        {
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
            cmd.Parameters.AddWithValue("@p", patientId!.Value);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return NotFound(new { message = "Documento no disponible." });
            key = rd.GetString(0); mime = rd.GetString(1); title = rd.GetString(2);
        }
        var stream = await storage.OpenReadAsync(key!, ct);
        if (stream is null) return NotFound(new { message = "Contenido no encontrado." });
        return File(stream, mime ?? "application/octet-stream", title ?? "documento");
    }

    [HttpGet("me/prescriptions")]
    public async Task<IActionResult> Prescriptions(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "prescriptions", "READ");

        var list = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"SELECT p.id,p.prescription_number,p.status,p.issued_at,p.notes,
                COALESCE(json_agg(json_build_object('name',i.medication_name,'dose',i.dose,'frequency',i.frequency,
                    'route',i.route,'durationDays',i.duration_days,'instructions',i.instructions))
                    FILTER (WHERE i.id IS NOT NULL),'[]'::json)
            FROM sigm.prescriptions p
            LEFT JOIN sigm.prescription_items i ON i.prescription_id=p.id AND i.tenant_id=p.tenant_id
            WHERE p.tenant_id=@t AND p.patient_id=@p
            GROUP BY p.id ORDER BY p.issued_at DESC LIMIT 100", c))
        {
            cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
            cmd.Parameters.AddWithValue("@p", patientId!.Value);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                list.Add(new
                {
                    id = rd.GetGuid(0),
                    number = rd.IsDBNull(1) ? null : rd.GetString(1),
                    status = rd.GetString(2),
                    issuedAt = rd.GetFieldValue<DateTimeOffset>(3),
                    notes = rd.IsDBNull(4) ? null : rd.GetString(4),
                    items = rd.IsDBNull(5) ? "[]" : rd.GetString(5)
                });
        }
        return Ok(list);
    }

    [HttpGet("me/lab-results")]
    public async Task<IActionResult> LabResults(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "lab-results", "READ");

        await using var cmd = new NpgsqlCommand(@"SELECT o.id,o.order_number,o.status,o.ordered_at,
                COALESCE(json_agg(json_build_object('test',t.name,'status',oi.status,'result',r.result_value,
                    'unit',t.unit,'reference',t.reference_range,'flag',r.result_flag))
                    FILTER (WHERE oi.id IS NOT NULL),'[]'::json)
            FROM sigm.laboratory_orders o
            LEFT JOIN sigm.laboratory_order_items oi ON oi.laboratory_order_id=o.id AND oi.tenant_id=o.tenant_id
            LEFT JOIN sigm.laboratory_tests t ON t.id=oi.test_id
            LEFT JOIN sigm.laboratory_results r ON r.order_item_id=oi.id
            WHERE o.tenant_id=@t AND o.patient_id=@p
            GROUP BY o.id ORDER BY o.ordered_at DESC LIMIT 50", c);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
        cmd.Parameters.AddWithValue("@p", patientId!.Value);
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new
            {
                id = rd.GetGuid(0),
                orderNumber = rd.IsDBNull(1) ? null : rd.GetString(1),
                status = rd.GetString(2),
                orderedAt = rd.GetFieldValue<DateTimeOffset>(3),
                tests = rd.IsDBNull(4) ? "[]" : rd.GetString(4)
            });
        return Ok(list);
    }

    private static readonly (string Type, string Label, string Version, string Description)[] ConsentCatalog =
    [
        ("DATA_TREATMENT", "Tratamiento de datos personales", "2026-1",
            "Autorizo el tratamiento de mis datos personales conforme a la Ley 1581 de 2012 y la política de privacidad de la institución."),
        ("CLINICAL_HISTORY", "Uso de mi historia clínica", "2026-1",
            "Autorizo el uso de mi historia clínica para mi atención, con acceso restringido al personal autorizado."),
        ("INTEROPERABILITY", "Intercambio de historia clínica", "2026-1",
            "Autorizo el intercambio de mi información clínica con otros prestadores a través de la historia clínica electrónica interoperable.")
    ];

    public record ConsentRequest(string? ConsentType, bool? Granted);

    /// <summary>Consentimientos informados del paciente con su estado vigente.</summary>
    [HttpGet("me/consents")]
    public async Task<IActionResult> Consents(CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "consents", "READ");

        var rows = new Dictionary<string, (bool Granted, DateTimeOffset? GrantedAt, DateTimeOffset? RevokedAt, DateTimeOffset UpdatedAt)>();
        await using (var cmd = new NpgsqlCommand(@"SELECT DISTINCT ON (consent_type) consent_type,granted,granted_at,revoked_at,updated_at
                FROM sigm.patient_consents
                WHERE tenant_id=@t AND patient_id=@p
                ORDER BY consent_type, created_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
            cmd.Parameters.AddWithValue("@p", patientId!.Value);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                rows[rd.GetString(0)] = (
                    rd.GetBoolean(1),
                    rd.IsDBNull(2) ? null : rd.GetFieldValue<DateTimeOffset>(2),
                    rd.IsDBNull(3) ? null : rd.GetFieldValue<DateTimeOffset>(3),
                    rd.GetFieldValue<DateTimeOffset>(4));
        }

        var list = ConsentCatalog.Select(x =>
        {
            rows.TryGetValue(x.Type, out var current);
            return new
            {
                consentType = x.Type,
                label = x.Label,
                version = x.Version,
                description = x.Description,
                granted = current.Granted,
                grantedAt = current.GrantedAt,
                revokedAt = current.RevokedAt,
                updatedAt = current.UpdatedAt == default ? (DateTimeOffset?)null : current.UpdatedAt
            };
        });
        return Ok(list);
    }

    /// <summary>
    /// El paciente otorga o revoca un consentimiento informado. Se guarda versión,
    /// fecha, IP y agente de usuario como evidencia; queda auditado.
    /// </summary>
    [HttpPost("me/consents")]
    public async Task<IActionResult> UpdateConsent([FromBody] ConsentRequest req, CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;

        var definition = ConsentCatalog.FirstOrDefault(x => x.Type == req.ConsentType);
        if (definition.Type is null) return BadRequest(new { message = "Tipo de consentimiento no válido." });
        if (req.Granted is not bool granted) return BadRequest(new { message = "Indique si acepta o revoca el consentimiento." });

        var evidence = $"Portal del paciente | IP {HttpContext.Connection.RemoteIpAddress} | {(granted ? "Aceptado" : "Revocado")} el {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | UA {Request.Headers.UserAgent}";
        await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.patient_consents(tenant_id,patient_id,consent_type,consent_version,granted,granted_at,revoked_at,recorded_by,notes)
                VALUES(@t,@p,@type,@ver,@g,CASE WHEN @g THEN now() ELSE NULL END,CASE WHEN @g THEN NULL ELSE now() END,@u,@n)
                ON CONFLICT (tenant_id,patient_id,consent_type,consent_version) DO UPDATE SET
                    granted=EXCLUDED.granted,
                    granted_at=CASE WHEN EXCLUDED.granted THEN now() ELSE NULL END,
                    revoked_at=CASE WHEN EXCLUDED.granted THEN NULL ELSE now() END,
                    recorded_by=EXCLUDED.recorded_by,
                    notes=EXCLUDED.notes,
                    updated_at=now()", c))
        {
            cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
            cmd.Parameters.AddWithValue("@p", patientId!.Value);
            cmd.Parameters.AddWithValue("@type", definition.Type);
            cmd.Parameters.AddWithValue("@ver", definition.Version);
            cmd.Parameters.AddWithValue("@g", granted);
            cmd.Parameters.AddWithValue("@u", (object?)tenant.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@n", evidence);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await AuditAsync(patientId!.Value, $"consent:{definition.Type}", "UPDATE");
        return Ok(new { consentType = definition.Type, version = definition.Version, granted, updatedAt = DateTimeOffset.UtcNow });
    }

    public record UpdateContactRequest(string? Phone, string? MobilePhone, string? AddressLine, string? City, string? Department);

    /// <summary>El paciente solo puede actualizar sus datos de contacto, nunca los clínicos.</summary>
    [HttpPut("me/contact")]
    public async Task<IActionResult> UpdateContact([FromBody] UpdateContactRequest req, CancellationToken ct)
    {
        var (c, patientId, error) = await ResolveAsync(ct);
        if (error is not null) return error;
        await using var _ = c;
        await AuditAsync(patientId!.Value, "contact", "UPDATE");

        await using var cmd = new NpgsqlCommand(@"UPDATE sigm.patients SET phone=@ph, mobile_phone=@mo, address_line=@ad, city=@ci, department=@de, updated_at=now()
            WHERE id=@p AND tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@p", patientId!.Value);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId!.Value);
        cmd.Parameters.AddWithValue("@ph", (object?)Clean(req.Phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mo", (object?)Clean(req.MobilePhone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ad", (object?)Clean(req.AddressLine) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ci", (object?)Clean(req.City) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@de", (object?)Clean(req.Department) ?? DBNull.Value);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound(new { message = "Paciente no encontrado." });
        return Ok(new { updated = true });
    }

    /// <summary>Registra el acceso del paciente a sus propios datos (Ley 1581 / trazabilidad).</summary>
    private Task AuditAsync(Guid patientId, string resource, string action) =>
        audit.LogAsync("PATIENT_PORTAL", patientId, action, after: new { resource },
            ip: HttpContext.Connection.RemoteIpAddress?.ToString(), ct: HttpContext.RequestAborted);

    private async Task<(NpgsqlConnection Connection, Guid? PatientId, IActionResult? Error)> ResolveAsync(CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid || tenant.UserId is not Guid uid)
        {
            var bad = new NpgsqlConnection();
            return (bad, null, BadRequest(new { message = "Contexto de organización/usuario no resuelto." }));
        }

        var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT p.id FROM sigm.patients p
            JOIN sigm.users u ON u.id=@u
            WHERE p.tenant_id=@t AND p.is_active
              AND lower(p.email) = lower(u.email)
            LIMIT 1", c);
        cmd.Parameters.AddWithValue("@t", tid);
        cmd.Parameters.AddWithValue("@u", uid);
        var found = await cmd.ExecuteScalarAsync(ct);
        if (found is Guid pid) return (c, pid, null);

        await c.DisposeAsync();
        var none = new NpgsqlConnection();
        return (none, null, NotFound(new
        {
            message = "Su usuario no está asociado a un registro de paciente en esta organización. " +
                      "Pida a su clínica que registre el paciente con el mismo correo de su cuenta."
        }));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
