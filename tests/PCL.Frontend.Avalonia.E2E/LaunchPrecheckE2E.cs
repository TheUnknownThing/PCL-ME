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
    public Task Launch_WithNoInstanceSelected_RecoversAfterPrecheckFailure() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await Settle();

        await app.Session.NavigateAsync("Launch"); await Settle();
        var launchButton = app.Window.GetVisualDescendants().OfType<PclLaunchButton>().Single(button => button.IsEffectivelyVisible);
        await Click(app.Window, launchButton);

        await Wait(() => app.Launcher.IsLaunchDialogVisible && !app.Launcher.IsLaunchDialogBusy,
            "The instance-not-selected precheck failure must surface as a stopped launch dialog. " + app.Launcher.LaunchButtonTitle);
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
