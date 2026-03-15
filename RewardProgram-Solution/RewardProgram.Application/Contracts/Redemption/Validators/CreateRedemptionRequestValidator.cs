using FluentValidation;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Redemption.Validators;

public class CreateRedemptionRequestValidator : AbstractValidator<CreateRedemptionRequest>
{
    public CreateRedemptionRequestValidator()
    {
        RuleFor(x => x.Method)
            .IsInEnum().WithMessage("طريقة الاسترداد غير صالحة");

        RuleFor(x => x.PointsAmount)
            .GreaterThanOrEqualTo(1000).WithMessage("الحد الأدنى للاسترداد 1000 نقطة");

        // Bank transfer fields — required only for BankTransfer method
        When(x => x.Method == RedemptionMethod.BankTransfer, () =>
        {
            RuleFor(x => x.Iban)
                .NotEmpty().WithMessage("رقم الآيبان مطلوب")
                .Matches(@"^SA\d{22}$").WithMessage("رقم الآيبان يجب أن يبدأ بـ SA متبوعاً بـ 22 رقم");

            RuleFor(x => x.BankName)
                .NotEmpty().WithMessage("اسم البنك مطلوب")
                .MaximumLength(200).WithMessage("اسم البنك يجب ألا يتجاوز 200 حرف");

            RuleFor(x => x.AccountHolderName)
                .NotEmpty().WithMessage("اسم صاحب الحساب مطلوب")
                .MaximumLength(200).WithMessage("اسم صاحب الحساب يجب ألا يتجاوز 200 حرف");
        });
    }
}
