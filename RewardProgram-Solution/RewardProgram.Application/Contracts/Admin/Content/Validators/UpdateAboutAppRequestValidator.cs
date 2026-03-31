using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Content.Validators;

public class UpdateAboutAppRequestValidator : AbstractValidator<UpdateAboutAppRequest>
{
    public UpdateAboutAppRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("المحتوى مطلوب");
    }
}
