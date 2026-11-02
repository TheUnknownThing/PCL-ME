using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft;

public static class MinecraftInstanceNameService
{
    // Java splits classpath entries on this character even inside a quoted argument.
    private static readonly char[] InvalidCharacters = [.. Path.GetInvalidFileNameChars(), Path.PathSeparator];

    public static bool ContainsInvalidCharacters(string name)
    {
        return name.IndexOfAny(InvalidCharacters) >= 0;
    }

    public static string Sanitize(string name)
    {
        return new string(name.Select(character => InvalidCharacters.Contains(character) ? '-' : character).ToArray());
    }
}
