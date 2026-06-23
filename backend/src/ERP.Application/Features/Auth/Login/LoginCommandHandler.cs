using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Common;
using MediatR;

namespace ERP.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private const string InvalidCredentials = "auth.invalid_credentials";

    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthSessionService _session;

    public LoginCommandHandler(IIdentityQueries identity, IPasswordHasher passwordHasher, AuthSessionService session)
    {
        _identity = identity;
        _passwordHasher = passwordHasher;
        _session = session;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _identity.FindByLoginAsync(request.MobileOrEmail.Trim(), ct);
        if (user is null || !user.IsActive)
            return Result<LoginResponse>.Fail(InvalidCredentials, "Invalid credentials.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Fail(InvalidCredentials, "Invalid credentials.");

        var response = await _session.IssueAsync(user, ct);
        return Result<LoginResponse>.Ok(response);
    }
}
