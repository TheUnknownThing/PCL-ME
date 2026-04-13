using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendPlatformAdapterTest
{
    [TestMethod]
    public void IsSuccessfulStart_ReturnsTrue_WhenProcessExists()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };

        Assert.IsTrue(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, currentProcess));
    }

    [TestMethod]
    public void IsSuccessfulStart_ReturnsTrue_WhenShellExecuteReturnsNull()
    {
        var startInfo = new ProcessStartInfo("C:\\")
        {
            UseShellExecute = true
        };

        Assert.IsTrue(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, null));
    }

    [TestMethod]
    public void IsSuccessfulStart_ReturnsFalse_WhenDirectStartReturnsNull()
    {
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = false
        };

        Assert.IsFalse(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, null));
    }
}
