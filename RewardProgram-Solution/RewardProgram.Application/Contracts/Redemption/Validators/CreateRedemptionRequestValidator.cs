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

        // Minimum threshold is enforced in the service using RewardSettings.MinimumRedemptionPoints
        // (admin-configurable). Here we only guard against zero/negative values.
        RuleFor(x => x.PointsAmount)
            .GreaterThan(0).WithMessage(L["Redemption.PointsAmount.Minimum"]);

        // Bank transfer fields — required only for BankTransfer method
        When(x => x.Method == RedemptionMethod.BankTransfer, () =>
        {
            RuleFor(x => x.Iban)
                .NotEmpty().WithMessage(L["Redemption.Iban.NotEmpty"])
                .Matches(@"^SA\d{22}$").WithMessage(L["Redemption.Iban.InvalidFormat"]);

            RuleFor(x => x.AccountNumber)
                .NotEmpty().WithMessage(L["Redemption.AccountNumber.NotEmpty"])
                .MaximumLength(50).WithMessage(L["Redemption.AccountNumber.MaxLength"]);

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage(L["Redemption.Address.NotEmpty"])
                .MaximumLength(200).WithMessage(L["Redemption.Address.MaxLength"]);

            RuleFor(x => x.SwiftCode)
                .NotEmpty().WithMessage(L["Redemption.SwiftCode.NotEmpty"])
                .MaximumLength(11).WithMessage(L["Redemption.SwiftCode.MaxLength"])
                .Matches(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$").WithMessage(L["Redemption.SwiftCode.InvalidFormat"]);

            RuleFor(x => x.AccountName)
                .NotEmpty().WithMessage(L["Redemption.AccountName.NotEmpty"])
                .MaximumLength(200).WithMessage(L["Redemption.AccountName.MaxLength"]);
        });
    }
}
