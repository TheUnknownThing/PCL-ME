using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class StringLengthValidator(int min = 0, int max = int.MaxValue) : AbstractValidator<string>
{
    public int Min { get; set; } = min;
    public int Max { get; set; } = max;

    public StringLengthValidator() : this(0)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length != Max || Max == Min).WithMessage($"Length must be exactly {Max} characters.")
            .Must(x => x.Length >= Min).WithMessage($"Length must be at least {Min} characters.")
            .Must(x => x.Length <= Max).WithMessage($"Length must be at most {Max} characters.");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
