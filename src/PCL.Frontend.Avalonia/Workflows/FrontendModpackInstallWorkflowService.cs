using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();
    private static readonly IReadOnlyDictionary<string, object?> EmptyI18nArgs =
        new Dictionary<string, object?>(0, StringComparer.Ordinal);
}
