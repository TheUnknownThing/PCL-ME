namespace PCL.Core.Minecraft.Launch;

public sealed record MinecraftLaunchHttpRequestPlan(
    string Method,
    string Url,
    string? ContentType = null,
    string? Body = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    string? BearerToken = null);
