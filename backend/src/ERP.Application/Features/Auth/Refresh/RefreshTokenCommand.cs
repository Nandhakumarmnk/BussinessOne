using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Login;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Auth.Refresh;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}
