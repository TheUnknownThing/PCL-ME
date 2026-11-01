using System;
using System.IO;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FolderPathValidator(bool useMinecraftCharCheck) : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; } = useMinecraftCharCheck;

    public FolderPathValidator() : this(true)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Input cannot be empty.")
            .Must(x => !x.EndsWith(' ')).WithMessage("Folder names cannot end with a space.")
            .Must(x => !x.EndsWith('.')).WithMessage("Folder names cannot end with a period.");

        RuleForEach(x => GetSubPaths(x))
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("The folder path contains an invalid segment.")
            .Must(x => !x.StartsWith(' ')).WithMessage("Folder names cannot start with a space.")
            .Must(x => !x.EndsWith(' ')).WithMessage("Folder names cannot end with a space.")
            .Must(x => !x.EndsWith('.')).WithMessage("Folder names cannot end with a period.")
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar != null)
                {
                    context.AddFailure($"Folder names cannot contain {invalidChar}.");
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord != null)
                {
                    context.AddFailure($"Folder names cannot be {reservedWord}.");
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName)).WithMessage("Folder names cannot use this legacy NTFS 8.3 format.")
            .OverridePropertyName("PathSegments");
    }
    
    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
    
    private static string[] GetSubPaths(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var normalizedPath = path.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return [];
        }

        var startIndex = 0;
        if (normalizedPath.StartsWith("//", StringComparison.Ordinal))
        {
            startIndex = Math.Min(2, segments.Length);
        }
        else if (segments[0].EndsWith(':'))
        {
            startIndex = 1;
        }

        return segments[startIndex..];
    }
}
