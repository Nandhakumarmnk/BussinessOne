using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.WebApi.Swagger;

/// <summary>
/// Documents the optional X-Business-Id header on business-scoped operations. Auth and platform
/// endpoints (paths under /auth or /admin) are skipped.
/// </summary>
public class BusinessIdHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        if (path.Contains("/auth", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/admin", StringComparison.OrdinalIgnoreCase))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Business-Id",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Active business (tenant scope). Required for business-scoped resources.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}
