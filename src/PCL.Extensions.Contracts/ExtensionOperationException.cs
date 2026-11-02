namespace PCL.Extensions.Contracts;

/// <summary>An expected operation failure that does not invalidate an extension's activation.</summary>
public sealed class ExtensionOperationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
