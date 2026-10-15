using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, "La solicitud no es válida.", validation.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "El recurso cambió.", "Actualiza la información e inténtalo nuevamente."),
            DbUpdateException => (StatusCodes.Status409Conflict, "No se pudo guardar el recurso.", "La información cambió o ya existe. Actualiza e inténtalo nuevamente."),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "Servicio externo no disponible.", "No se pudo completar la operación. Inténtalo nuevamente."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "Operación no disponible.", "El servicio requerido no está configurado o disponible."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno.", "Ocurrió un error inesperado.")
        };

        if (status >= 500)
            logger.LogError(exception, "Error no controlado durante {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Error de dominio durante {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}"
            }
        });
        return true;
    }
}
