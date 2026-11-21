using System.Data;
using Microsoft.EntityFrameworkCore;
using SIGM.Shared.Tenancy;

namespace SIGM.Infrastructure.Persistence;

public class SigmDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public SigmDbContext(DbContextOptions<SigmDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<TenantRow> Tenants => Set<TenantRow>();
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<UserTenantRow> UserTenants => Set<UserTenantRow>();
    public DbSet<RoleRow> Roles => Set<RoleRow>();
    public DbSet<PermissionRow> Permissions => Set<PermissionRow>();
    public DbSet<UserRoleRow> UserRoles => Set<UserRoleRow>();
    public DbSet<RolePermissionRow> RolePermissions => Set<RolePermissionRow>();
    public DbSet<SpecialtyRow> Specialties => Set<SpecialtyRow>();
    public DbSet<TenantSpecialtyRow> TenantSpecialties => Set<TenantSpecialtyRow>();
    public DbSet<OrganizationRow> Organizations => Set<OrganizationRow>();
    public DbSet<PlanRow> Plans => Set<PlanRow>();
    public DbSet<TenantSubscriptionRow> TenantSubscriptions => Set<TenantSubscriptionRow>();
    public DbSet<SiteRow> Sites => Set<SiteRow>();
    public DbSet<MedicalOfficeRow> MedicalOffices => Set<MedicalOfficeRow>();
    public DbSet<ProfessionalRow> Professionals => Set<ProfessionalRow>();
    public DbSet<ProfessionalSpecialtyRow> ProfessionalSpecialties => Set<ProfessionalSpecialtyRow>();
    public DbSet<PatientRow> Patients => Set<PatientRow>();
    public DbSet<ClinicalRecordRow> ClinicalRecords => Set<ClinicalRecordRow>();
    public DbSet<EncounterRow> Encounters => Set<EncounterRow>();
    public DbSet<ClinicalNoteRow> ClinicalNotes => Set<ClinicalNoteRow>();
    public DbSet<ClinicalNoteVersionRow> ClinicalNoteVersions => Set<ClinicalNoteVersionRow>();
    public DbSet<AppointmentTypeRow> AppointmentTypes => Set<AppointmentTypeRow>();
    public DbSet<AppointmentRow> Appointments => Set<AppointmentRow>();
    public DbSet<ScheduleRow> Schedules => Set<ScheduleRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sigm");

        modelBuilder.Entity<TenantRow>(e => { e.ToTable("tenants"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantCode).HasColumnName("tenant_code"); e.Property(x => x.LegalName).HasColumnName("legal_name"); e.Property(x => x.CommercialName).HasColumnName("commercial_name"); e.Property(x => x.TaxId).HasColumnName("tax_id"); e.Property(x => x.ProductType).HasColumnName("product_type"); e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.Timezone).HasColumnName("timezone"); e.Property(x => x.Locale).HasColumnName("locale"); e.Property(x => x.Currency).HasColumnName("currency"); });
        modelBuilder.Entity<OrganizationRow>(e => { e.ToTable("organizations"); e.HasKey(x => x.TenantId); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.LegalName).HasColumnName("legal_name"); e.Property(x => x.CommercialName).HasColumnName("commercial_name"); e.Property(x => x.TaxId).HasColumnName("tax_id"); e.Property(x => x.ProductType).HasColumnName("product_type"); e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.Email).HasColumnName("email"); e.Property(x => x.Phone).HasColumnName("phone"); });
        modelBuilder.Entity<UserRow>(e => { e.ToTable("users"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.Email).HasColumnName("email"); e.Property(x => x.PasswordHash).HasColumnName("password_hash"); e.Property(x => x.FullName).HasColumnName("full_name"); e.Property(x => x.Phone).HasColumnName("phone"); e.Property(x => x.IsActive).HasColumnName("is_active"); e.Property(x => x.MfaEnabled).HasColumnName("mfa_enabled");
        e.Property(x => x.MfaSecretEncrypted).HasColumnName("mfa_secret_encrypted"); });
        modelBuilder.Entity<UserTenantRow>(e => { e.ToTable("user_tenants"); e.HasKey(x => new { x.TenantId, x.UserId }); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.UserId).HasColumnName("user_id"); e.Property(x => x.Status).HasColumnName("status"); });
        modelBuilder.Entity<RoleRow>(e => { e.ToTable("roles"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.IsSystem).HasColumnName("is_system"); });
        modelBuilder.Entity<PermissionRow>(e => { e.ToTable("permissions"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.Module).HasColumnName("module"); e.Property(x => x.Name).HasColumnName("name"); });
        modelBuilder.Entity<RolePermissionRow>(e => { e.ToTable("role_permissions"); e.HasKey(x => new { x.RoleId, x.PermissionId }); e.Property(x => x.RoleId).HasColumnName("role_id"); e.Property(x => x.PermissionId).HasColumnName("permission_id"); });
        modelBuilder.Entity<UserRoleRow>(e => { e.ToTable("user_roles"); e.HasKey(x => new { x.TenantId, x.UserId, x.RoleId }); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.UserId).HasColumnName("user_id"); e.Property(x => x.RoleId).HasColumnName("role_id"); });
        modelBuilder.Entity<SpecialtyRow>(e => { e.ToTable("specialties"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<TenantSpecialtyRow>(e => { e.ToTable("tenant_specialties"); e.HasKey(x => new { x.TenantId, x.SpecialtyId }); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.SpecialtyId).HasColumnName("specialty_id"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<PlanRow>(e => { e.ToTable("plans"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.ProductType).HasColumnName("product_type"); e.Property(x => x.Price).HasColumnName("price"); e.Property(x => x.BillingPeriod).HasColumnName("billing_period"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<TenantSubscriptionRow>(e => { e.ToTable("tenant_subscriptions"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.PlanId).HasColumnName("plan_id"); e.Property(x => x.Status).HasColumnName("status"); });

        modelBuilder.Entity<SiteRow>(e => { e.ToTable("sites"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.City).HasColumnName("city"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<MedicalOfficeRow>(e => { e.ToTable("medical_offices"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.Code).HasColumnName("code"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<ProfessionalRow>(e => { e.ToTable("professionals"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.UserId).HasColumnName("user_id"); e.Property(x => x.DisplayName).HasColumnName("display_name"); e.Property(x => x.ProfessionalType).HasColumnName("professional_type"); e.Property(x => x.ProfessionalLicense).HasColumnName("professional_license"); e.Property(x => x.PrimarySiteId).HasColumnName("primary_site_id"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<ProfessionalSpecialtyRow>(e => { e.ToTable("professional_specialties"); e.HasKey(x => new { x.TenantId, x.ProfessionalId, x.SpecialtyId }); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.ProfessionalId).HasColumnName("professional_id"); e.Property(x => x.SpecialtyId).HasColumnName("specialty_id"); e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.IsActive).HasColumnName("is_active"); e.Property(x => x.VerificationStatus).HasColumnName("verification_status"); e.Property(x => x.ProfessionalLicense).HasColumnName("professional_license"); });
        modelBuilder.Entity<PatientRow>(e => { e.ToTable("patients"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.PatientNumber).HasColumnName("patient_number"); e.Property(x => x.DocumentType).HasColumnName("document_type"); e.Property(x => x.DocumentNumber).HasColumnName("document_number"); e.Property(x => x.FirstName).HasColumnName("first_name"); e.Property(x => x.LastName).HasColumnName("last_name"); e.Property(x => x.FullName).HasColumnName("full_name"); e.Property(x => x.BirthDate).HasColumnName("birth_date"); e.Property(x => x.Email).HasColumnName("email"); e.Property(x => x.Phone).HasColumnName("phone"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<ClinicalRecordRow>(e => { e.ToTable("clinical_records"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.PatientId).HasColumnName("patient_id"); e.Property(x => x.Status).HasColumnName("status"); });
        modelBuilder.Entity<EncounterRow>(e => { e.ToTable("encounters"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.PatientId).HasColumnName("patient_id"); e.Property(x => x.ProfessionalId).HasColumnName("professional_id"); e.Property(x => x.SpecialtyId).HasColumnName("specialty_id"); e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.AppointmentId).HasColumnName("appointment_id"); e.Property(x => x.EncounterType).HasColumnName("encounter_type"); e.Property(x => x.EncounterDate).HasColumnName("encounter_date"); e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.ChiefComplaint).HasColumnName("chief_complaint"); });
        modelBuilder.Entity<ClinicalNoteRow>(e => { e.ToTable("clinical_notes"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.EncounterId).HasColumnName("encounter_id"); e.Property(x => x.PatientId).HasColumnName("patient_id"); e.Property(x => x.AuthorUserId).HasColumnName("author_user_id"); e.Property(x => x.NoteType).HasColumnName("note_type"); e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.CurrentVersion).HasColumnName("current_version"); e.Property(x => x.SignedAt).HasColumnName("signed_at"); });
        modelBuilder.Entity<ClinicalNoteVersionRow>(e => { e.ToTable("clinical_note_versions"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.NoteId).HasColumnName("note_id"); e.Property(x => x.VersionNumber).HasColumnName("version_number"); e.Property(x => x.Content).HasColumnName("content").HasColumnType("jsonb"); e.Property(x => x.AuthorUserId).HasColumnName("author_user_id"); e.Property(x => x.SignedAt).HasColumnName("signed_at"); });
        modelBuilder.Entity<AppointmentTypeRow>(e => { e.ToTable("appointment_types"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.DefaultDurationMinutes).HasColumnName("default_duration_minutes"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
        modelBuilder.Entity<AppointmentRow>(e => { e.ToTable("appointments"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.AppointmentNumber).HasColumnName("appointment_number"); e.Property(x => x.PatientId).HasColumnName("patient_id"); e.Property(x => x.ProfessionalId).HasColumnName("professional_id"); e.Property(x => x.SpecialtyId).HasColumnName("specialty_id"); e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.MedicalOfficeId).HasColumnName("medical_office_id"); e.Property(x => x.StartsAt).HasColumnName("starts_at"); e.Property(x => x.EndsAt).HasColumnName("ends_at"); e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.Reason).HasColumnName("reason"); });
        modelBuilder.Entity<ScheduleRow>(e => { e.ToTable("schedules"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.ProfessionalId).HasColumnName("professional_id"); e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.Weekday).HasColumnName("weekday"); e.Property(x => x.StartTime).HasColumnName("start_time"); e.Property(x => x.EndTime).HasColumnName("end_time"); e.Property(x => x.IsActive).HasColumnName("is_active"); });
    }

    public async Task OpenTenantConnectionAsync(CancellationToken ct = default)
    {
        var conn = Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
        if (_tenant.TenantId is Guid tid)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
            var p = cmd.CreateParameter(); p.ParameterName = "@tid"; p.Value = tid.ToString(); cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        if (_tenant.UserId is Guid uid)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.user_id', @uid, true);";
            var p = cmd.CreateParameter(); p.ParameterName = "@uid"; p.Value = uid.ToString(); cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}

public class TenantRow { public Guid Id { get; set; } public string TenantCode { get; set; } = ""; public string LegalName { get; set; } = ""; public string CommercialName { get; set; } = ""; public string? TaxId { get; set; } public string ProductType { get; set; } = ""; public string Status { get; set; } = ""; public string Timezone { get; set; } = ""; public string Locale { get; set; } = ""; public string Currency { get; set; } = ""; }
public class OrganizationRow { public Guid TenantId { get; set; } public string LegalName { get; set; } = ""; public string? CommercialName { get; set; } public string? TaxId { get; set; } public string ProductType { get; set; } = ""; public string Status { get; set; } = ""; public string? Email { get; set; } public string? Phone { get; set; } }
public class UserRow { public Guid Id { get; set; } public string Email { get; set; } = ""; public string? PasswordHash { get; set; } public string FullName { get; set; } = ""; public string? Phone { get; set; } public bool IsActive { get; set; } public bool MfaEnabled { get; set; } public string? MfaSecretEncrypted { get; set; } }
public class UserTenantRow { public Guid TenantId { get; set; } public Guid UserId { get; set; } public string Status { get; set; } = ""; }
public class RoleRow { public Guid Id { get; set; } public Guid? TenantId { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsSystem { get; set; } }
public class PermissionRow { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Module { get; set; } = ""; public string Name { get; set; } = ""; }
public class RolePermissionRow { public Guid RoleId { get; set; } public Guid PermissionId { get; set; } }
public class UserRoleRow { public Guid TenantId { get; set; } public Guid UserId { get; set; } public Guid RoleId { get; set; } }
public class SpecialtyRow { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } }
public class TenantSpecialtyRow { public Guid TenantId { get; set; } public Guid SpecialtyId { get; set; } public bool IsActive { get; set; } }
public class PlanRow { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string ProductType { get; set; } = ""; public decimal Price { get; set; } public string BillingPeriod { get; set; } = ""; public bool IsActive { get; set; } }
public class TenantSubscriptionRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid PlanId { get; set; } public string Status { get; set; } = ""; }

public class SiteRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public string Name { get; set; } = ""; public string? Code { get; set; } public string? City { get; set; } public bool IsActive { get; set; } }
public class MedicalOfficeRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid SiteId { get; set; } public string Name { get; set; } = ""; public string? Code { get; set; } public bool IsActive { get; set; } }
public class ProfessionalRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid UserId { get; set; } public string DisplayName { get; set; } = ""; public string ProfessionalType { get; set; } = ""; public string? ProfessionalLicense { get; set; } public Guid? PrimarySiteId { get; set; } public bool IsActive { get; set; } }
public class ProfessionalSpecialtyRow { public Guid TenantId { get; set; } public Guid ProfessionalId { get; set; } public Guid SpecialtyId { get; set; } public Guid? SiteId { get; set; } public bool IsActive { get; set; } public string VerificationStatus { get; set; } = "PENDING"; public string? ProfessionalLicense { get; set; } }
public class PatientRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public string PatientNumber { get; set; } = ""; public string? DocumentType { get; set; } public string? DocumentNumber { get; set; } public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public string? FullName { get; set; } public DateOnly? BirthDate { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public bool IsActive { get; set; } }
public class ClinicalRecordRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid PatientId { get; set; } public string Status { get; set; } = "OPEN"; }
public class EncounterRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid PatientId { get; set; } public Guid ProfessionalId { get; set; } public Guid SpecialtyId { get; set; } public Guid SiteId { get; set; } public Guid? AppointmentId { get; set; } public string EncounterType { get; set; } = "CONSULTATION"; public DateTimeOffset EncounterDate { get; set; } public string Status { get; set; } = "IN_PROGRESS"; public string? ChiefComplaint { get; set; } }
public class ClinicalNoteRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid EncounterId { get; set; } public Guid PatientId { get; set; } public Guid AuthorUserId { get; set; } public string NoteType { get; set; } = ""; public string Status { get; set; } = "DRAFT"; public int CurrentVersion { get; set; } public DateTimeOffset? SignedAt { get; set; } }
public class ClinicalNoteVersionRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid NoteId { get; set; } public int VersionNumber { get; set; } public string Content { get; set; } = "{}"; public Guid AuthorUserId { get; set; } public DateTimeOffset? SignedAt { get; set; } }
public class AppointmentTypeRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public string Name { get; set; } = ""; public int DefaultDurationMinutes { get; set; } public bool IsActive { get; set; } }
public class AppointmentRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public string AppointmentNumber { get; set; } = ""; public Guid PatientId { get; set; } public Guid ProfessionalId { get; set; } public Guid SpecialtyId { get; set; } public Guid SiteId { get; set; } public Guid? MedicalOfficeId { get; set; } public DateTimeOffset StartsAt { get; set; } public DateTimeOffset EndsAt { get; set; } public string Status { get; set; } = "PENDING"; public string? Reason { get; set; } }
public class ScheduleRow { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid ProfessionalId { get; set; } public Guid? SiteId { get; set; } public short Weekday { get; set; } public TimeOnly StartTime { get; set; } public TimeOnly EndTime { get; set; } public bool IsActive { get; set; } }
