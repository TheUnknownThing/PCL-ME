using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Text;
using PCL.Core.Minecraft.Launch;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private sealed record LaunchHookRegistration(IExtensionContext Context, ExtensionLaunchHook Definition,
        Func<ExtensionLaunchContext, CancellationToken, ValueTask<ExtensionLaunchChanges>> Prepare);
    private readonly object _hookSync = new();
    private readonly Dictionary<string, LaunchHookRegistration> _launchHooks = new(StringComparer.Ordinal);
    private string? _executingHookOwner;

    private sealed class ScopedLaunchHooks(FrontendExtensionSession session, IExtensionContext context) : IExtensionLaunchHooks
    {
        public IDisposable Register(ExtensionLaunchHook hook, Func<ExtensionLaunchContext, CancellationToken, ValueTask<ExtensionLaunchChanges>> prepare)
        {
            ValidateContributionId(context, hook.Id);
            if (hook.Timeout < TimeSpan.FromMilliseconds(10) || hook.Timeout > TimeSpan.FromMinutes(2) || !Enum.IsDefined(hook.FailurePolicy))
                throw new ArgumentException("Hook deadlines must be between 10 milliseconds and two minutes.");
            lock (session._hookSync) session._launchHooks.Add(hook.Id, new(context, hook, prepare));
            return context.Own(new ExtensionRegistration(() => { lock (session._hookSync) session._launchHooks.Remove(hook.Id); }));
        }
    }

    internal async ValueTask<FrontendLaunchComposition> PrepareExtensionLaunchAsync(FrontendLaunchComposition composition, string instanceId, CancellationToken token)
    {
        LaunchHookRegistration[] hooks;
        lock (_hookSync) hooks = _launchHooks.Values.OrderBy(item => item.Definition.Order).ThenBy(item => item.Definition.Id, StringComparer.Ordinal).ToArray();
        if (hooks.Length == 0) return composition;
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        var environment = new Dictionary<string, string>(composition.SessionStartPlan.ProcessShellPlan.EnvironmentVariables,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var context = new ExtensionLaunchContext { InstanceId = instanceId, MinecraftVersion = _launcher.GetExtensionSurfaceContext().MinecraftVersion };
        foreach (var hook in hooks)
        {
            token.ThrowIfCancellationRequested();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, hook.Context.Lifetime);
            Task<ExtensionLaunchChanges>? task = null;
            try
            {
                _executingHookOwner = hook.Context.Manifest.Id;
                task = Task.Run(async () => await hook.Prepare(context, linked.Token), linked.Token);
                var changes = await task.WaitAsync(hook.Definition.Timeout, linked.Token);
                token.ThrowIfCancellationRequested(); hook.Context.Lifetime.ThrowIfCancellationRequested();
                // Validate a hook atomically, so ContinueLaunch never applies a partial result.
                var nextArguments = new Dictionary<string, string>(arguments, StringComparer.Ordinal);
                var nextEnvironment = new Dictionary<string, string>(environment, environment.Comparer);
                foreach (var argument in changes.JvmArguments)
                {
                    if (argument.Length > 8192 || argument.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
                        !(argument.StartsWith("-D", StringComparison.Ordinal) || argument.StartsWith("-XX:", StringComparison.Ordinal) || argument.StartsWith("-X", StringComparison.Ordinal)) ||
                        argument.StartsWith("-Xbootclasspath", StringComparison.Ordinal))
                        throw new ExtensionOperationException("hook.argument", "Hooks may contribute individual JVM options, not classpaths or main classes.");
                    var key = argument.Split('=', 2)[0];
                    if (key.StartsWith("-XX:", StringComparison.Ordinal)) key = key.Replace("-XX:+", "-XX:").Replace("-XX:-", "-XX:");
                    if (key.StartsWith("-Xmx", StringComparison.Ordinal)) key = "-Xmx";
                    if (key.StartsWith("-Xms", StringComparison.Ordinal)) key = "-Xms";
                    if (nextArguments.TryGetValue(key, out var existing) && existing != argument) throw new ExtensionOperationException("hook.conflict", "Launch hooks supplied conflicting JVM options: " + key);
                    nextArguments[key] = argument;
                }
                foreach (var (key, value) in changes.Environment)
                {
                    if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant) || value.Length > 32768 || value.IndexOfAny(['\0', '\r', '\n']) >= 0)
                        throw new ExtensionOperationException("hook.environment", "Invalid launch environment variable.");
                    if (nextEnvironment.TryGetValue(key, out var existing) && existing != value) throw new ExtensionOperationException("hook.conflict", "Launch hooks supplied conflicting environment variables: " + key);
                    nextEnvironment[key] = value;
                }
                arguments = nextArguments; environment = nextEnvironment;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Host.LogContributionError(hook.Context.Manifest.Id, ex);
                if (hook.Definition.FailurePolicy == ExtensionHookFailurePolicy.StopLaunch)
                    throw new ExtensionOperationException("hook.failed", hook.Definition.Id + ": " + ex.Message);
            }
            finally
            {
                _executingHookOwner = null;
                linked.Cancel();
                if (task is { IsCompleted: false }) _ = task.ContinueWith(failed => _ = failed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        var process = composition.SessionStartPlan.ProcessShellPlan;
        var combinedEnvironment = new Dictionary<string, string>(process.EnvironmentVariables, environment.Comparer);
        foreach (var (key, value) in environment)
        {
            if (combinedEnvironment.TryGetValue(key, out var existing) && existing != value) throw new ExtensionOperationException("hook.conflict", "A hook conflicts with the launcher environment: " + key);
            combinedEnvironment[key] = value;
        }
        var prefix = string.Join(' ', arguments.Values.Select(QuoteProcessArgument));
        var scriptPrefix = string.Join(' ', arguments.Values.Select(QuoteScriptArgument));
        var updated = process with { Arguments = prefix.Length == 0 ? process.Arguments : prefix + " " + process.Arguments,
            EnvironmentVariables = new ReadOnlyDictionary<string, string>(combinedEnvironment) };
        return composition with
        {
            ArgumentPlan = composition.ArgumentPlan with { FinalArguments = updated.Arguments },
            SessionStartPlan = MinecraftLaunchSessionWorkflowService.WithProcessPlan(composition.SessionStartPlan, updated, composition.InstanceName,
                scriptPrefix.Length == 0 ? process.Arguments : scriptPrefix + " " + process.Arguments)
        };
    }

    private static string QuoteProcessArgument(string value)
    {
        // ProcessStartInfo uses the same double-quote/backslash parsing on supported platforms.
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static string QuoteScriptArgument(string value)
    {
        if (!OperatingSystem.IsWindows()) return "'" + value.Replace("'", "'\"'\"'") + "'";
        var result = new StringBuilder();
        var quoted = false;
        foreach (var character in QuoteProcessArgument(value))
        {
            if (character == '"') quoted = !quoted;
            if (character == '%') result.Append('%');
            if (!quoted && "^&|<>()".Contains(character)) result.Append('^');
            result.Append(character);
        }
        return result.ToString();
    }
}
