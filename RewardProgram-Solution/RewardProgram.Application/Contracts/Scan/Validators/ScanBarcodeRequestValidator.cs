using FluentValidation;

namespace RewardProgram.Application.Contracts.Scan.Validators;

public class ScanBarcodeRequestValidator : AbstractValidator<ScanBarcodeRequest>
{
    public ScanBarcodeRequestValidator()
    {
        RuleFor(x => x.BarcodeCode)
            .NotEmpty().WithMessage("كود الباركود مطلوب")
            .Length(12).WithMessage("كود الباركود يجب أن يكون 12 حرف");
    }
}
