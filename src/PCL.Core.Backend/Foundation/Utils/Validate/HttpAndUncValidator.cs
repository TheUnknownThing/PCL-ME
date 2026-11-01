using FluentValidation;
using FluentValidation.Results;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class HttpAndUncValidator(bool allowNullOrEmpty) : AbstractValidator<string>
{
    public bool AllowsNullOrEmpty { get; set; } = allowNullOrEmpty;

    public HttpAndUncValidator() : this(false)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x =>
            {
                if (AllowsNullOrEmpty && string.IsNullOrEmpty(x))
                {
                    return true;
                }

                return x.IsMatch(RegexPatterns.HttpUri) || x.IsMatch(RegexPatterns.UncPath);
            }).WithMessage("The provided path or URL is invalid.");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
