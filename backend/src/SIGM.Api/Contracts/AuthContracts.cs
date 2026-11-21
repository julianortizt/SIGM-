namespace SIGM.Api.Contracts;

// TenantId se acepta como UUID o como código de organización (tenant_code).
public record LoginRequest(string Email, string Password, string? TenantId = null, string? MfaCode = null);
// TenantCode/TenantName permiten mostrar la organización al usuario sin exponer el UUID.
public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, Guid? TenantId, string FullName, string[] Permissions, string? TenantCode = null, string? TenantName = null);
public record VerifyMfaRequest(string Code);
public record DisableMfaRequest(string? Password, string? Code);
public record RegisterOrganizationRequest(string TenantCode, string LegalName, string CommercialName, string? TaxId, string ProductType, string AdminEmail, string AdminFullName, string AdminPassword, Guid[] SpecialtyIds, Guid? PlanId);
public record OrganizationCreatedResponse(Guid TenantId, Guid UserId, string ProductType);
public record EnableSpecialtiesRequest(Guid[] SpecialtyIds);
public record CreateTenantUserRequest(string Email, string FullName, string? Phone, string? Password, string[] RoleCodes);
