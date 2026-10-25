using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftInstanceNameServiceTest
{
    [TestMethod]
    public void ContainsInvalidCharactersRejectsClasspathSeparator()
    {
        Assert.IsTrue(MinecraftInstanceNameService.ContainsInvalidCharacters($"Pack{Path.PathSeparator} Ultimate"));
        Assert.IsTrue(MinecraftInstanceNameService.ContainsInvalidCharacters("Yet Another Bingo: Ultimate"));
    }

    [TestMethod]
    public void SanitizeMakesBingoJarPathASingleClasspathEntry()
    {
        var name = MinecraftInstanceNameService.Sanitize("Yet Another Bingo: Ultimate");
        var jarPath = Path.Combine("versions", name, $"{name}.jar");

        Assert.AreEqual("Yet Another Bingo- Ultimate", name);
        Assert.AreEqual(1, jarPath.Split(Path.PathSeparator).Length);
        Assert.IsFalse(MinecraftInstanceNameService.ContainsInvalidCharacters(name));
    }

    [TestMethod]
    public void SanitizeStillReplacesInvalidFileNameCharacters()
    {
        var name = MinecraftInstanceNameService.Sanitize("Pack/Ultimate\0");

        Assert.AreEqual("Pack-Ultimate-", name);
        Assert.IsFalse(MinecraftInstanceNameService.ContainsInvalidCharacters(name));
    }

    [TestMethod]
    public void SanitizePreservesSpacesUnicodeAndVersionNumbers()
    {
        const string name = "Bingo 整合包 1.21.11";

        Assert.AreEqual(name, MinecraftInstanceNameService.Sanitize(name));
        Assert.IsFalse(MinecraftInstanceNameService.ContainsInvalidCharacters(name));
    }
}
