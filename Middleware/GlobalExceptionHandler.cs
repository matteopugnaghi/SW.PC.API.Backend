using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SW.PC.API.Backend.Middleware;

/// <summary>
/// 🔒 EU CRA / OWASP / IEC 62443-4-1 — Manejador global de excepciones no controladas (SCG-25/26, v1.4).
///
/// Convierte cualquier excepción no manejada del pipeline HTTP en una respuesta
/// <c>application/problem+json</c> conforme a RFC 7807 (ProblemDetails), evitando:
///   - Fugas de stack-trace al cliente (information disclosure, CWE-209).
///   - Respuestas inconsistentes (texto plano vs JSON) según el tipo de excepción.
///   - Pérdida de trazabilidad: cada excepción se registra con su <c>traceId</c>.
///
/// El detalle técnico (stack-trace + mensaje original) se incluye SOLO en entorno
/// Development; en Production el cliente recibe un mensaje genérico y se conserva
/// el <c>traceId</c> para correlación con logs.
///
/// Excepciones de dominio frecuentes se mapean a sus códigos HTTP semánticos.
/// SignalR tiene su propio pipeline y no pasa por este handler.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Cancelaciones de cliente: no son errores, no se debe responder.
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        var (status, title) = MapException(exception);
        var traceId = httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled exception [{TraceId}] {Method} {Path} → {Status} {Title}",
            traceId,
            httpContext.Request.Method,
            httpContext.Request.Path,
            status,
            title);

        // Si la respuesta ya empezó a enviarse, no podemos sobrescribirla.
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning("Response already started for {TraceId}; cannot write ProblemDetails", traceId);
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;

        // Solo en Development incluimos detalles internos (mensaje + tipo de excepción).
        if (_env.IsDevelopment())
        {
            problem.Detail = exception.Message;
            problem.Extensions["exceptionType"] = exception.GetType().FullName;
        }
        else
        {
            // Producción: mensaje genérico, sin filtrar información sensible.
            problem.Detail = status == StatusCodes.Status500InternalServerError
                ? "Se produjo un error inesperado. Si el problema persiste, contacte con soporte indicando el traceId."
                : title;
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int Status, string Title) MapException(Exception exception) => exception switch
    {
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        ArgumentNullException or ArgumentException or FormatException
            => (StatusCodes.Status400BadRequest, "Bad Request"),
        InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
        NotImplementedException => (StatusCodes.Status501NotImplemented, "Not Implemented"),
        TimeoutException => (StatusCodes.Status504GatewayTimeout, "Gateway Timeout"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
    };
}
