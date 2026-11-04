using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.UI.Controls;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class ToolsTestE2E
{
    [TestMethod]
    public Task ToolsTest_AchievementCard_ShowsInputRequirements() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start(); await Settle();

        await app.Session.NavigateAsync("Tools/ToolsTest"); await Settle();
        var card = app.Window.GetVisualDescendants().OfType<PclCard>()
            .Single(control => control.Header.Contains("Achievement"));
        if (!card.IsChevronExpanded)
        {
            await Click(app.Window, card.Find<Button>("HeaderButton")!);
        }

        await Wait(() => app.Window.GetVisualDescendants().OfType<TextBlock>()
            .Any(block => block.Text?.Contains("third-party service") == true),
            "The achievement generator card must describe the service's input restrictions.");
    });
}
