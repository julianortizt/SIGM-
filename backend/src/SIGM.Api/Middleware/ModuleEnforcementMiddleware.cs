using Npgsql;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Tenancy;

namespace SIGM.Api.Middleware;

/// <summary>
/// Aplica los interruptores de módulos configurados por el superadmin de
/// plataforma (sigm.tenant_features): si un módulo está desactivado para el
/// tenant, las rutas de su API responden 403 con un mensaje claro.
/// Módulos sin fila en tenant_features están habilitados por defecto.
/// </summary>
public sealed class ModuleEnforcementMiddleware(RequestDelegate next)
{
    // Prefijo de ruta (sin /api/v1/) -> feature_code en tenant_features
    internal static readonly (string Prefix, string Feature)[] Map =
    [
        ("appointments", "appointments"),
        ("waiting-list", "waiting_list"),
        ("waiting", "waiting_list"),
        ("reminders", "reminders"),
        ("prescriptions", "prescriptions"),
        ("laboratory", "laboratory"),
        ("billing", "billing"),
        ("insurers", "insurers"),
        ("documents", "documents"),
        ("interoperability", "interoperability"),
        ("audit", "audit"),
        ("notes", "clinical"),
        ("encounters", "clinical"),
        ("clinical", "clinical"),
        ("clinical-extended", "clinical"),
        ("rips", "interoperability"),
    ];

    public async Task InvokeAsync(HttpContext context, TenantContext tenant, TenantConnectionFactory db)
    {
        var path = context.Request.Path.Value ?? "";
        if (tenant.TenantId is Guid tid && path.StartsWith("/api/v1/"))
        {
            var rest = path["/api/v1/".Length..];
            var seg = rest.Split('/')[0];
            var feature = Map.FirstOrDefault(m => m.Prefix == seg).Feature;
            if (feature is not null)
            {
                await using var c = await db.OpenAsync(context.RequestAborted, platform: true);
                await using var cmd = new NpgsqlCommand("SELECT enabled FROM sigm.tenant_features WHERE tenant_id=@t AND feature_code=@f", c);
                cmd.Parameters.AddWithValue("@t", tid); cmd.Parameters.AddWithValue("@f", feature);
                var res = await cmd.ExecuteScalarAsync(context.RequestAborted);
                if (res is bool enabled && !enabled)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"message\":\"Este módulo está desactivado para su organización. Contacte al soporte de SIGM si lo necesita.\"}", context.RequestAborted);
                    return;
                }
            }
        }
        await next(context);
    }
}

public static class ModuleEnforcementExtensions
{
    public static IApplicationBuilder UseModuleEnforcement(this IApplicationBuilder app) => app.UseMiddleware<ModuleEnforcementMiddleware>();
}
