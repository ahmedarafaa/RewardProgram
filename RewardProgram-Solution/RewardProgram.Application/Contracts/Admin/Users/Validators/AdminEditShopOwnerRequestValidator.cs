using FluentValidation;
using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditShopOwnerRequestValidator : AbstractValidator<AdminEditShopOwnerRequest>
{
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB

    public AdminEditShopOwnerRequestValidator()
    {
        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(3).WithMessage("الاسم يجب أن يكون 3 أحرف على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف")
            .Matches(@"^[\p{L}\s]+$").WithMessage("الاسم يجب أن يحتوي على أحرف فقط");

        RuleFor(x => x.CityId)
            .NotEmpty().WithMessage("المدينة مطلوبة");

        When(x => x.StoreName != null || x.ShopImage != null, () =>
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage("اسم المتجر مطلوب")
                .MinimumLength(5).WithMessage("اسم المتجر يجب أن يكون 5 أحرف على الأقل")
                .MaximumLength(150).WithMessage("اسم المتجر يجب ألا يتجاوز 150 حرف");

            RuleFor(x => x.ShortAddress)
                .Matches(@"^[A-Za-z]{4}\d{4}$").WithMessage("العنوان المختصر يجب أن يتكون من 4 أحرف و4 أرقام")
                .When(x => !string.IsNullOrEmpty(x.ShortAddress));

            RuleFor(x => x.ShopImage)
                .Must(BeValidImageType).WithMessage("صورة المحل يجب أن تكون بصيغة JPG أو PNG")
                .Must(BeValidImageSize).WithMessage("حجم الصورة يجب ألا يتجاوز 5 ميجابايت")
                .When(x => x.ShopImage != null);

            RuleFor(x => x.NationalAddress!)
                .NotNull().WithMessage("العنوان الوطني مطلوب")
                .SetValidator(new NationalAddressDtoValidator(), "Default");
        });
    }

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
}
