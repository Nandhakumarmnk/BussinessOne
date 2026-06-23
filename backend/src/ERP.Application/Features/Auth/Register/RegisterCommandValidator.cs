using ERP.Domain.Enums;
using FluentValidation;

namespace ERP.Application.Features.Auth.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);

        RuleFor(x => x.FirstBusinessName).MaximumLength(150);
        RuleFor(x => x.FirstBusinessTypeCode)
            .Must(code => code is null || BusinessTypeCodes.All.Contains(code))
            .WithMessage("Unknown business type code.");
        RuleFor(x => x.FirstBusinessTypeCode)
            .NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.FirstBusinessName))
            .WithMessage("Business type is required when creating a first business.");
    }
}
