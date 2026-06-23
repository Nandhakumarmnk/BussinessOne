using System.Reflection;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using MediatR;

namespace ERP.Application.Common.Behaviors;

/// <summary>
/// Enforces the <see cref="HasPermissionAttribute"/> on a request (if present) against the
/// caller's per-business permission set. The API also gates UI actions; this is the server backstop.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationBehavior(ICurrentUser currentUser) => _currentUser = currentUser;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var attribute = request.GetType().GetCustomAttribute<HasPermissionAttribute>();
        if (attribute is not null)
        {
            if (!_currentUser.IsAuthenticated)
                throw new ForbiddenException("auth.unauthenticated", "Authentication required.");

            if (!await _currentUser.HasPermissionAsync(attribute.Permission, ct))
                throw new ForbiddenException("auth.forbidden", $"Missing permission: {attribute.Permission}");
        }

        return await next();
    }
}
