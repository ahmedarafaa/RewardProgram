using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditShopOwnerRequestValidator : AbstractValidator<AdminEditShopOwnerRequest>
{
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB

    public AdminEditShopOwnerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);

        RuleFor(x => x.CityId)
            .NotEmpty().WithMessage(L["CityId.NotEmpty"]);

        When(x => x.StoreName != null || x.ShopImage != null, () =>
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage(L["StoreName.NotEmpty"])
                .MinimumLength(5).WithMessage(L["StoreName.MinLength"])
                .MaximumLength(150).WithMessage(L["StoreName.MaxLength"]);

            RuleFor(x => x.ShortAddress)
                .Matches(@"^[A-Za-z]{4}\d{4}$").WithMessage(L["ShortAddress.InvalidFormat"])
                .When(x => !string.IsNullOrEmpty(x.ShortAddress));

            RuleFor(x => x.ShopImage)
                .Must(BeValidImageType).WithMessage(L["ShopImage.InvalidType"])
                .Must(BeValidImageSize).WithMessage(L["ShopImage.TooLarge"])
                .When(x => x.ShopImage != null);

            RuleFor(x => x.NationalAddress!)
                .NotNull().WithMessage(L["NationalAddress.NotNull"])
                .SetValidator(new NationalAddressDtoValidator(L), "Default");
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
