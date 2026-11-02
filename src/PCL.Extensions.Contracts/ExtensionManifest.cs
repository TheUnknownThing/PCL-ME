namespace PCL.Extensions.Contracts;

public sealed record ExtensionManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string? Website { get; init; }
    public int ApiVersion { get; init; } = 1;
    public string? MinimumSdkVersion { get; init; }
    public string[] RequiredCapabilities { get; init; } = [];
    public string[] OptionalCapabilities { get; init; } = [];
    public int? AvaloniaMajorVersion { get; init; }
    public string? EntryAssembly { get; init; }
    public string? EntryType { get; init; }
    public bool StartWithLauncher { get; init; }
    public string[] Platforms { get; init; } = [];
    public string[] Architectures { get; init; } = [];
    public ExtensionDependency[] Dependencies { get; init; } = [];
    public ExtensionCommand[] Commands { get; init; } = [];
    public ExtensionSetting[] Settings { get; init; } = [];
    public string DefaultLocale { get; init; } = "en-US";
}

public sealed record ExtensionDependency(string Id, string MinimumVersion);

public sealed record ExtensionCommand
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public string? Shortcut { get; init; }
    public string[] Keywords { get; init; } = [];
}

public enum ExtensionSettingKind { Boolean, String, Number, Choice, File }

public sealed record ExtensionSetting
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public ExtensionSettingKind Kind { get; init; }
    public string? DefaultValue { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public ExtensionSettingChoice[] Choices { get; init; } = [];
    public string[] FilePatterns { get; init; } = [];
}

public sealed record ExtensionSettingChoice(string Value, string Title, string? TitleKey = null);
