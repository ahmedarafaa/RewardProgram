using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption.Validators;

public class CreateRedemptionRequestValidator : AbstractValidator<CreateRedemptionRequest>
{
    public CreateRedemptionRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Method)
            .IsInEnum().WithMessage(L["Redemption.Method.Invalid"]);

        RuleFor(x => x.PointsAmount)
            .GreaterThanOrEqualTo(1000).WithMessage(L["Redemption.PointsAmount.Minimum"]);

        // Bank transfer fields — required only for BankTransfer method
        When(x => x.Method == RedemptionMethod.BankTransfer, () =>
        {
            RuleFor(x => x.Iban)
                .NotEmpty().WithMessage(L["Redemption.Iban.NotEmpty"])
                .Matches(@"^SA\d{22}$").WithMessage(L["Redemption.Iban.InvalidFormat"]);

            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage(L["Redemption.BankName.NotEmpty"])
                .MaximumLength(200).WithMessage(L["Redemption.BankName.MaxLength"]);

            RuleFor(x => x.AccountHolderName)
                .NotEmpty().WithMessage(L["Redemption.AccountHolderName.NotEmpty"])
                .MaximumLength(200).WithMessage(L["Redemption.AccountHolderName.MaxLength"]);
        });
    }
}
