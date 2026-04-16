using FluentValidation;
using FluentValidation.Results;

namespace PCL.Core.Utils.Validate;

public class IntValidator(int max = int.MaxValue, int min = int.MinValue) : AbstractValidator<string>
{
    public int Max { get; set; } = max;
    public int Min { get; set; } = min;

    public IntValidator() : this(int.MaxValue)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => x.Length < 9).WithMessage("Please enter a number within the expected range.")
            .Must(x => int.TryParse(x, out _)).WithMessage("Please enter an integer.")
            .Must(x => int.TryParse(x, out var value) && value <= Max).WithMessage($"Must be no greater than {Max}.")
            .Must(x => int.TryParse(x, out var value) && value >= Min).WithMessage($"Must be no less than {Min}.");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
