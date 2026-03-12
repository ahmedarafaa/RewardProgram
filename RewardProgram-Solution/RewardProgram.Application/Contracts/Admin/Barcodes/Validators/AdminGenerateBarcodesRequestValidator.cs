using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Barcodes.Validators;

public class AdminGenerateBarcodesRequestValidator : AbstractValidator<AdminGenerateBarcodesRequest>
{
    public AdminGenerateBarcodesRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("معرف المنتج مطلوب");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1000).WithMessage("الكمية يجب أن تكون بين 1 و 1000");
    }
}
