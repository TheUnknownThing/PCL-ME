using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class SystemEnvironmentInfoTest
{
    [TestCleanup]
    public void Cleanup()
    {
        SystemEnvironmentSourceProvider.Reset();
    }

    [TestMethod]
    public void DefaultSourceReturnsPortableFallbackSnapshot()
    {
        var runtimeSource = new FakeRuntimeInfoSource(new SystemRuntimeSnapshot(
            new Version(9, 8, 7, 6),
            Architecture.Arm64,
            true,
            123456789,
            987654321));
        var source = new DefaultSystemEnvironmentSource(runtimeSource);

        var snapshot = source.GetSnapshot();

        Assert.AreEqual(RuntimeInformation.OSDescription, snapshot.OsDescription);
        Assert.AreEqual(new Version(9, 8, 7, 6), snapshot.OsVersion);
        Assert.AreEqual(Architecture.Arm64, snapshot.OsArchitecture);
        Assert.IsTrue(snapshot.Is64BitOperatingSystem);
        Assert.AreEqual<ulong>(123456789, snapshot.TotalPhysicalMemoryBytes);
        Assert.AreEqual(string.Empty, snapshot.CpuName);
        Assert.AreEqual(0, snapshot.Gpus.Count);
    }

    [TestMethod]
    public void PublicFacadeUsesConfiguredProvider()
    {
        SystemEnvironmentSnapshot expected = new(
            "Test OS",
            new Version(1, 2, 3, 4),
            Architecture.X64,
            true,
            4096,
            "Test CPU",
            [new SystemGpuInfo("Test GPU", 8192, "1.0.0")]);
        SystemEnvironmentSourceProvider.SetCurrent(new FakeSystemEnvironmentSource(expected));

        var actual = SystemEnvironmentInfo.GetSnapshot();

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ProcessInteropSetGpuPreferencePreservesInvalidPathValidation()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => ProcessInterop.SetGpuPreference(" "));

        StringAssert.Contains(exception.Message, "可执行文件路径不能为空");
    }

    private sealed class FakeRuntimeInfoSource(SystemRuntimeSnapshot snapshot) : ISystemRuntimeInfoSource
    {
        public SystemRuntimeSnapshot GetSnapshot() => snapshot;
    }

    private sealed class FakeSystemEnvironmentSource(SystemEnvironmentSnapshot snapshot) : ISystemEnvironmentSource
    {
        public SystemEnvironmentSnapshot GetSnapshot() => snapshot;
    }
}
