using System.Security.Claims;
using SIGM.Api.Services;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenant, UserSessionValidator sessions)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tid"), out var tid))
                tenant.TenantId = tid;
            if (Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var uid))
                tenant.UserId = uid;
            tenant.ProductType = user.FindFirstValue("product_type");
            // La identidad de plataforma la fija el login del superadmin (claim firmado),
            // no la posesión de un permiso: un permiso filtrado por error no debe
            // convertir a un usuario de tenant en administrador de plataforma.
            tenant.IsPlatform = string.Equals(user.FindFirstValue("is_platform_admin"), "true", StringComparison.OrdinalIgnoreCase);
        }
        // La cabecera X-Tenant-Id solo puede cambiar de organizacion a un usuario de
        // plataforma autenticado: antes cualquier cliente anonimo podia fijar el tenant
        // y consultar datos de otra organizacion.
        if (tenant.TenantId is null && user?.Identity?.IsAuthenticated == true
            && user.Claims.Any(c => c.Type == "permission" && c.Value is "platform.tenants.manage" or "platform.database.manage")
            && context.Request.Headers.TryGetValue("X-Tenant-Id", out var h)
            && Guid.TryParse(h.ToString(), out var headerTid))
        {
            tenant.TenantId = headerTid;
        }
        // Revocación de sesiones: si el usuario desactivó MFA, cambió o restableció
        // su contraseña, la versión del token deja de coincidir y la petición se corta.
        if (tenant.UserId is Guid sessionUserId)
        {
            var version = int.TryParse(user?.FindFirstValue("tv"), out var tv) ? tv : 0;
            if (!await sessions.IsCurrentAsync(sessionUserId, version, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"message\":\"Su sesión expiró por un cambio de seguridad. Vuelva a iniciar sesión.\"}");
                return;
            }
        }

        await next(context);
    }
}

public static class TenantResolutionExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) => app.UseMiddleware<TenantResolutionMiddleware>();
}
