using FluentValidation;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterShopOwnerRequestValidator : AbstractValidator<RegisterShopOwnerRequest>
{
    public RegisterShopOwnerRequestValidator()
    {
        RuleFor(x => x.OwnerName)
        .NotEmpty().WithMessage("اسم المالك مطلوب")
        .MaximumLength(100).WithMessage("اسم المالك يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage("اسم المحل مطلوب")
                .MaximumLength(150).WithMessage("اسم المحل يجب ألا يتجاوز 150 حرف");

        RuleFor(x => x.VAT)
            .NotEmpty().WithMessage("الرقم الضريبي مطلوب")
            .Matches(@"^3\d{13}3$")
            .WithMessage("الرقم الضريبي يجب أن يتكون من 15 رقم ويبدأ وينتهي بالرقم 3");

        RuleFor(x => x.CRN)
                .NotEmpty().WithMessage("السجل التجاري مطلوب")
                .Length(10).WithMessage("السجل التجاري يجب أن يتكون من 10 أرقام");

        RuleFor(x => x.ShopImageUrl)
                .NotEmpty().WithMessage("صورة المحل مطلوبة");

        RuleFor(x => x.NationalAddress)
        .NotNull().WithMessage("العنوان الوطني مطلوب")
        .SetValidator(new NationalAddressDtoValidator(), "Default");
    }

}
