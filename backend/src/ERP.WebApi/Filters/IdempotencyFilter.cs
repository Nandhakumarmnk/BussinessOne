using System.Text.Json;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.WebApi.Filters;

/// <summary>
/// Makes POST writes safely retryable: when a request carries an <c>Idempotency-Key</c> header,
/// the first success is cached (per business) and any replay returns the original response without
/// re-applying. This is what lets the mobile offline outbox resend queued writes after reconnect.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IIdempotencyStore _store;
    private readonly ITenantContext _tenant;

    public IdempotencyFilter(IIdempotencyStore store, ITenantContext tenant)
    {
        _store = store;
        _tenant = tenant;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].FirstOrDefault();
        var ct = http.RequestAborted;

        if (!HttpMethods.IsPost(http.Request.Method)
            || string.IsNullOrWhiteSpace(key)
            || _tenant.BusinessId is not { } businessId)
        {
            await next();
            return;
        }

        var existing = await _store.GetAsync(businessId, key, ct);
        if (existing is not null)
        {
            context.Result = new ContentResult
            {
                StatusCode = existing.StatusCode,
                Content = existing.ResponseBody,
                ContentType = "application/json"
            };
            return;
        }

        var executed = await next();
        if (executed.Result is ObjectResult obj)
        {
            var status = obj.StatusCode ?? StatusCodes.Status200OK;
            if (status is >= 200 and < 300)
                await _store.SaveAsync(businessId, key, status, JsonSerializer.Serialize(obj.Value, Json), ct);
        }
    }
}
