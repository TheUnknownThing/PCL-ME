using Avalonia.Platform;

namespace PCL.Extensions.DesktopPet;

/// <summary>Defines every bundled pet preset and loads its animation assets.</summary>
internal static class PetPresetCatalog
{
    private const string AssetRoot = "avares://PCL.Extensions.DesktopPet/Assets/";

    public const string DefaultId = "creeper_gif";

    public static IReadOnlyList<PetPreset> All { get; } =
    [
        new("creeper_gif", PetPresetKind.GifAnimation,
            AssetRoot + "creeper_n.gif", AssetRoot + "creeper_l.gif",
            PetClickAnimationMode.WaitIdleLoopEnd, AspectRatio: 1),
        new("slime", PetPresetKind.VectorMascot),
        new("enderman", PetPresetKind.VectorMascot),
        new("campfire_gif", PetPresetKind.GifAnimation,
            AssetRoot + "campfire.gif", AssetRoot + "campfire.gif",
            PetClickAnimationMode.None, AspectRatio: 1)
    ];

    public static IReadOnlyList<string> Ids { get; } = All.Select(preset => preset.Id).ToArray();

    /// <summary>Resolves a persisted id, falling back to the default preset.</summary>
    public static PetPreset Find(string? id) =>
        All.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.Ordinal)) ?? All[0];

    public static bool IsKnown(string? id) =>
        id is not null && All.Any(preset => string.Equals(preset.Id, id, StringComparison.Ordinal));

    /// <summary>Loads a preset animation at the requested decode size.</summary>
    public static PetImage? LoadAnimation(PetPreset preset, bool click, int maximumSide)
    {
        var asset = click ? preset.ClickAsset : preset.IdleAsset;
        if (!preset.IsAnimated || asset is null) return null;
        try
        {
            using var stream = AssetLoader.Open(new Uri(asset));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return PetImageService.LoadAnimated(buffer.ToArray(), maximumSide);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Could not load the pet preset asset {asset}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Returns the decode size that keeps a preset sharp at the on-screen size.</summary>
    public static int GetDecodeSide(double heightDip, double renderScaling)
    {
        var pixels = heightDip * (renderScaling > 0 ? renderScaling : 1) * 1.25;
        return Math.Clamp((int)Math.Ceiling(pixels), 96, 320);
    }
}
