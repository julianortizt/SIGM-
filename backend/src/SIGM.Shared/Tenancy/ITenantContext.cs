namespace SIGM.Shared.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? ProductType { get; }
    bool IsPlatform { get; }
}
