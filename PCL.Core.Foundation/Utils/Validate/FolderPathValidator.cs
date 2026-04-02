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
            .NotEmpty().WithMessage("输入内容不能为空！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件夹名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件夹名不能以小数点结尾！");

        RuleForEach(x => GetSubPaths(x))
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("文件夹路径存在错误！")
            .Must(x => !x.StartsWith(' ')).WithMessage("文件夹名不能以空格开头！")
            .Must(x => !x.EndsWith(' ')).WithMessage("文件夹名不能以空格结尾！")
            .Must(x => !x.EndsWith('.')).WithMessage("文件夹名不能以小数点结尾！")
            .Custom((fileName, context) => 
            {
                var invalidChar = CheckInvalidStrings(fileName, UseMinecraftCharCheck ? ["!;"] : []);
                if (invalidChar != null)
                {
                    context.AddFailure($"文件夹名不可包含 {invalidChar} 字符！");
                }
            })
            .Custom((fileName, context) => 
            {
                var reservedWord = CheckReservedWord(fileName, []);
                if (reservedWord != null)
                {
                    context.AddFailure($"文件夹名不可为 {reservedWord}！");
                }
            })
            .Must(x => !x.IsMatch(RegexPatterns.Ntfs83FileName)).WithMessage("文件夹名不能包含这一特殊格式！")
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
