using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCL.Extensions.Runtime;

namespace PCL.Frontend.Avalonia.Workflows;

internal enum ExtensionReleaseChannel { Stable, Beta, Alpha }

internal sealed record ExtensionReleaseAsset(string Title, Uri Url, ExtensionReleaseChannel Channel)
{
    public override string ToString() => Title;
}

internal sealed record ExtensionRepository(Uri Url, string Project)
{
    public Uri Origin => new(Url.GetLeftPart(UriPartial.Authority));
}

/// <summary>Resolves remote packages and builds source before handing installation to the host.</summary>
internal sealed partial class FrontendExtensionInstallService
{
    private const long MaximumPackageBytes = 256L * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly Func<string, string[], string, CancellationToken, Task<string>> _run;
    internal static FrontendExtensionInstallService Instance { get; } = new(
        FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromMinutes(5)));

    internal FrontendExtensionInstallService(HttpClient http,
        Func<string, string[], string, CancellationToken, Task<string>>? run = null)
    {
        _http = http;
        _run = run ?? RunProcessAsync;
    }

    internal static Uri ParseUrl(string text)
    {
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(url.UserInfo))
            throw new ExtensionInstallException("invalid_url");
        return url;
    }

    internal static bool IsPackageUrl(Uri url) => url.AbsolutePath.EndsWith(".pclext", StringComparison.OrdinalIgnoreCase)
        || url.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    internal static ExtensionRepository ParseRepository(Uri url)
    {
        var project = url.AbsolutePath.Trim('/');
        // Accept repository landing pages and their release tabs, including GitLab subgroups.
        var releaseTab = project.IndexOf("/-/", StringComparison.Ordinal);
        if (releaseTab >= 0) project = project[..releaseTab];
        else if (project.EndsWith("/releases", StringComparison.Ordinal)) project = project[..^9];
        var cloneProject = project;
        if (project.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) project = project[..^4];
        if (project.Split('/').Length < 2 || project.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".."))
            throw new ExtensionInstallException("invalid_url");
        return new(new Uri(url.GetLeftPart(UriPartial.Authority) + "/" + cloneProject), project);
    }

    internal async Task<IReadOnlyList<ExtensionReleaseAsset>> GetReleasesAsync(ExtensionRepository repository, CancellationToken token)
    {
        if (repository.Url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return await ReadReleasesAsync(new Uri("https://api.github.com/repos/" + repository.Project + "/releases"), false, token);
        var gitlab = new Uri(repository.Origin, "/api/v4/projects/" + Uri.EscapeDataString(Uri.UnescapeDataString(repository.Project)) + "/releases");
        try { return await ReadReleasesAsync(gitlab, true, token); }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Unauthorized)
        {
            // Gitea and Forgejo (including Codeberg) share this release API.
            return await ReadReleasesAsync(new Uri(repository.Origin, "/api/v1/repos/" + repository.Project + "/releases"), false, token);
        }
    }

    private async Task<IReadOnlyList<ExtensionReleaseAsset>> ReadReleasesAsync(Uri endpoint, bool gitlab, CancellationToken token)
    {
        var result = new List<ExtensionReleaseAsset>();
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + $"?per_page=100&limit=100&page={page}");
            request.Headers.UserAgent.ParseAdd("PCL-ME/1.0");
            using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: token).ConfigureAwait(false)
                ?? throw new InvalidDataException("Empty releases response.");
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (Flag(release, "draft") || Flag(release, "upcoming_release")) continue;
                var tag = Text(release, "tag_name");
                var title = Text(release, "name");
                if (string.IsNullOrWhiteSpace(title)) title = tag;
                var channel = ClassifyChannel(tag + " " + title, Flag(release, "prerelease"));
                if (!release.TryGetProperty("assets", out var assets)) continue;
                var entries = gitlab ? assets.GetProperty("links") : assets;
                foreach (var asset in entries.EnumerateArray())
                {
                    var name = Text(asset, "name");
                    var link = gitlab ? Text(asset, "direct_asset_url") : Text(asset, "browser_download_url");
                    if (gitlab && string.IsNullOrEmpty(link)) link = Text(asset, "url");
                    if (!Uri.TryCreate(link, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) continue;
                    if (!IsPackageUrl(url) && !name.EndsWith(".pclext", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new(title + " · " + name, url, channel));
                }
            }
            var hasNextPage = response.Headers.TryGetValues("Link", out var links) && links.Any(link => link.Contains("rel=\"next\"", StringComparison.Ordinal))
                || response.Headers.TryGetValues("X-Next-Page", out var pages) && pages.Any(value => !string.IsNullOrWhiteSpace(value));
            if (json.RootElement.GetArrayLength() < 100 && !hasNextPage) break;
            if (page >= 100) break;
        }
        return result;
        static bool Flag(JsonElement value, string key) => value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.True;
        static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var item) ? item.GetString() ?? "" : "";
    }

    [GeneratedRegex(@"(?:^|[^a-z])alpha(?:[^a-z]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlphaPattern();
    [GeneratedRegex(@"(?:^|[^a-z])(?:beta|rc|preview)(?:[^a-z]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BetaPattern();

    internal static ExtensionReleaseChannel ClassifyChannel(string text, bool prerelease)
        => AlphaPattern().IsMatch(text) ? ExtensionReleaseChannel.Alpha
            : prerelease || BetaPattern().IsMatch(text) ? ExtensionReleaseChannel.Beta : ExtensionReleaseChannel.Stable;

    internal async Task<IReadOnlyList<string>> GetBranchesAsync(ExtensionRepository repository, CancellationToken token)
    {
        var output = await _run("git", ["ls-remote", "--heads", repository.Url.AbsoluteUri], Path.GetTempPath(), token).ConfigureAwait(false);
        return output.Split('\n').Select(line => line.Trim().Split('\t'))
            .Where(parts => parts.Length == 2 && parts[1].StartsWith("refs/heads/", StringComparison.Ordinal))
            .Select(parts => parts[1][11..]).Distinct(StringComparer.Ordinal)
            .OrderBy(branch => branch is "main" or "master" ? 0 : 1).ThenBy(branch => branch, StringComparer.Ordinal).ToArray();
    }

    internal async Task<ExtensionInfo> InstallUrlAsync(ExtensionHost host, Uri url, string? expectedId, CancellationToken token)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var archive = Path.Combine(directory, "extension.pclext");
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumPackageBytes) throw new ExtensionInstallException("package_too_large");
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = File.Create(archive))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += count;
                    if (total > MaximumPackageBytes) throw new ExtensionInstallException("package_too_large");
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                }
            }
            return await InstallFileAsync(host, archive, expectedId, token).ConfigureAwait(false);
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    internal async Task<ExtensionInfo> InstallSourceAsync(ExtensionHost host, ExtensionRepository repository, string branch,
        string? projectPath, string? expectedId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(branch)) throw new ExtensionInstallException("no_branches");
        var directory = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(directory, "source");
            await _run("git", ["clone", "--depth", "1", "--single-branch", "--branch", branch, "--", repository.Url.AbsoluteUri, source], directory, token).ConfigureAwait(false);
            var project = FindProject(source, projectPath, expectedId);
            var published = Path.Combine(directory, "published");
            await _run("dotnet", ["publish", project, "-c", "Release", "--nologo", "-o", published], source, token).ConfigureAwait(false);
            ExtensionPackage.ReadManifest(published);
            var archive = Path.Combine(directory, "extension.pclext");
            await Task.Run(() => ZipFile.CreateFromDirectory(published, archive), token).ConfigureAwait(false);
            return await InstallFileAsync(host, archive, expectedId, token).ConfigureAwait(false);
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    internal static Task<ExtensionInfo> InstallFileAsync(ExtensionHost host, string path, string? expectedId, CancellationToken token)
        => (expectedId is null ? host.InstallAsync(path, token) : host.UpdateAsync(expectedId, path, token)).AsTask();

    internal static string FindProject(string source, string? projectPath, string? expectedId)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var selected = ExtensionPackage.ResolvePackagePath(source, projectPath.Trim());
            if (!File.Exists(selected) || !selected.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                throw new ExtensionInstallException("project_missing");
            return selected;
        }
        var projects = Directory.EnumerateFiles(source, "extension.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar).Any(part => part is ".git" or "bin" or "obj"))
            .Where(path => MatchesId(path, expectedId))
            .SelectMany(path => Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.csproj")).ToArray();
        if (projects.Length != 1) throw new ExtensionInstallException(projects.Length == 0 ? "project_missing" : "project_ambiguous");
        return projects[0];

        static bool MatchesId(string path, string? id)
        {
            if (id is null) return true;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.EnumerateObject().Any(property => property.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == id);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pcl-extension-install", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        // Git object files can be read-only on Windows. Do not follow checkout symlinks.
        if (OperatingSystem.IsWindows())
            foreach (var path in Directory.EnumerateFiles(directory, "*", new EnumerationOptions
                     { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        Directory.Delete(directory, recursive: true);
    }

    internal static async Task<string> RunProcessAsync(string executable, string[] arguments, string directory, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        using var process = new Process { StartInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception) { throw new ExtensionInstallException("tools_missing"); }
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            if (!token.IsCancellationRequested) throw new ExtensionInstallException("process_timeout");
            throw;
        }
        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var message = (error + "\n" + output).Trim();
            throw new InvalidOperationException(message[..Math.Min(message.Length, 4000)]);
        }
        return output;
    }
}

internal sealed class ExtensionInstallException(string key) : Exception(key)
{
    internal string Key { get; } = key;
}
