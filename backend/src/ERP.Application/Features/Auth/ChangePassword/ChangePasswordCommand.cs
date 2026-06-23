using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Auth.ChangePassword;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;

    public ChangePasswordCommandHandler(
        ICurrentUser currentUser, IIdentityQueries identity, IPasswordHasher passwordHasher,
        IUnitOfWork uow, IDateTime clock)
    {
        _currentUser = currentUser;
        _identity = identity;
        _passwordHasher = passwordHasher;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId)
            throw new ForbiddenException("auth.unauthenticated", "Authentication required.");

        var user = await _identity.GetUserAsync(userId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Fail("auth.invalid_credentials", "Current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        _uow.Repository<ERP.Domain.Identity.User>().Update(user);
        await _uow.SaveChangesAsync(ct);

        // Force re-authentication everywhere else.
        await _identity.RevokeAllRefreshTokensAsync(userId, _clock.UtcNow, ct);

        return Result.Ok();
    }
}
