namespace SIGM.Shared.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? ProductType { get; set; }
    public bool IsPlatform { get; set; }
}
