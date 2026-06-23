using System.Text.Json;
using ERP.Domain.Exceptions;
using AppValidationException = ERP.Application.Common.Exceptions.ValidationException;
using ForbiddenException = ERP.Application.Common.Exceptions.ForbiddenException;
using NotFoundException = ERP.Application.Common.Exceptions.NotFoundException;

namespace ERP.WebApi.Middleware;

/// <summary>Converts exceptions into the standard error envelope (see docs/07).</summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteError(context, ex);
        }
    }

    private async Task WriteError(HttpContext context, Exception ex)
    {
        var correlationId = CorrelationIdMiddleware.Get(context);

        var (status, code, message, details) = ex switch
        {
            AppValidationException v => (422, "validation.failed", "One or more validation errors occurred.",
                                         (object?)v.Errors),
            ForbiddenException f     => (403, f.Code, f.Message, null),
            NotFoundException n      => (404, "resource.not_found", n.Message, null),
            DomainException d        => (422, d.Code, d.Message, null),
            _                        => (500, "server.error", "An unexpected error occurred.", null)
        };

        if (status >= 500)
            _logger.LogError(ex, "Unhandled exception ({CorrelationId})", correlationId);
        else
            _logger.LogWarning("Handled error {Code} ({CorrelationId}): {Message}", code, correlationId, ex.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var payload = new
        {
            error = new
            {
                code,
                message,
                status,
                details,
                correlationId,
                timestamp = DateTime.UtcNow
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
