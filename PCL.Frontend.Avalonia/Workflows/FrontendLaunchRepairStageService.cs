namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchRepairStageService
{
    public static string ResolveStage(FrontendInstanceRepairProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var assets = snapshot.Groups.TryGetValue(FrontendInstanceRepairFileGroup.Assets, out var assetGroup)
            ? assetGroup
            : null;
        if (assets is not null && assets.TotalFiles > 0 && assets.Progress < 0.999d)
        {
            return string.IsNullOrWhiteSpace(assets.CurrentFileName)
                ? "校验游戏资源文件"
                : $"校验资源文件 • {assets.CurrentFileName}";
        }

        var supportGroups = new[]
        {
            FrontendInstanceRepairFileGroup.Client,
            FrontendInstanceRepairFileGroup.Libraries,
            FrontendInstanceRepairFileGroup.AssetIndex
        };
        foreach (var group in supportGroups)
        {
            if (!snapshot.Groups.TryGetValue(group, out var snapshotGroup) || snapshotGroup.TotalFiles == 0 || snapshotGroup.Progress >= 0.999d)
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(snapshotGroup.CurrentFileName)
                ? "补全游戏主文件与支持库"
                : $"补全支持文件 • {snapshotGroup.CurrentFileName}";
        }

        return "校验实例文件";
    }
}
