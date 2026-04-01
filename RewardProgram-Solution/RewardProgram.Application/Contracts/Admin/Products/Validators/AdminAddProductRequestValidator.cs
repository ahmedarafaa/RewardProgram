using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Products.Validators;

public class AdminAddProductRequestValidator : AbstractValidator<AdminAddProductRequest>
{
    public AdminAddProductRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Product.Name.NotEmpty"])
            .MaximumLength(200).WithMessage(L["Product.Name.MaxLength"]);

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage(L["Product.ProductCode.NotEmpty"])
            .MaximumLength(50).WithMessage(L["Product.ProductCode.MaxLength"]);

        RuleFor(x => x.PointValue)
            .GreaterThan(0).WithMessage(L["Product.PointValue.GreaterThanZero"]);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage(L["Product.Price.NotNegative"]);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage(L["Product.Category.MaxLength"])
            .When(x => x.Category is not null);
    }
}
