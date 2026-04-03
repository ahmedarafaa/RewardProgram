using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterSellerRequestValidator : AbstractValidator<RegisterSellerRequest>
{
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB

    public RegisterSellerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.VerificationToken)
            .NotEmpty().WithMessage(L["VerificationToken.NotEmpty"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage(L["MobileNumber.NotEmpty"])
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage(L["MobileNumber.InvalidFormat"]);

        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage(L["CustomerCode.NotEmpty"])
            .MaximumLength(50).WithMessage(L["CustomerCode.MaxLength"]);

        // Conditional shop data rules — only required when ShopData doesn't exist (validated in service)
        When(x => x.StoreName != null || x.VAT != null || x.CRN != null || x.ShopImage != null, () =>
        {
            RuleFor(x => x.StoreName)
                .NotEmpty().WithMessage(L["StoreName.NotEmpty"])
                .MinimumLength(5).WithMessage(L["StoreName.MinLength"])
                .MaximumLength(150).WithMessage(L["StoreName.MaxLength"]);

            RuleFor(x => x.VAT)
                .NotEmpty().WithMessage(L["VAT.NotEmpty"])
                .Length(15).WithMessage(L["VAT.Length"])
                .Matches(@"^3\d{13}3$").WithMessage(L["VAT.InvalidFormat"]);

            RuleFor(x => x.CRN)
                .NotEmpty().WithMessage(L["CRN.NotEmpty"])
                .Length(10).WithMessage(L["CRN.Length"])
                .Matches(@"^\d{10}$").WithMessage(L["CRN.InvalidFormat"]);

            RuleFor(x => x.ShortAddress)
                .NotEmpty().WithMessage(L["ShortAddress.NotEmpty"])
                .Matches(@"^[A-Za-z]{4}\d{4}$").WithMessage(L["ShortAddress.InvalidFormat"]);

            RuleFor(x => x.ShopImage)
                .NotNull().WithMessage(L["ShopImage.NotNull"])
                .Must(BeValidImageType).WithMessage(L["ShopImage.InvalidType"])
                .Must(BeValidImageSize).WithMessage(L["ShopImage.TooLarge"]);

            RuleFor(x => x.CityId)
                .NotEmpty().WithMessage(L["CityId.NotEmpty"]);

            RuleFor(x => x.NationalAddress!)
                .NotNull().WithMessage(L["NationalAddress.NotNull"])
                .SetValidator(new NationalAddressDtoValidator(L), "Default");
        });
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
