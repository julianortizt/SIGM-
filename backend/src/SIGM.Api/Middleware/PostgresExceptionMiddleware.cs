using Npgsql;

namespace SIGM.Api.Middleware;

/// <summary>
/// Traduce los errores de integridad de PostgreSQL a respuestas 4xx con un mensaje
/// accionable. Antes, una restricción UNIQUE/FK/CHECK llegaba al cliente como un 500
/// sin cuerpo (por ejemplo, dos pólizas principales para un mismo paciente).
/// </summary>
public sealed class PostgresExceptionMiddleware(RequestDelegate next, ILogger<PostgresExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (PostgresException ex) when (ex.SqlState is not null)
        {
            logger.LogWarning(ex, "Error de PostgreSQL {SqlState} en {Path}", ex.SqlState, context.Request.Path);

            var (status, message) = ex.SqlState switch
            {
                "23505" => (StatusCodes.Status409Conflict,
                    $"El registro ya existe o entra en conflicto con otro existente{(string.IsNullOrWhiteSpace(ex.ConstraintName) ? "." : $" ({ex.ConstraintName}).")}"),
                "23503" => (StatusCodes.Status400BadRequest,
                    "No se puede guardar el registro porque hace referencia a otro dato que no existe."),
                "23514" => (StatusCodes.Status400BadRequest,
                    "Uno de los valores enviados no cumple las reglas permitidas."),
                "23502" => (StatusCodes.Status400BadRequest,
                    "Falta un campo obligatorio."),
                "22001" => (StatusCodes.Status400BadRequest,
                    "Uno de los textos enviados es demasiado largo."),
                "22P02" or "22007" => (StatusCodes.Status400BadRequest,
                    "El formato de uno de los valores enviados no es válido."),
                "23P01" => (StatusCodes.Status409Conflict,
                    "El registro se traslapa con otro existente (horario o período)."),
                _ => (StatusCodes.Status500InternalServerError, "Error de base de datos.")
            };

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = status;
                context.Response.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status,
                    message,
                    code = ex.SqlState,
                    constraint = ex.ConstraintName,
                    traceId = context.TraceIdentifier
                });
                await context.Response.WriteAsync(payload);
            }
        }
    }
}

public static class PostgresExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UsePostgresExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<PostgresExceptionMiddleware>();
}
