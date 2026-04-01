using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RejectRequestValidator : AbstractValidator<RejectRequest>
{
    public RejectRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(L["UserId.NotEmpty"]);

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(L["Reason.NotEmpty"])
            .MaximumLength(500).WithMessage(L["Reason.MaxLength"]);
    }
}
