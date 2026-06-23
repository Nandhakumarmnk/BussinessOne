using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using MediatR;

namespace ERP.Application.Features.Auth.Login;

public record LoginCommand(string MobileOrEmail, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    UserSummary User,
    IReadOnlyList<MembershipDto> Memberships);
