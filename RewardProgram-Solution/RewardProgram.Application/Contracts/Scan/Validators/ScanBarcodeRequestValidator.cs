using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Scan.Validators;

public class ScanBarcodeRequestValidator : AbstractValidator<ScanBarcodeRequest>
{
    public ScanBarcodeRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.BarcodeCode)
            .NotEmpty().WithMessage(L["Barcode.Code.NotEmpty"])
            .Length(12).WithMessage(L["Barcode.Code.Length"]);
    }
}
