using System;
using System.IO;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FolderNameValidator(
    string? parentFolder = null,
    bool useMinecraftCharCheck = true,
    bool ignoreCase = true,
    bool ignoreSameNameInParentFolder = true)
    : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; } = useMinecraftCharCheck;
    public bool IgnoreCase { get; set; } = ignoreCase;
    public bool IgnoreSameNameInParentFolder { get; set; } = ignoreSameNameInParentFolder;
    public string? ParentFolder { get; set; } = parentFolder;

    public FolderNameValidator() : this(null)
    {
    }

    private void BuildRules()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Input cannot be empty.")
            .Must(x => !x.StartsWith(' ')).WithMessage("File names cannot start with a space.")
            .Must(x => !x.EndsWith(' ')).WithMessage("File names cannot end with a space.")
            .Must(x => !x.EndsWith('.')).WithMessage("File names cannot end with a period.")
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar != null)
                {
                    context.AddFailure($"File names cannot contain {invalidChar}.");
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord != null)
                {
                    context.AddFailure($"File names cannot be {reservedWord}.");
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName)).WithMessage("File names cannot use this legacy NTFS 8.3 format.")
            .Must(x =>
            {
                if (ParentFolder is null) return true;
                
                var dirInfo = new DirectoryInfo(ParentFolder);
                if (!dirInfo.Exists) return true;
                if (IgnoreSameNameInParentFolder) return true;
                    
                return !dirInfo.EnumerateFiles().Select(f => f.Name).Contains(x,
                    IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            }).WithMessage("File names cannot duplicate an existing file.");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
