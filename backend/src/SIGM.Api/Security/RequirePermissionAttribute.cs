using Microsoft.AspNetCore.Authorization;

namespace SIGM.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PolicyPrefix + permission;
    }

    public string Permission { get; }
}
