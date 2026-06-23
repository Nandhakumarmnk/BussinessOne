using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using MediatR;

namespace ERP.Application.Features.Auth.Me;

public record GetMeQuery : IRequest<Result<MeResponse>>;

public record MeResponse(UserSummary User, IReadOnlyList<MembershipDto> Memberships);
