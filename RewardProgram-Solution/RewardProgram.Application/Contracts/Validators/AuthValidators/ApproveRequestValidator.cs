using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class ApproveRequestValidator : AbstractValidator<ApproveRequest>
{
    public ApproveRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(L["UserId.NotEmpty"]);
    }
}