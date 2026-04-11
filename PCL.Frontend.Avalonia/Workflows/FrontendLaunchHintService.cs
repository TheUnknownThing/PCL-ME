namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchHintService
{
    private static readonly string[] FallbackHints =
    [
        "在 PCL 文件夹下创建 hints.txt，可以自定义启动时显示的“你知道吗”内容。",
        "游戏的基本时间单位是刻，1 刻等于 0.05 秒。",
        "给一只绵羊命名为 \"jeb_\"，它的羊毛会持续变色。",
        "没有绑定磁石的指南针会始终指向世界出生点。",
        "鹦鹉在正在播放唱片的唱片机周围会跳舞。",
        "把地图和玻璃板一起放进制图台，地图内容会被锁定。",
        "小乌龟长大的一瞬间会掉落鳞片。",
        "药水伤害无视护甲，别指望下界合金甲能硬抗伤害药水。",
        "在 Java 版中，把生物命名为 Dinnerbone 或 Grumm，它会倒过来。",
        "Minecraft 是全球累计销量最高的电子游戏之一。"
    ];

    public static string GetRandomHint(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var candidates = new[]
            {
                Path.Combine(runtimePaths.ExecutableDirectory, "PCL", "hints.txt"),
                FrontendLauncherAssetLocator.GetPath("Resources", "hints.txt")
            }
            .Where(File.Exists)
            .ToArray();

        foreach (var path in candidates)
        {
            try
            {
                var hints = File.ReadAllLines(path)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
                if (hints.Length > 0)
                {
                    return hints[Random.Shared.Next(hints.Length)];
                }
            }
            catch
            {
                // Ignore malformed or temporarily inaccessible hint sources and keep falling back.
            }
        }

        return FallbackHints[Random.Shared.Next(FallbackHints.Length)];
    }
}
