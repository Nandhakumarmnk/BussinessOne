using ERP.Application.Features.Auth.ChangePassword;
using ERP.Application.Features.Auth.Login;
using ERP.Application.Features.Auth.Logout;
using ERP.Application.Features.Auth.Refresh;
using ERP.Application.Features.Auth.Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.WebApi.Controllers;

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    /// <summary>Authenticate with mobile/email + password. Returns access + refresh tokens and memberships.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        => FromResult(await Mediator.Send(new LoginCommand(request.MobileOrEmail, request.Password), ct));

    /// <summary>Self-service onboarding: creates a tenant + owner (and optional first business), then logs in.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand request, CancellationToken ct)
        => FromResult(await Mediator.Send(request, ct));

    /// <summary>Exchange a valid refresh token for a new access + (rotated) refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand request, CancellationToken ct)
        => FromResult(await Mediator.Send(request, ct));

    /// <summary>Revoke a refresh token (sign out this device).</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand request, CancellationToken ct)
        => FromResult(await Mediator.Send(request, ct));

    /// <summary>Change the authenticated user's password (revokes other sessions).</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand request, CancellationToken ct)
        => FromResult(await Mediator.Send(request, ct));
}

public record LoginRequest(string MobileOrEmail, string Password);
