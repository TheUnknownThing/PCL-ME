using System;
using System.IO;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Utils.Validate;

public class FileNameValidator(
    string? parentFolder = null,
    bool ignoreCase = true,
    bool useMinecraftCharCheck = true,
    bool requireParentFolderExists = true)
    : FileSystemValidator
{
    public bool UseMinecraftCharCheck { get; set; } = useMinecraftCharCheck;
    public bool IgnoreCase { get; set; } = ignoreCase;
    public string? ParentFolder { get; set; } = parentFolder;
    public bool RequireParentFolderExists { get; set; } = requireParentFolderExists;

    private bool? _isParentFolderExists;

    public FileNameValidator() : this(null)
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
                if (dirInfo.Exists)
                {
                    return !dirInfo.EnumerateFiles().Select(f => f.Name).Contains(x,
                        IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                }

                _isParentFolderExists = false;
                return !RequireParentFolderExists;

            }).WithMessage(_isParentFolderExists is not null ? $"Parent folder does not exist: {ParentFolder}" : "File names cannot duplicate an existing file.");
    }

    protected override bool PreValidate(ValidationContext<string> context, ValidationResult result)
    {
        BuildRules();
        return base.PreValidate(context, result);
    }
}
