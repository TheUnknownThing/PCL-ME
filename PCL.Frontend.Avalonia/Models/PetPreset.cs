namespace PCL.Frontend.Avalonia.Models;

/// <summary>How a preset renders and which animation it owns.</summary>
internal enum PetPresetKind
{
    /// <summary>A vector mascot drawn with primitives.</summary>
    VectorMascot,

    /// <summary>A bundled GIF animation.</summary>
    GifAnimation
}

/// <summary>When the interaction animation may replace the idle loop.</summary>
internal enum PetClickAnimationMode
{
    /// <summary>The preset has no separate interaction animation.</summary>
    None,

    /// <summary>Play the interaction animation after the current idle loop finishes.</summary>
    WaitIdleLoopEnd,

    /// <summary>Play the interaction animation immediately.</summary>
    Immediate
}

/// <summary>What a click gesture does on the desktop pet.</summary>
internal enum PetAction
{
    None,
    PlayAnimation,
    ShowLauncher,
    HidePet,
    LaunchSelected
}

/// <summary>An immutable desktop pet preset definition.</summary>
/// <param name="Id">Stable identifier persisted in settings.</param>
/// <param name="Kind">Whether the preset is drawn or animated.</param>
/// <param name="IdleAsset">Bundled asset used for the idle loop, if any.</param>
/// <param name="ClickAsset">Bundled asset used for the interaction animation, if any.</param>
/// <param name="ClickMode">How the interaction animation interrupts the idle loop.</param>
/// <param name="AspectRatio">Window width divided by window height.</param>
/// <param name="BaseHeightDip">Display height in device-independent pixels before scaling.</param>
internal sealed record PetPreset(
    string Id,
    PetPresetKind Kind,
    string? IdleAsset = null,
    string? ClickAsset = null,
    PetClickAnimationMode ClickMode = PetClickAnimationMode.None,
    double AspectRatio = 20d / 26d,
    double BaseHeightDip = 104)
{
    public bool IsAnimated => Kind == PetPresetKind.GifAnimation && IdleAsset is not null;

    /// <summary>True when the preset repeats its idle animation instead of a dedicated one.</summary>
    public bool RepeatsIdleOnClick =>
        ClickMode == PetClickAnimationMode.None
        || string.Equals(IdleAsset, ClickAsset, StringComparison.Ordinal);
}
