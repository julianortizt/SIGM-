using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SIGM.Api.Middleware;
using SIGM.Api.Security;
using SIGM.Api.Services;
using SIGM.Api.Workers;
using SIGM.Infrastructure.Persistence;
using SIGM.Shared.Abstractions;
using SIGM.Shared.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("dev", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Total-Count"));
    opt.AddPolicy("production", p =>
    {
      var allowedOrigins = builder.Configuration.GetSection("CORS:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3001", "http://localhost:3002", "http://localhost:3003" };
      p.WithOrigins(allowedOrigins)
       .AllowAnyHeader()
       .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
       .WithExposedHeaders("X-Total-Count")
       .AllowCredentials();
    });
});

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TenantConnectionFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<UserSessionValidator>();
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DatabaseBackupService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<ReminderSender>();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddHostedService<ScheduledBackupWorker>();

var conn = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(conn))
{
    if (builder.Environment.IsDevelopment())
        conn = "Host=localhost;Port=5432;Database=sigm_cloud_dev;Username=postgres;Password=postgres";
    else
        throw new InvalidOperationException("ConnectionStrings:Default no está configurada. Use variables de entorno o un gestor de secretos.");
}
if (!builder.Environment.IsDevelopment()
    && (conn.Contains("postgres:postgres", StringComparison.OrdinalIgnoreCase)
        || conn.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || conn.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "La cadena de conexión de producción apunta a credenciales/host de desarrollo. Configure una cadena real antes de desplegar.");
}
builder.Services.AddDbContext<SigmDbContext>((sp, opt) =>
{
    opt.UseNpgsql(conn);
    opt.AddInterceptors(sp.GetRequiredService<TenantDbConnectionInterceptor>());
});
builder.Services.AddScoped<TenantDbConnectionInterceptor>();

var jwtKey = builder.Configuration["Jwt:Key"];
const string DevOnly = "DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+";
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment())
        jwtKey = DevOnly;
    else
        throw new InvalidOperationException("Jwt:Key no está configurada. Establece una clave secreta de al menos 32 caracteres en producción.");
}
else if (jwtKey == DevOnly)
{
    throw new InvalidOperationException("Jwt:Key aún contiene el valor por defecto DEV_ONLY_CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32+. Reemplázalo por una clave secreta propia antes de ponerlo en producción.");
}
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres para firmar tokens de forma segura.");

var key = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "sigm-cloud",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "sigm-cloud-clients",
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                // Un token de plataforma sin tenant es valido, pero un usuario normal
                // siempre debe traer su organizacion resuelta por el login.
                return Task.CompletedTask;
            }
        };
    });

// Limite de intentos por IP para autenticacion y MFA (mitiga fuerza bruta).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.AddPolicy("mfa", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "300";
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Demasiados intentos. Espere unos minutos antes de volver a intentarlo.\"}", token);
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("dev");
}
else
{
    app.UseCors("production");
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();
app.UseModuleEnforcement();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "sigm-api", utc = DateTimeOffset.UtcNow }));
app.MapGet("/health/ready", async (TenantConnectionFactory db, CancellationToken ct) =>
{
    try
    {
        await using var c = await db.OpenAsync(ct, platform: true);
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", c);
        await cmd.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready", database = true, utc = DateTimeOffset.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unavailable", database = false, error = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
