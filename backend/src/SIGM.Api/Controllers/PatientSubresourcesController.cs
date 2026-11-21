using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:guid}")]
[Authorize]
public class PatientSubresourcesController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    // CONTACTS
    [HttpGet("contacts")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListContacts(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,contact_type,name,relationship,phone,email,is_primary,is_emergency,notes,created_at FROM sigm.patient_contacts WHERE tenant_id=@t AND patient_id=@p ORDER BY is_primary DESC, created_at DESC", ("@p", patientId)));
    [HttpPost("contacts")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddContact(Guid patientId, [FromBody] ContactReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_contacts(id,tenant_id,patient_id,contact_type,name,relationship,phone,email,is_primary,is_emergency,notes) VALUES(gen_random_uuid(),@t,@p,@type,@name,@rel,@phone,@email,@prim,@emerg,@notes)", ("@p", patientId), ("@type", req.ContactType ?? "OTHER"), ("@name", req.Name), ("@rel", (object?)req.Relationship ?? DBNull.Value), ("@phone", (object?)req.Phone ?? DBNull.Value), ("@email", (object?)req.Email ?? DBNull.Value), ("@prim", req.IsPrimary), ("@emerg", req.IsEmergency), ("@notes", (object?)req.Notes ?? DBNull.Value));
    [HttpDelete("contacts/{id:guid}")][RequirePermission("patients.update")]
    public async Task<IActionResult> DeleteContact(Guid patientId, Guid id, CancellationToken ct) => await ExecDelete(ct, "sigm.patient_contacts", id, ("@p", patientId));

    // ADDRESSES
    [HttpGet("addresses")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListAddresses(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,address_type,line1,line2,city,department,country,postal_code,is_primary,created_at FROM sigm.patient_addresses WHERE tenant_id=@t AND patient_id=@p ORDER BY is_primary DESC", ("@p", patientId)));
    [HttpPost("addresses")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddAddress(Guid patientId, [FromBody] AddressReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_addresses(id,tenant_id,patient_id,address_type,line1,line2,city,department,country,postal_code,is_primary) VALUES(gen_random_uuid(),@t,@p,@type,@l1,@l2,@city,@dep,@country,@pc,@prim)", ("@p", patientId), ("@type", req.AddressType ?? "HOME"), ("@l1", req.Line1), ("@l2", (object?)req.Line2 ?? DBNull.Value), ("@city", (object?)req.City ?? DBNull.Value), ("@dep", (object?)req.Department ?? DBNull.Value), ("@country", req.Country ?? "Colombia"), ("@pc", (object?)req.PostalCode ?? DBNull.Value), ("@prim", req.IsPrimary));
    [HttpDelete("addresses/{id:guid}")][RequirePermission("patients.update")]
    public async Task<IActionResult> DeleteAddress(Guid patientId, Guid id, CancellationToken ct) => await ExecDelete(ct, "sigm.patient_addresses", id, ("@p", patientId));

    // IDENTIFIERS
    [HttpGet("identifiers")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListIdentifiers(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,identifier_type,identifier_value,issuing_authority,is_primary,valid_from,valid_to,created_at FROM sigm.patient_identifiers WHERE tenant_id=@t AND patient_id=@p", ("@p", patientId)));
    [HttpPost("identifiers")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddIdentifier(Guid patientId, [FromBody] IdentifierReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_identifiers(id,tenant_id,patient_id,identifier_type,identifier_value,issuing_authority,is_primary,valid_from,valid_to) VALUES(gen_random_uuid(),@t,@p,@type,@val,@iss,@prim,@vf,@vt)", ("@p", patientId), ("@type", req.IdentifierType), ("@val", req.IdentifierValue), ("@iss", (object?)req.IssuingAuthority ?? DBNull.Value), ("@prim", req.IsPrimary), ("@vf", (object?)req.ValidFrom ?? DBNull.Value), ("@vt", (object?)req.ValidTo ?? DBNull.Value));

    // CONSENTS
    [HttpGet("consents")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListConsents(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,consent_type,consent_version,granted,granted_at,revoked_at,document_id,notes,created_at FROM sigm.patient_consents WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", ("@p", patientId)));
    [HttpPost("consents")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddConsent(Guid patientId, [FromBody] ConsentReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_consents(id,tenant_id,patient_id,consent_type,consent_version,granted,granted_at,document_id,recorded_by,notes) VALUES(gen_random_uuid(),@t,@p,@type,@ver,@g,CASE WHEN @g THEN now() ELSE NULL END,@doc,@u,@notes)", ("@p", patientId), ("@type", req.ConsentType), ("@ver", req.ConsentVersion ?? "1"), ("@g", req.Granted), ("@doc", (object?)req.DocumentId ?? DBNull.Value), ("@u", (object?)tenant.UserId ?? DBNull.Value), ("@notes", (object?)req.Notes ?? DBNull.Value));

    // INSURANCES
    [HttpGet("insurances")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListInsurances(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,insurer_id,affiliate_number,plan_name,is_primary,valid_from,valid_to,status,created_at FROM sigm.patient_insurances WHERE tenant_id=@t AND patient_id=@p ORDER BY is_primary DESC", ("@p", patientId)));
    [HttpPost("insurances")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddInsurance(Guid patientId, [FromBody] InsuranceReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_insurances(id,tenant_id,patient_id,insurer_id,affiliate_number,plan_name,is_primary,valid_from,valid_to,status) VALUES(gen_random_uuid(),@t,@p,@ins,@aff,@plan,@prim,@vf,@vt,@st)", ("@p", patientId), ("@ins", req.InsurerId), ("@aff", (object?)req.AffiliateNumber ?? DBNull.Value), ("@plan", (object?)req.PlanName ?? DBNull.Value), ("@prim", req.IsPrimary), ("@vf", (object?)req.ValidFrom ?? DBNull.Value), ("@vt", (object?)req.ValidTo ?? DBNull.Value), ("@st", req.Status ?? "ACTIVE"));

    // MEDICATIONS
    [HttpGet("medications")][RequirePermission("clinical.read")]
    public async Task<IActionResult> ListMedications(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,medication_catalog_id,medication_name,dose,frequency,route,start_date,end_date,status,notes,created_at FROM sigm.patient_medications WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", ("@p", patientId)));
    [HttpPost("medications")][RequirePermission("clinical.update")]
    public async Task<IActionResult> AddMedication(Guid patientId, [FromBody] MedicationReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_medications(id,tenant_id,patient_id,medication_catalog_id,medication_name,dose,frequency,route,start_date,end_date,status,notes,recorded_by) VALUES(gen_random_uuid(),@t,@p,@mid,@name,@dose,@freq,@route,@sd,@ed,@st,@notes,@u)", ("@p", patientId), ("@mid", (object?)req.MedicationCatalogId ?? DBNull.Value), ("@name", req.MedicationName), ("@dose", (object?)req.Dose ?? DBNull.Value), ("@freq", (object?)req.Frequency ?? DBNull.Value), ("@route", (object?)req.Route ?? DBNull.Value), ("@sd", (object?)req.StartDate ?? DBNull.Value), ("@ed", (object?)req.EndDate ?? DBNull.Value), ("@st", req.Status ?? "ACTIVE"), ("@notes", (object?)req.Notes ?? DBNull.Value), ("@u", (object?)tenant.UserId ?? DBNull.Value));

    // RELATIVES
    [HttpGet("relatives")][RequirePermission("patients.read")]
    public async Task<IActionResult> ListRelatives(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,relative_patient_id,name,relationship,document_type,document_number,phone,is_guardian,created_at FROM sigm.patient_relatives WHERE tenant_id=@t AND patient_id=@p", ("@p", patientId)));
    [HttpPost("relatives")][RequirePermission("patients.update")]
    public async Task<IActionResult> AddRelative(Guid patientId, [FromBody] RelativeReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.patient_relatives(id,tenant_id,patient_id,relative_patient_id,name,relationship,document_type,document_number,phone,is_guardian) VALUES(gen_random_uuid(),@t,@p,@rp,@name,@rel,@dt,@dn,@phone,@g)", ("@p", patientId), ("@rp", (object?)req.RelativePatientId ?? DBNull.Value), ("@name", req.Name), ("@rel", (object?)req.Relationship ?? DBNull.Value), ("@dt", (object?)req.DocumentType ?? DBNull.Value), ("@dn", (object?)req.DocumentNumber ?? DBNull.Value), ("@phone", (object?)req.Phone ?? DBNull.Value), ("@g", req.IsGuardian));

    // FAMILY HISTORY
    [HttpGet("family-history")][RequirePermission("clinical.read")]
    public async Task<IActionResult> ListFamily(Guid patientId, CancellationToken ct) => Ok(await Query(ct, "SELECT id,relative_relationship,condition_description,diagnosis_catalog_id,age_at_onset,status,created_at FROM sigm.family_history WHERE tenant_id=@t AND patient_id=@p ORDER BY created_at DESC", ("@p", patientId)));
    [HttpPost("family-history")][RequirePermission("clinical.history.manage")]
    public async Task<IActionResult> AddFamily(Guid patientId, [FromBody] FamilyReq req, CancellationToken ct) => await Exec(ct, @"INSERT INTO sigm.family_history(id,tenant_id,patient_id,relative_relationship,condition_description,diagnosis_catalog_id,age_at_onset,status,recorded_by) VALUES(gen_random_uuid(),@t,@p,@rel,@cond,@dx,@age,@st,@u)", ("@p", patientId), ("@rel", req.RelativeRelationship), ("@cond", req.ConditionDescription), ("@dx", (object?)req.DiagnosisCatalogId ?? DBNull.Value), ("@age", (object?)req.AgeAtOnset ?? DBNull.Value), ("@st", req.Status ?? "ACTIVE"), ("@u", (object?)tenant.UserId ?? DBNull.Value));

    // helpers
    private async Task<List<Dictionary<string, object?>>> Query(CancellationToken ct, string sql, params (string, object)[] extra)
    {
        if (tenant.TenantId is not Guid tid) throw new InvalidOperationException("Tenant no resuelto");
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@t", tid);
        foreach (var (k, v) in extra) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        var list = new List<Dictionary<string, object?>>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct)) { var d = new Dictionary<string, object?>(); for (int i = 0; i < rd.FieldCount; i++) d[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i); list.Add(d); }
        return list;
    }
    private async Task<IActionResult> Exec(CancellationToken ct, string sql, params (string, object)[] ps)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@t", tid);
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct); return Ok();
    }
    private async Task<IActionResult> ExecDelete(CancellationToken ct, string table, Guid id, params (string, object)[] extra)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest();
        await using var c = await db.OpenAsync(ct);
        var extraSql = extra.Length > 0 ? " AND " + string.Join(" AND ", extra.Select(x => $"{x.Item1.TrimStart('@')}={x.Item1}")) : "";
        // extraSql built safely from known params
        await using var cmd = new NpgsqlCommand($"DELETE FROM {table} WHERE id=@id AND tenant_id=@t" + (extra.Any(x=>x.Item1=="@p")?" AND patient_id=@p":""), c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        foreach (var (k, v) in extra) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound() : NoContent();
    }

    public record ContactReq(string? ContactType, string Name, string? Relationship, string? Phone, string? Email, bool IsPrimary, bool IsEmergency, string? Notes);
    public record AddressReq(string? AddressType, string Line1, string? Line2, string? City, string? Department, string? Country, string? PostalCode, bool IsPrimary);
    public record IdentifierReq(string IdentifierType, string IdentifierValue, string? IssuingAuthority, bool IsPrimary, DateOnly? ValidFrom, DateOnly? ValidTo);
    public record ConsentReq(string ConsentType, string? ConsentVersion, bool Granted, Guid? DocumentId, string? Notes);
    public record InsuranceReq(Guid InsurerId, string? AffiliateNumber, string? PlanName, bool IsPrimary, DateOnly? ValidFrom, DateOnly? ValidTo, string? Status);
    public record MedicationReq(Guid? MedicationCatalogId, string MedicationName, string? Dose, string? Frequency, string? Route, DateOnly? StartDate, DateOnly? EndDate, string? Status, string? Notes);
    public record RelativeReq(Guid? RelativePatientId, string Name, string? Relationship, string? DocumentType, string? DocumentNumber, string? Phone, bool IsGuardian);
    public record FamilyReq(string RelativeRelationship, string ConditionDescription, Guid? DiagnosisCatalogId, int? AgeAtOnset, string? Status);
}
