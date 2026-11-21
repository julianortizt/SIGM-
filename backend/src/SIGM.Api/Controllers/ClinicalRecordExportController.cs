using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Api.Services;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

/// <summary>
/// Exportación de la historia clínica del paciente. Devuelve la misma información
/// que se consulta en pantalla (datos del paciente, alergias, diagnósticos,
/// antecedentes, signos vitales, atenciones, notas firmadas y adendas, fórmulas y
/// laboratorio) como JSON para impresión o como PDF descargable.
/// Cada exportación queda registrada en la auditoría (acción EXPORT).
/// </summary>
[ApiController]
[Route("api/v1/patients/{patientId:guid}/clinical-record")]
[Authorize]
public class ClinicalRecordExportController(TenantConnectionFactory db, ITenantContext tenant, AuditService audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("clinical.read")]
    public async Task<IActionResult> Get(Guid patientId, CancellationToken ct)
    {
        var snapshot = await LoadAsync(patientId, ct);
        if (snapshot is null) return NotFound(new { message = "Paciente no encontrado en la organización." });

        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        await audit.LogAsync("patient", patientId, "EXPORT", null, new { format = "json", bytes = bytes.Length, sha256 = hash },
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        Response.Headers["X-Document-Sha256"] = hash;
        return Ok(snapshot);
    }

    [HttpGet("pdf")]
    [RequirePermission("clinical.read")]
    public async Task<IActionResult> Pdf(Guid patientId, CancellationToken ct)
    {
        var s = await LoadAsync(patientId, ct);
        if (s is null) return NotFound(new { message = "Paciente no encontrado en la organización." });

        var generatedAt = DateTimeOffset.Now;
        var builder = new PdfReportBuilder()
            .Header($"{s.TenantName}  ·  Historia clínica {s.Patient.PatientNumber ?? ""}".Trim())
            .Footer("Documento confidencial de uso clínico");

        builder.Title("HISTORIA CLÍNICA");
        builder.Small($"{s.TenantName}  ·  NIT {s.TenantTaxId ?? "—"}  ·  Organización {s.TenantCode ?? "—"}");
        builder.Small($"Generado: {generatedAt:yyyy-MM-dd HH:mm}  ·  Por: {s.GeneratedBy}");
        builder.Rule();

        builder.Heading("1. Identificación del paciente");
        builder.KeyValue("Nombre completo", s.Patient.FullName);
        builder.KeyValue("Documento", $"{s.Patient.DocumentType ?? "—"} {s.Patient.DocumentNumber ?? ""}".Trim());
        builder.KeyValue("Historia / número de paciente", s.Patient.PatientNumber);
        builder.KeyValue("Fecha de nacimiento", s.Patient.BirthDate is DateOnly bd ? $"{bd:yyyy-MM-dd} ({Age(bd)} años)" : "—");
        builder.KeyValue("Sexo", s.Patient.SexAtBirth);
        builder.KeyValue("Teléfono", s.Patient.Phone ?? s.Patient.MobilePhone);
        builder.KeyValue("Correo", s.Patient.Email);
        builder.KeyValue("Dirección", string.Join(", ", new[] { s.Patient.AddressLine, s.Patient.City, s.Patient.Department }.Where(x => !string.IsNullOrWhiteSpace(x))));

        builder.Spacer();
        builder.Heading("2. Alertas clínicas (alergias)");
        if (s.Allergies.Count == 0) builder.Body("Sin alergias registradas.");
        foreach (var a in s.Allergies)
            builder.Body($"- {a.Allergen} ({a.AllergenType}) · severidad {a.Severity} · {a.Status}{(string.IsNullOrWhiteSpace(a.Reaction) ? "" : $" · reacción: {a.Reaction}")}");

        builder.Spacer();
        builder.Heading("3. Diagnósticos");
        if (s.Diagnoses.Count == 0) builder.Body("Sin diagnósticos registrados.");
        foreach (var d in s.Diagnoses)
            builder.Body($"- [{d.Code}] {d.Description} · {d.DiagnosisType}/{d.Certainty} · {d.CreatedAt:yyyy-MM-dd}");

        builder.Spacer();
        builder.Heading("4. Antecedentes");
        if (s.History.Count == 0) builder.Body("Sin antecedentes registrados.");
        foreach (var h in s.History)
            builder.Body($"- {h.HistoryType}: {h.Description}{(h.OnsetDate is DateOnly od ? $" (desde {od:yyyy-MM-dd})" : "")} · {h.Status}");

        builder.Spacer();
        builder.Heading("5. Signos vitales");
        if (s.Vitals.Count == 0) builder.Body("Sin registros de signos vitales.");
        foreach (var v in s.Vitals)
            builder.Body($"- {v.MeasuredAt:yyyy-MM-dd HH:mm} · T {Fmt(v.TemperatureC)} °C · FC {Fmt(v.HeartRate)} · FR {Fmt(v.RespiratoryRate)} · TA {Fmt(v.SystolicBp)}/{Fmt(v.DiastolicBp)} · SpO2 {Fmt(v.OxygenSaturation)}% · Peso {Fmt(v.WeightKg)} kg · Talla {Fmt(v.HeightCm)} cm · IMC {Fmt(v.Bmi)}");

        builder.Spacer();
        builder.Heading("6. Atenciones y notas clínicas");
        if (s.Encounters.Count == 0) builder.Body("Sin atenciones registradas.");
        foreach (var e in s.Encounters)
        {
            builder.Body($"• {e.EncounterDate:yyyy-MM-dd HH:mm} · {e.EncounterType} · {e.Status} · {e.ProfessionalName ?? "Profesional no asignado"}{(string.IsNullOrWhiteSpace(e.SpecialtyName) ? "" : $" · {e.SpecialtyName}")}");
            if (!string.IsNullOrWhiteSpace(e.ChiefComplaint)) builder.Body($"   Motivo: {e.ChiefComplaint}");
            foreach (var n in e.Notes)
            {
                builder.Body($"   Nota {n.NoteType} · {n.Status} · v{n.CurrentVersion}{(n.SignedAt is DateTimeOffset sa ? $" · firmada {sa:yyyy-MM-dd HH:mm}" : "")}");
                builder.Body($"   {n.Content}");
                foreach (var am in n.Amendments)
                    builder.Body($"   Adenda {am.Number}: {am.Reason} — {am.Content}");
            }
            if (e.Notes.Count == 0) builder.Small("   (sin notas registradas)");
        }

        builder.Spacer();
        builder.Heading("7. Fórmulas médicas");
        if (s.Prescriptions.Count == 0) builder.Body("Sin fórmulas registradas.");
        foreach (var p in s.Prescriptions)
        {
            builder.Body($"- {p.Number ?? "Fórmula"} · {p.Status} · {p.IssuedAt:yyyy-MM-dd HH:mm}");
            foreach (var item in p.Items)
                builder.Body($"   · {item.MedicationName} {item.Dose} {item.Frequency} {item.Route} {(item.DurationDays is int dd ? $"por {dd} días" : "")} {(string.IsNullOrWhiteSpace(item.Instructions) ? "" : $"({item.Instructions})")}");
        }

        builder.Spacer();
        builder.Heading("8. Laboratorio");
        if (s.LabOrders.Count == 0) builder.Body("Sin órdenes de laboratorio.");
        foreach (var o in s.LabOrders)
        {
            builder.Body($"- Orden {o.OrderNumber ?? ""} · {o.Status} · {o.OrderedAt:yyyy-MM-dd HH:mm}");
            foreach (var t in o.Tests) builder.Body($"   · {t.Name}{(string.IsNullOrWhiteSpace(t.Result) ? "" : $" = {t.Result} {t.Unit}")}");
        }

        builder.Spacer();
        builder.Rule();
        builder.Small("Este documento se genera a partir del registro clínico electrónico y no reemplaza la firma del profesional. " +
                      "Las notas en borrador se identifican como tales; solo las notas firmadas tienen validez clínica.");

        var bytes = builder.Build();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        await audit.LogAsync("patient", patientId, "EXPORT", null, new { format = "pdf", bytes = bytes.Length, sha256 = hash },
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        var fileName = $"historia-clinica-{Slug(s.Patient.PatientNumber ?? s.Patient.DocumentNumber ?? patientId.ToString())}.pdf";
        Response.Headers["X-Document-Sha256"] = hash;
        return File(bytes, "application/pdf", fileName);
    }

    // ---------------------------------------------------------------------
    // Carga de datos
    // ---------------------------------------------------------------------
    private async Task<ClinicalRecordSnapshot?> LoadAsync(Guid patientId, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return null;
        await using var c = await db.OpenAsync(ct);

        var p = new PatientHeader();
        await using (var cmd = new NpgsqlCommand(@"SELECT patient_number,document_type,document_number,full_name,birth_date,sex_at_birth,email,phone,mobile_phone,address_line,city,department,is_active
                FROM sigm.patients WHERE id=@id AND tenant_id=@t", c))
        {
            cmd.Parameters.AddWithValue("@id", patientId); cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (!await rd.ReadAsync(ct)) return null;
            p.PatientNumber = rd.IsDBNull(0) ? null : rd.GetString(0);
            p.DocumentType = rd.IsDBNull(1) ? null : rd.GetString(1);
            p.DocumentNumber = rd.IsDBNull(2) ? null : rd.GetString(2);
            p.FullName = rd.IsDBNull(3) ? "Paciente" : rd.GetString(3);
            p.BirthDate = rd.IsDBNull(4) ? null : rd.GetFieldValue<DateOnly>(4);
            p.SexAtBirth = rd.IsDBNull(5) ? null : rd.GetString(5);
            p.Email = rd.IsDBNull(6) ? null : rd.GetString(6);
            p.Phone = rd.IsDBNull(7) ? null : rd.GetString(7);
            p.MobilePhone = rd.IsDBNull(8) ? null : rd.GetString(8);
            p.AddressLine = rd.IsDBNull(9) ? null : rd.GetString(9);
            p.City = rd.IsDBNull(10) ? null : rd.GetString(10);
            p.Department = rd.IsDBNull(11) ? null : rd.GetString(11);
            p.IsActive = rd.GetBoolean(12);
        }

        var s = new ClinicalRecordSnapshot { Patient = p };
        await using (var cmd = new NpgsqlCommand("SELECT commercial_name,tenant_code,tax_id FROM sigm.tenants WHERE id=@t", c))
        {
            cmd.Parameters.AddWithValue("@t", tid);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                s.TenantName = rd.IsDBNull(0) ? "SIGM Cloud" : rd.GetString(0);
                s.TenantCode = rd.IsDBNull(1) ? null : rd.GetString(1);
                s.TenantTaxId = rd.IsDBNull(2) ? null : rd.GetString(2);
            }
        }
        s.GeneratedBy = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "Usuario SIGM";

        await using (var cmd = new NpgsqlCommand("SELECT allergen,allergen_type,reaction,severity,status,created_at FROM sigm.allergies WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                s.Allergies.Add(new AllergyRow(rd.GetString(0), rd.GetString(1), rd.IsDBNull(2) ? null : rd.GetString(2), rd.GetString(3), rd.GetString(4), rd.GetFieldValue<DateTimeOffset>(5)));
        }

        await using (var cmd = new NpgsqlCommand(@"SELECT dc.code,dc.description,d.diagnosis_type,d.certainty,d.created_at
                FROM sigm.diagnoses d JOIN sigm.diagnosis_catalog dc ON dc.id=d.diagnosis_catalog_id
                WHERE d.tenant_id=@t AND d.patient_id=@p ORDER BY d.created_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                s.Diagnoses.Add(new DiagnosisRow(rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetString(3), rd.GetFieldValue<DateTimeOffset>(4)));
        }

        await using (var cmd = new NpgsqlCommand("SELECT history_type,description,onset_date,status,created_at FROM sigm.medical_history WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                s.History.Add(new HistoryRow(rd.GetString(0), rd.GetString(1), rd.IsDBNull(2) ? null : rd.GetFieldValue<DateOnly>(2), rd.GetString(3), rd.GetFieldValue<DateTimeOffset>(4)));
        }

        await using (var cmd = new NpgsqlCommand(@"SELECT measured_at,temperature_c,heart_rate,respiratory_rate,systolic_bp,diastolic_bp,oxygen_saturation,weight_kg,height_cm,bmi
                FROM sigm.vital_signs WHERE tenant_id=@t AND patient_id=@p ORDER BY measured_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                s.Vitals.Add(new VitalRow(rd.GetFieldValue<DateTimeOffset>(0), Dec(rd, 1), Int(rd, 2), Int(rd, 3), Int(rd, 4), Int(rd, 5), Dec(rd, 6), Dec(rd, 7), Dec(rd, 8), Dec(rd, 9)));
        }

        // Atenciones con profesional/especialidad y sus notas (versión vigente + adendas).
        var encounters = new List<EncounterRow>();
        await using (var cmd = new NpgsqlCommand(@"SELECT e.id,e.encounter_date,e.encounter_type,e.status,e.chief_complaint,pr.display_name,sp.name
                FROM sigm.encounters e
                LEFT JOIN sigm.professionals pr ON pr.id=e.professional_id
                LEFT JOIN sigm.specialties sp ON sp.id=e.specialty_id
                WHERE e.tenant_id=@t AND e.patient_id=@p ORDER BY e.encounter_date DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                encounters.Add(new EncounterRow(rd.GetGuid(0), rd.GetFieldValue<DateTimeOffset>(1), rd.GetString(2), rd.GetString(3),
                    rd.IsDBNull(4) ? null : rd.GetString(4), rd.IsDBNull(5) ? null : rd.GetString(5), rd.IsDBNull(6) ? null : rd.GetString(6)));
        }

        var notesByEncounter = new Dictionary<Guid, List<ClinicalNoteRow>>();
        await using (var cmd = new NpgsqlCommand(@"SELECT n.id,n.encounter_id,n.note_type,n.status,n.current_version,n.signed_at,v.content,n.created_at
                FROM sigm.clinical_notes n
                LEFT JOIN sigm.clinical_note_versions v ON v.note_id=n.id AND v.tenant_id=n.tenant_id AND v.version_number=n.current_version
                WHERE n.tenant_id=@t AND n.patient_id=@p ORDER BY n.created_at", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var note = new ClinicalNoteRow(rd.GetGuid(0), rd.GetString(2), rd.GetString(3), rd.GetInt32(4),
                    rd.IsDBNull(5) ? null : rd.GetFieldValue<DateTimeOffset>(5), FlattenJson(rd.IsDBNull(6) ? null : rd.GetString(6)),
                    rd.GetFieldValue<DateTimeOffset>(7));
                var encId = rd.GetGuid(1);
                if (!notesByEncounter.TryGetValue(encId, out var list)) notesByEncounter[encId] = list = new List<ClinicalNoteRow>();
                list.Add(note);
            }
        }

        var amendments = new Dictionary<Guid, List<(int Number, string Reason, string Content)>>();
        await using (var cmd = new NpgsqlCommand(@"SELECT a.note_id,a.amendment_number,a.reason,a.content
                FROM sigm.clinical_amendments a
                JOIN sigm.clinical_notes n ON n.id=a.note_id AND n.tenant_id=a.tenant_id
                WHERE a.tenant_id=@t AND n.patient_id=@p ORDER BY a.amendment_number", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var noteId = rd.GetGuid(0);
                if (!amendments.TryGetValue(noteId, out var list)) amendments[noteId] = list = new();
                list.Add((rd.GetInt32(1), rd.GetString(2), FlattenJson(rd.IsDBNull(3) ? null : rd.GetString(3))));
            }
        }
        foreach (var (encId, notes) in notesByEncounter)
        {
            foreach (var n in notes)
                if (amendments.TryGetValue(n.Id, out var list))
                    n.Amendments.AddRange(list.Select(a => new AmendmentRow(a.Number, a.Reason, a.Content)));
            var enc = encounters.FirstOrDefault(e => e.Id == encId);
            if (enc is not null) enc.Notes.AddRange(notes);
        }
        s.Encounters.AddRange(encounters);

        // Fórmulas
        var rx = new List<PrescriptionRow>();
        await using (var cmd = new NpgsqlCommand("SELECT id,prescription_number,status,issued_at FROM sigm.prescriptions WHERE tenant_id=@t AND patient_id=@p ORDER BY issued_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                rx.Add(new PrescriptionRow(rd.GetGuid(0), rd.IsDBNull(1) ? null : rd.GetString(1), rd.GetString(2), rd.GetFieldValue<DateTimeOffset>(3)));
        }
        if (rx.Count > 0)
        {
            await using var cmd = new NpgsqlCommand(@"SELECT prescription_id,medication_name,dose,frequency,route,duration_days,instructions
                    FROM sigm.prescription_items WHERE tenant_id=@t AND prescription_id = ANY(@ids) ORDER BY created_at", c);
            cmd.Parameters.AddWithValue("@t", tid);
            cmd.Parameters.AddWithValue("@ids", rx.Select(r => r.Id).ToArray());
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var target = rx.FirstOrDefault(r => r.Id == rd.GetGuid(0));
                target?.Items.Add(new PrescriptionItemRow(rd.IsDBNull(1) ? "Medicamento" : rd.GetString(1),
                    rd.IsDBNull(2) ? "" : rd.GetString(2), rd.IsDBNull(3) ? "" : rd.GetString(3), rd.IsDBNull(4) ? "" : rd.GetString(4),
                    rd.IsDBNull(5) ? null : rd.GetInt32(5), rd.IsDBNull(6) ? null : rd.GetString(6)));
            }
        }
        s.Prescriptions.AddRange(rx);

        // Laboratorio
        var orders = new List<LabOrderRow>();
        await using (var cmd = new NpgsqlCommand("SELECT id,order_number,status,ordered_at FROM sigm.laboratory_orders WHERE tenant_id=@t AND patient_id=@p ORDER BY ordered_at DESC", c))
        {
            cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@p", patientId);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                orders.Add(new LabOrderRow(rd.GetGuid(0), rd.IsDBNull(1) ? null : rd.GetString(1), rd.GetString(2), rd.GetFieldValue<DateTimeOffset>(3)));
        }
        if (orders.Count > 0)
        {
            await using var cmd = new NpgsqlCommand(@"SELECT oi.laboratory_order_id,t.name,oi.status,r.result_value,t.unit
                    FROM sigm.laboratory_order_items oi
                    LEFT JOIN sigm.laboratory_tests t ON t.id=oi.test_id
                    LEFT JOIN sigm.laboratory_results r ON r.order_item_id=oi.id
                    WHERE oi.tenant_id=@t AND oi.laboratory_order_id = ANY(@ids) ORDER BY t.name", c);
            cmd.Parameters.AddWithValue("@t", tid);
            cmd.Parameters.AddWithValue("@ids", orders.Select(o => o.Id).ToArray());
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var target = orders.FirstOrDefault(o => o.Id == rd.GetGuid(0));
                target?.Tests.Add(new LabTestRow(rd.IsDBNull(1) ? "Prueba" : rd.GetString(1), rd.GetString(2),
                    rd.IsDBNull(3) ? null : rd.GetString(3), rd.IsDBNull(4) ? null : rd.GetString(4)));
            }
        }
        s.LabOrders.AddRange(orders);
        return s;
    }

    internal static string FlattenJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "(sin contenido)";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var parts = new List<string>();
            Flatten(doc.RootElement, "", parts);
            return parts.Count == 0 ? "(sin contenido)" : string.Join(" · ", parts);
        }
        catch { return json.Length > 900 ? json[..900] : json; }
    }

    private static void Flatten(JsonElement e, string prefix, List<string> parts)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in e.EnumerateObject())
                {
                    var label = string.IsNullOrWhiteSpace(prop.Name) ? "" : Humanize(prop.Name);
                    Flatten(prop.Value, string.IsNullOrWhiteSpace(prefix) ? label : $"{prefix}.{label}", parts);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray()) Flatten(item, prefix, parts);
                break;
            case JsonValueKind.String:
                parts.Add(prefix.Length == 0 ? e.GetString()! : $"{prefix}: {e.GetString()}");
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                parts.Add(prefix.Length == 0 ? e.ToString() : $"{prefix}: {e}");
                break;
        }
    }

    private static string Humanize(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        foreach (var ch in name)
        {
            if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static decimal? Dec(NpgsqlDataReader rd, int i) => rd.IsDBNull(i) ? null : rd.GetDecimal(i);
    private static int? Int(NpgsqlDataReader rd, int i) => rd.IsDBNull(i) ? null : rd.GetInt32(i);
    private static string Fmt(decimal? v) => v?.ToString("0.##") ?? "—";
    private static string Fmt(int? v) => v?.ToString() ?? "—";
    private static int Age(DateOnly birth) { var now = DateOnly.FromDateTime(DateTime.Today); var age = now.Year - birth.Year; if (birth > now.AddYears(-age)) age--; return age; }
    private static string Slug(string value) => new(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());

    // ---------------------------------------------------------------------
    // Modelos de salida
    // ---------------------------------------------------------------------
    public sealed class ClinicalRecordSnapshot
    {
        public PatientHeader Patient { get; set; } = new();
        public string TenantName { get; set; } = "SIGM Cloud";
        public string? TenantCode { get; set; }
        public string? TenantTaxId { get; set; }
        public string GeneratedBy { get; set; } = "";
        public List<AllergyRow> Allergies { get; } = new();
        public List<DiagnosisRow> Diagnoses { get; } = new();
        public List<HistoryRow> History { get; } = new();
        public List<VitalRow> Vitals { get; } = new();
        public List<EncounterRow> Encounters { get; } = new();
        public List<PrescriptionRow> Prescriptions { get; } = new();
        public List<LabOrderRow> LabOrders { get; } = new();
    }

    public sealed class PatientHeader
    {
        public string? PatientNumber { get; set; }
        public string? DocumentType { get; set; }
        public string? DocumentNumber { get; set; }
        public string FullName { get; set; } = "";
        public DateOnly? BirthDate { get; set; }
        public string? SexAtBirth { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? MobilePhone { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed record AllergyRow(string Allergen, string AllergenType, string? Reaction, string Severity, string Status, DateTimeOffset CreatedAt);
    public sealed record DiagnosisRow(string Code, string Description, string DiagnosisType, string Certainty, DateTimeOffset CreatedAt);
    public sealed record HistoryRow(string HistoryType, string Description, DateOnly? OnsetDate, string Status, DateTimeOffset CreatedAt);
    public sealed record VitalRow(DateTimeOffset MeasuredAt, decimal? TemperatureC, int? HeartRate, int? RespiratoryRate, int? SystolicBp, int? DiastolicBp, decimal? OxygenSaturation, decimal? WeightKg, decimal? HeightCm, decimal? Bmi);

    public sealed class EncounterRow(Guid id, DateTimeOffset encounterDate, string encounterType, string status, string? chiefComplaint, string? professionalName, string? specialtyName)
    {
        public Guid Id { get; } = id;
        public DateTimeOffset EncounterDate { get; } = encounterDate;
        public string EncounterType { get; } = encounterType;
        public string Status { get; } = status;
        public string? ChiefComplaint { get; } = chiefComplaint;
        public string? ProfessionalName { get; } = professionalName;
        public string? SpecialtyName { get; } = specialtyName;
        public List<ClinicalNoteRow> Notes { get; } = new();
    }

    public sealed class ClinicalNoteRow(Guid id, string noteType, string status, int currentVersion, DateTimeOffset? signedAt, string content, DateTimeOffset createdAt)
    {
        public Guid Id { get; } = id;
        public string NoteType { get; } = noteType;
        public string Status { get; } = status;
        public int CurrentVersion { get; } = currentVersion;
        public DateTimeOffset? SignedAt { get; } = signedAt;
        public string Content { get; } = content;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public List<AmendmentRow> Amendments { get; } = new();
    }

    public sealed record AmendmentRow(int Number, string Reason, string Content);

    public sealed class PrescriptionRow(Guid id, string? number, string status, DateTimeOffset issuedAt)
    {
        public Guid Id { get; } = id;
        public string? Number { get; } = number;
        public string Status { get; } = status;
        public DateTimeOffset IssuedAt { get; } = issuedAt;
        public List<PrescriptionItemRow> Items { get; } = new();
    }

    public sealed record PrescriptionItemRow(string MedicationName, string Dose, string Frequency, string Route, int? DurationDays, string? Instructions);

    public sealed class LabOrderRow(Guid id, string? orderNumber, string status, DateTimeOffset orderedAt)
    {
        public Guid Id { get; } = id;
        public string? OrderNumber { get; } = orderNumber;
        public string Status { get; } = status;
        public DateTimeOffset OrderedAt { get; } = orderedAt;
        public List<LabTestRow> Tests { get; } = new();
    }

    public sealed record LabTestRow(string Name, string Status, string? Result, string? Unit);
}
