using FluentValidation;
using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterShopOwnerRequestValidator : AbstractValidator<RegisterShopOwnerRequest>
{
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    public RegisterShopOwnerRequestValidator()
    {
        RuleFor(x => x.StoreName)
           .NotEmpty().WithMessage("اسم المتجر مطلوب")
           .MinimumLength(2).WithMessage("اسم المتجر يجب أن يكون حرفين على الأقل")
           .MaximumLength(150).WithMessage("اسم المتجر يجب ألا يتجاوز 150 حرف");

        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(2).WithMessage("الاسم يجب أن يكون حرفين على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.VAT)
            .NotEmpty().WithMessage("الرقم الضريبي مطلوب")
            .Length(15).WithMessage("الرقم الضريبي يجب أن يتكون من 15 رقم")
            .Matches(@"^3\d{13}3$").WithMessage("الرقم الضريبي يجب أن يبدأ وينتهي بالرقم 3");

        RuleFor(x => x.CRN)
            .NotEmpty().WithMessage("رقم السجل التجاري مطلوب")
            .Length(10).WithMessage("رقم السجل التجاري يجب أن يتكون من 10 أرقام")
            .Matches(@"^\d{10}$").WithMessage("رقم السجل التجاري يجب أن يتكون من أرقام فقط");

        RuleFor(x => x.ShopImage)
            .NotNull().WithMessage("صورة المحل مطلوبة")
            .Must(BeValidImageType).WithMessage("صورة المحل يجب أن تكون بصيغة JPG أو PNG")
            .Must(BeValidImageSize).WithMessage("حجم الصورة يجب ألا يتجاوز 5 ميجابايت");

        RuleFor(x => x.NationalAddress)
        .NotNull().WithMessage("العنوان الوطني مطلوب")
        .SetValidator(new NationalAddressDtoValidator(), "Default");
    }
    #region Helpers
    private bool BeValidImageType(IFormFile? file)
    {
        if (file == null) return false;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return _allowedImageExtensions.Contains(extension);
    }

    private bool BeValidImageSize(IFormFile? file)
    {
        if (file == null) return false;
        return file.Length <= MaxImageSize;
    }

    #endregion
}
