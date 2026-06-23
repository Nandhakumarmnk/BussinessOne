using ERP.Application.Common.Models;
using ERP.Application.Features.Auth.Login;
using MediatR;

namespace ERP.Application.Features.Auth.Register;

/// <summary>
/// Self-service onboarding: creates a tenant + owner user (and optionally a first business),
/// then returns an authenticated session.
/// </summary>
public record RegisterCommand(
    string TenantName,
    string FullName,
    string Mobile,
    string? Email,
    string Password,
    string? FirstBusinessName,
    string? FirstBusinessTypeCode) : IRequest<Result<LoginResponse>>;
