namespace ERP.WebApi.Middleware;

/// <summary>Adds baseline security response headers (see docs/10 §6).</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["X-Permitted-Cross-Domain-Policies"] = "none";
            return Task.CompletedTask;
        });
        return _next(context);
    }
}
