using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupConsentResult(
    IReadOnlyList<LauncherStartupPrompt> Prompts);
