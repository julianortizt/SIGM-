using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIGM.Api.Security;
using SIGM.Api.Validation;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Controllers;

[ApiController]
[Route("api/v1/patients")]
[Authorize]
public class PatientsController(TenantConnectionFactory db, ITenantContext tenant) : ControllerBase
{
    public record PatientRequest(string FirstName, string LastName, string? MiddleName, string? SecondLastName, string? DocumentType, string? DocumentNumber, DateOnly? BirthDate, string? SexAtBirth, string? Email, string? Phone, string? MobilePhone, string? AddressLine, string? City, string? Department);

    [HttpGet]
    [RequirePermission("patients.read")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] int take = 50, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        take = Math.Clamp(take, 1, 200);
        page = Math.Max(page, 1);
        var filter = string.IsNullOrWhiteSpace(q) ? string.Empty : " AND (full_name ILIKE @q OR document_number ILIKE @q)";

        await using var c = await db.OpenAsync(ct);

        // Total de coincidencias para que el cliente pueda paginar (expuesto vía CORS).
        await using (var count = new NpgsqlCommand($"SELECT count(*) FROM sigm.patients WHERE tenant_id=@t{filter}", c))
        {
            count.Parameters.AddWithValue("@t", tid);
            if (!string.IsNullOrWhiteSpace(q)) count.Parameters.AddWithValue("@q", $"%{q}%");
            Response.Headers["X-Total-Count"] = Convert.ToInt64(await count.ExecuteScalarAsync(ct)).ToString();
        }

        await using var cmd = new NpgsqlCommand($@"SELECT id, patient_number, document_type, document_number, full_name, first_name, last_name, birth_date, email, phone, is_active
                    FROM sigm.patients WHERE tenant_id=@t{filter}
                    ORDER BY patient_number DESC LIMIT @n OFFSET @o", c);
        cmd.Parameters.AddWithValue("@t", tid);
        cmd.Parameters.AddWithValue("@n", take);
        cmd.Parameters.AddWithValue("@o", (page - 1) * take);
        if (!string.IsNullOrWhiteSpace(q)) cmd.Parameters.AddWithValue("@q", $"%{q}%");
        var list = new List<object>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new { id = rd.GetGuid(0), patientNumber = rd.IsDBNull(1)?null:rd.GetString(1), documentType = rd.IsDBNull(2)?null:rd.GetString(2), documentNumber = rd.IsDBNull(3)?null:rd.GetString(3), fullName = rd.IsDBNull(4)?null:rd.GetString(4), firstName = rd.GetString(5), lastName = rd.GetString(6), birthDate = rd.IsDBNull(7)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(7), email = rd.IsDBNull(8)?null:rd.GetString(8), phone = rd.IsDBNull(9)?null:rd.GetString(9), isActive = rd.GetBoolean(10) });
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("patients.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT id,patient_number,document_type,document_number,first_name,middle_name,last_name,second_last_name,full_name,birth_date,sex_at_birth,email,phone,mobile_phone,address_line,city,department,is_active
            FROM sigm.patients WHERE id=@id AND tenant_id=@t", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return NotFound();
        return Ok(new {
            id = rd.GetGuid(0), patientNumber = rd.IsDBNull(1)?null:rd.GetString(1), documentType = rd.IsDBNull(2)?null:rd.GetString(2), documentNumber = rd.IsDBNull(3)?null:rd.GetString(3),
            firstName = rd.GetString(4), middleName = rd.IsDBNull(5)?null:rd.GetString(5), lastName = rd.GetString(6), secondLastName = rd.IsDBNull(7)?null:rd.GetString(7), fullName = rd.IsDBNull(8)?null:rd.GetString(8),
            birthDate = rd.IsDBNull(9)?(DateOnly?)null:rd.GetFieldValue<DateOnly>(9), sexAtBirth = rd.IsDBNull(10)?null:rd.GetString(10), email = rd.IsDBNull(11)?null:rd.GetString(11), phone = rd.IsDBNull(12)?null:rd.GetString(12),
            mobilePhone = rd.IsDBNull(13)?null:rd.GetString(13), addressLine = rd.IsDBNull(14)?null:rd.GetString(14), city = rd.IsDBNull(15)?null:rd.GetString(15), department = rd.IsDBNull(16)?null:rd.GetString(16), isActive = rd.GetBoolean(17)
        });
    }

    [HttpPost]
    [RequirePermission("patients.create")]
    public async Task<IActionResult> Create([FromBody] PatientRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var invalid = PatientRules.Validate(req.FirstName, req.LastName, req.Email, req.Phone, req.MobilePhone, req.BirthDate, req.DocumentNumber);
        if (invalid is not null) return BadRequest(new { message = invalid });

        var id = Guid.NewGuid();
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        string? number = null;
        try
        {
            await using (var cmd = new NpgsqlCommand(@"INSERT INTO sigm.patients(id,tenant_id,document_type,document_number,first_name,middle_name,last_name,second_last_name,birth_date,sex_at_birth,email,phone,mobile_phone,address_line,city,department,is_active)
                                VALUES(@id,@tid,@dt,@dn,@fn,@mn,@ln,@sln,@bd,@sex,@email,@phone,@mobile,@addr,@city,@dep,true)
                                RETURNING patient_number", c, tx))
            {
                cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@tid", tid);
                cmd.Parameters.AddWithValue("@dt", (object?)PatientRules.Clean(req.DocumentType) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@dn", (object?)PatientRules.Clean(req.DocumentNumber) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fn", req.FirstName.Trim()); cmd.Parameters.AddWithValue("@mn", (object?)PatientRules.Clean(req.MiddleName) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ln", req.LastName.Trim()); cmd.Parameters.AddWithValue("@sln", (object?)PatientRules.Clean(req.SecondLastName) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@bd", (object?)req.BirthDate ?? DBNull.Value); cmd.Parameters.AddWithValue("@sex", PatientRules.NormalizeSex(req.SexAtBirth));
                cmd.Parameters.AddWithValue("@email", (object?)PatientRules.Clean(req.Email) ?? DBNull.Value); cmd.Parameters.AddWithValue("@phone", (object?)PatientRules.Clean(req.Phone) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mobile", (object?)PatientRules.Clean(req.MobilePhone) ?? DBNull.Value); cmd.Parameters.AddWithValue("@addr", (object?)PatientRules.Clean(req.AddressLine) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@city", (object?)PatientRules.Clean(req.City) ?? DBNull.Value); cmd.Parameters.AddWithValue("@dep", (object?)PatientRules.Clean(req.Department) ?? DBNull.Value);
                number = (string?)await cmd.ExecuteScalarAsync(ct);
            }
            await using (var cmd2 = new NpgsqlCommand("INSERT INTO sigm.clinical_records(tenant_id,patient_id,status) VALUES(@tid,@pid,'OPEN') ON CONFLICT DO NOTHING", c, tx))
            { cmd2.Parameters.AddWithValue("@tid", tid); cmd2.Parameters.AddWithValue("@pid", id); await cmd2.ExecuteNonQueryAsync(ct); }
            await tx.CommitAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Conflict(new { message = "Ya existe un paciente con ese tipo y número de documento en la organización." });
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return BadRequest(new { message = "La base de datos rechazó los datos: revise fecha de nacimiento, sexo y tipo de documento." });
        }
        return Ok(new { id, patientNumber = number });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("patients.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PatientRequest req, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        var invalid = PatientRules.Validate(req.FirstName, req.LastName, req.Email, req.Phone, req.MobilePhone, req.BirthDate, req.DocumentNumber);
        if (invalid is not null) return BadRequest(new { message = invalid });

        await using var c = await db.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(@"UPDATE sigm.patients SET
                    document_type=@dt, document_number=@dn, first_name=@fn, middle_name=@mn, last_name=@ln, second_last_name=@sln,
                    birth_date=@bd, sex_at_birth=@sex, email=@email, phone=@phone, mobile_phone=@mobile, address_line=@addr,
                    city=@city, department=@dep, updated_at=now()
                WHERE id=@id AND tenant_id=@t", c);
            cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
            cmd.Parameters.AddWithValue("@dt", (object?)PatientRules.Clean(req.DocumentType) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dn", (object?)PatientRules.Clean(req.DocumentNumber) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fn", req.FirstName.Trim()); cmd.Parameters.AddWithValue("@mn", (object?)PatientRules.Clean(req.MiddleName) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ln", req.LastName.Trim()); cmd.Parameters.AddWithValue("@sln", (object?)PatientRules.Clean(req.SecondLastName) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bd", (object?)req.BirthDate ?? DBNull.Value); cmd.Parameters.AddWithValue("@sex", PatientRules.NormalizeSex(req.SexAtBirth));
            cmd.Parameters.AddWithValue("@email", (object?)PatientRules.Clean(req.Email) ?? DBNull.Value); cmd.Parameters.AddWithValue("@phone", (object?)PatientRules.Clean(req.Phone) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mobile", (object?)PatientRules.Clean(req.MobilePhone) ?? DBNull.Value); cmd.Parameters.AddWithValue("@addr", (object?)PatientRules.Clean(req.AddressLine) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@city", (object?)PatientRules.Clean(req.City) ?? DBNull.Value); cmd.Parameters.AddWithValue("@dep", (object?)PatientRules.Clean(req.Department) ?? DBNull.Value);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0) return NotFound(new { message = "Paciente no encontrado en la organización." });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { message = "Ya existe otro paciente con ese tipo y número de documento." });
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { message = "La base de datos rechazó los datos: revise fecha de nacimiento, sexo y tipo de documento." });
        }
        return Ok(new { id });
    }

    /// <summary>Baja lógica: el paciente deja de estar activo pero conserva su historia.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("patients.delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (tenant.TenantId is not Guid tid) return BadRequest(new { message = "Tenant no resuelto." });
        await using var c = await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE sigm.patients SET is_active=false, updated_at=now() WHERE id=@id AND tenant_id=@t AND is_active", c);
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@t", tid);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound(new { message = "Paciente no encontrado o ya inactivo." });
        return NoContent();
    }
}
