using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.UI.Controls;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class LaunchPrecheckE2E
{
    [TestMethod]
    public Task Launch_WithNoInstanceSelected_ButtonStaysDisabled() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await Settle();

        Assert.IsFalse(app.Launcher.LaunchCommand.CanExecute(null),
            "The launch command must not be executable while no instance is selected.");

        var launchButton = app.Window.GetVisualDescendants().OfType<PclLaunchButton>().Single(button => button.IsEffectivelyVisible);
        Assert.IsFalse(launchButton.Find<global::Avalonia.Controls.Button>("ButtonHost")!.IsEffectivelyEnabled,
            "The launch button must not be clickable while no instance is selected.");
        Assert.AreEqual(PclTheme.GetBrush("ColorBrushGray4"), launchButton.Find<global::Avalonia.Controls.TextBlock>("TitleBlock")!.Foreground,
            "The launch button must render muted while no instance is selected.");
    });

    [TestMethod]
    public Task Launch_AfterPrecheckFailure_RecoversInsteadOfSticking() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        // A reserved character in the instance name makes the launch precheck fail
        // through the generic branch, which used to leave the button stuck on "Launching...".
        app.CreateInstance("bad;name");
        app.Actions.PersistLocalValue("LaunchInstanceSelect", "bad;name");
        await app.Restart(); await Settle();

        await app.Session.NavigateAsync("Launch"); await Settle();
        Assert.IsTrue(app.Launcher.LaunchCommand.CanExecute(null),
            "The launch command must be executable once an instance is selected.");
        var launchButton = app.Window.GetVisualDescendants().OfType<PclLaunchButton>().Single(button => button.IsEffectivelyVisible);
        Assert.AreEqual(PclTheme.GetBrush("ColorBrush3"), launchButton.Find<global::Avalonia.Controls.TextBlock>("TitleBlock")!.Foreground,
            "The launch button must stop rendering muted once an instance is selected.");
        await Click(app.Window, launchButton);

        await Wait(() => app.Launcher.IsLaunchDialogVisible && !app.Launcher.IsLaunchDialogBusy,
            "The precheck failure must surface as a stopped launch dialog. " + app.Launcher.LaunchButtonTitle);
        Assert.IsTrue(app.Launcher.IsLaunchDialogError, "The stopped dialog must be flagged as an error.");

        await Wait(() => app.Launcher.LaunchCommand.CanExecute(null),
            "The launch command must become available again after a precheck failure instead of staying stuck.");
        Assert.AreEqual("Launch Game", app.Launcher.LaunchButtonTitle, "The launch button must leave the Launching... state.");

        await Click(app.Window, () => app.Window.GetVisualDescendants().OfType<PclButton>()
            .SingleOrDefault(button => button.IsEffectivelyVisible && Equals(button.Command, app.Launcher.CancelLaunchCommand)));
        await Wait(() => !app.Launcher.IsLaunchDialogVisible, "The stopped dialog must be closable.");
        Assert.IsTrue(app.Launcher.LaunchCommand.CanExecute(null), "Closing the dialog must keep the launch command available.");
    });
}
