using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Products.Validators;

public class AdminEditProductRequestValidator : AbstractValidator<AdminEditProductRequest>
{
    public AdminEditProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم المنتج مطلوب")
            .MaximumLength(200).WithMessage("اسم المنتج يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage("كود المنتج مطلوب")
            .MaximumLength(50).WithMessage("كود المنتج يجب ألا يتجاوز 50 حرف");

        RuleFor(x => x.PointValue)
            .GreaterThan(0).WithMessage("قيمة النقاط يجب أن تكون أكبر من صفر");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("السعر يجب أن يكون صفر أو أكثر");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("المجموعة يجب ألا تتجاوز 100 حرف")
            .When(x => x.Category is not null);
    }
}
