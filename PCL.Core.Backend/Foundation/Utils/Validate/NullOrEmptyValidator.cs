using FluentValidation;

namespace PCL.Core.Utils.Validate;

public class NullOrEmptyValidator : AbstractValidator<string>
{
    public NullOrEmptyValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x)).WithMessage("Input cannot be empty.");
    }
}
