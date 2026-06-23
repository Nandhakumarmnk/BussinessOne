using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using MediatR;

namespace ERP.Application.Features.Auth.Me;

public class GetMeQueryHandler : IRequestHandler<GetMeQuery, Result<MeResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;

    public GetMeQueryHandler(ICurrentUser currentUser, IIdentityQueries identity)
    {
        _currentUser = currentUser;
        _identity = identity;
    }

    public async Task<Result<MeResponse>> Handle(GetMeQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException("auth.unauthenticated", "Authentication required.");

        var user = await _identity.GetUserAsync(_currentUser.UserId.Value, ct)
                   ?? throw new NotFoundException("User not found.");

        var memberships = await _identity.GetMembershipsAsync(user.Id, ct);

        var response = new MeResponse(
            new UserSummary(user.Id, user.FullName, user.Mobile, user.Email, user.IsSuperAdmin),
            memberships);

        return Result<MeResponse>.Ok(response);
    }
}
