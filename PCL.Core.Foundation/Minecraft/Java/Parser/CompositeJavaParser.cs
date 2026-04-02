using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Parser;

public sealed class CompositeJavaParser(params IJavaParser[] parsers) : IJavaParser
{
    private readonly IReadOnlyList<IJavaParser> _parsers = parsers;

    public JavaInstallation? Parse(string javaExePath)
    {
        return _parsers
            .Select(parser => parser.Parse(javaExePath))
            .FirstOrDefault(result => result != null);
    }
}
