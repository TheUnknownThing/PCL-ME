using System.Text;
using System.Text.Json;
using PCL.Core.App.I18n;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendCliExecutionService
{
    public static int Run(AvaloniaCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Command switch
        {
            AvaloniaCommandKind.LaunchInstance => RunLaunchInstance(options),
            AvaloniaCommandKind.Register => RunDesktopEntryRegistration(),
            AvaloniaCommandKind.Unregister => RunDesktopEntryUnregistration(),
            _ => 1
        };
    }

    private static int RunDesktopEntryRegistration()
    {
        try
        {
            var result = FrontendLinuxDesktopEntryService.RegisterCurrentProcess();
            WriteDesktopEntryResult(result);
            return result.IsSuccess ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunDesktopEntryUnregistration()
    {
        try
        {
            var result = FrontendLinuxDesktopEntryService.Unregister();
            WriteDesktopEntryResult(result);
            return result.IsSuccess ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WriteDesktopEntryResult(FrontendDesktopEntryOperationResult result)
    {
        if (result.IsSuccess)
        {
            Console.WriteLine(result.Message);
        }
        else
        {
            Console.Error.WriteLine(result.Message);
        }
    }

    private static int RunLaunchInstance(AvaloniaCommandOptions options)
    {
        var platformAdapter = new FrontendPlatformAdapter();
        var runtimePaths = FrontendRuntimePaths.Resolve(platformAdapter);
        FrontendLoggingBootstrap.Initialize(runtimePaths);
        FrontendHttpProxyService.ApplyStoredProxySettings(runtimePaths);
        FrontendHttpProxyService.ApplyStoredDnsSettings(runtimePaths);

        using var i18n = CreateI18nService(runtimePaths);

        try
        {
            return RunLaunchInstanceAsync(options, runtimePaths, platformAdapter, i18n).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            FrontendLoggingBootstrap.Dispose();
        }
    }

    private static async Task<int> RunLaunchInstanceAsync(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter,
        II18nService i18n)
    {
        var instanceName = options.InstanceNameOverride?.Trim();
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            Console.Error.WriteLine("Missing instance name.");
            return 1;
        }

        var launcherActionService = new LauncherActionService(
            runtimePaths,
            platformAdapter,
            exitLauncher: static () => { },
            i18n);

        var instanceComposition = FrontendInstanceCompositionService.Compose(
            runtimePaths,
            instanceName,
            FrontendInstanceCompositionService.LoadMode.Lightweight,
            i18n);
        if (!instanceComposition.Selection.HasSelection)
        {
            Console.Error.WriteLine(i18n.T("launch.precheck.failures.instance_not_found", new Dictionary<string, object?>
            {
                ["instance_name"] = instanceName
            }));
            return 1;
        }

        var profileRefresh = await FrontendLaunchProfileRefreshService.RefreshSelectedProfileAsync(
            runtimePaths,
            i18n,
            onStatusChanged: WriteStatusLine);
        if (profileRefresh.WasChecked)
        {
            WriteStatusLine(profileRefresh.Message);
        }

        var ignoreJavaCompatibilityWarningOnce = false;
        FrontendLaunchComposition launchComposition;
        for (var attempt = 0; ; attempt++)
        {
            if (attempt >= 6)
            {
                Console.Error.WriteLine("Could not resolve launch requirements automatically.");
                return 1;
            }

            launchComposition = FrontendLaunchCompositionService.Compose(
                options,
                runtimePaths,
                ignoreJavaCompatibilityWarningOnce,
                i18n);
            if (!launchComposition.PrecheckResult.IsSuccess)
            {
                Console.Error.WriteLine(GetLaunchPrecheckFailureMessage(launchComposition, i18n));
                return 1;
            }

            if (launchComposition.SelectedJavaRuntime is null &&
                launchComposition.JavaRuntimeInstallPlan is not null)
            {
                WriteStatusLine($"Installing Java runtime: {launchComposition.JavaRuntimeInstallPlan.DisplayName}");
                var installResult = await Task.Run(() => launcherActionService.MaterializeJavaRuntime(
                    launchComposition.JavaRuntimeInstallPlan,
                    onProgress: snapshot =>
                    {
                        if (snapshot.Phase == FrontendJavaRuntimeInstallPhase.Finalize &&
                            !string.IsNullOrWhiteSpace(snapshot.CurrentFileRelativePath))
                        {
                            WriteStatusLine(snapshot.CurrentFileRelativePath);
                        }
                    }));
                await FrontendJavaInventoryService.RefreshPortableJavaScanCacheAsync();
                PersistMaterializedJavaRuntime(runtimePaths, platformAdapter, launchComposition.JavaRuntimeInstallPlan, installResult);
                ignoreJavaCompatibilityWarningOnce = false;
                continue;
            }

            if (launchComposition.JavaCompatibilityPrompt is not null)
            {
                WritePromptAutoResolution("Ignoring Java compatibility prompt once", launchComposition.JavaCompatibilityPrompt, i18n);
                ignoreJavaCompatibilityWarningOnce = true;
                continue;
            }

            foreach (var prompt in launchComposition.PrecheckResult.Prompts)
            {
                WritePromptAutoResolution("Continuing through pre-launch prompt", prompt, i18n);
            }

            if (launchComposition.SupportPrompt is not null)
            {
                WritePromptAutoResolution("Continuing through support prompt", launchComposition.SupportPrompt, i18n);
            }

            break;
        }

        if (!instanceComposition.Setup.DisableFileValidation)
        {
            WriteStatusLine(i18n.T("launch.status.logs.verifying_instance_files"));
            _ = await Task.Run(() => launcherActionService.RepairInstance(
                new FrontendInstanceRepairRequest(
                    instanceComposition.Selection.LauncherDirectory,
                    instanceComposition.Selection.InstanceDirectory,
                    instanceComposition.Selection.InstanceName,
                    ForceCoreRefresh: false)));
        }

        var effectiveComposition = PrepareHeadlessLaunchComposition(launchComposition);
        var startResult = await Task.Run(() => launcherActionService.StartLaunchSession(
            effectiveComposition,
            instanceComposition.Selection.InstanceDirectory,
            WriteStatusLine));

        WriteStatusLine($"Launched {launchComposition.InstanceName} (PID {startResult.Process.Id}).");
        return 0;
    }

    private static FrontendLaunchComposition PrepareHeadlessLaunchComposition(FrontendLaunchComposition composition)
    {
        if (!OperatingSystem.IsWindows())
        {
            return composition;
        }

        var processShellPlan = composition.SessionStartPlan.ProcessShellPlan with
        {
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        return composition with
        {
            SessionStartPlan = composition.SessionStartPlan with
            {
                ProcessShellPlan = processShellPlan
            }
        };
    }

    private static string GetLaunchPrecheckFailureMessage(FrontendLaunchComposition composition, II18nService i18n)
    {
        return composition.PrecheckResult.Failure is { } failure
            ? i18n.T(failure.ToLocalizedText())
            : i18n.T("launch.precheck.failures.unknown");
    }

    private static void PersistMaterializedJavaRuntime(
        FrontendRuntimePaths runtimePaths,
        FrontendPlatformAdapter platformAdapter,
        FrontendJavaRuntimeInstallPlan installPlan,
        FrontendJavaRuntimeInstallResult installResult)
    {
        var executablePath = platformAdapter.GetJavaExecutablePath(installResult.RuntimeDirectory);
        var installedRuntime = FrontendJavaInventoryService.TryResolveRuntime(
                                 executablePath,
                                 isEnabled: true,
                                 fallbackDisplayName: installPlan.DisplayName)
                             ?? FrontendJavaInventoryService.CreateStoredRuntime(
                                 executablePath,
                                 installPlan.DisplayName,
                                 installPlan.VersionName,
                                 isEnabled: true,
                                 is64Bit: Is64BitMachineType(installPlan.RuntimeArchitecture),
                                 isJre: installPlan.IsJre,
                                 brand: installPlan.Brand,
                                 architecture: installPlan.RuntimeArchitecture);

        var provider = runtimePaths.OpenLocalConfigProvider();
        var rawJson = provider.Exists("LaunchArgumentJavaUser")
            ? provider.Get<string>("LaunchArgumentJavaUser")
            : "[]";
        var items = FrontendJavaInventoryService.ParseStorageItems(rawJson).ToList();
        var existingIndex = items.FindIndex(item =>
            string.Equals(item.Path, installedRuntime.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        var updatedItem = new JavaStorageItem
        {
            Path = installedRuntime.ExecutablePath,
            IsEnable = true,
            Source = JavaSource.AutoInstalled,
            Installation = new JavaStorageInstallationInfo
            {
                JavaExePath = installedRuntime.ExecutablePath,
                DisplayName = installedRuntime.DisplayName,
                Version = installedRuntime.ParsedVersion?.ToString() ?? installedRuntime.DisplayName,
                MajorVersion = installedRuntime.MajorVersion,
                Is64Bit = installedRuntime.Is64Bit,
                IsJre = installedRuntime.IsJre,
                Brand = installedRuntime.Brand,
                Architecture = installedRuntime.Architecture
            }
        };

        if (existingIndex >= 0)
        {
            items[existingIndex] = updatedItem;
        }
        else
        {
            items.Add(updatedItem);
        }

        provider.Set("LaunchArgumentJavaUser", JsonSerializer.Serialize(items));
        provider.Sync();

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        sharedConfig.Set("LaunchArgumentJavaSelect", installedRuntime.ExecutablePath);
        sharedConfig.Sync();
    }

    private static bool? Is64BitMachineType(MachineType? machineType)
    {
        return machineType switch
        {
            MachineType.AMD64 or MachineType.ARM64 or MachineType.IA64 => true,
            MachineType.I386 or MachineType.ARM or MachineType.ARMNT => false,
            _ => null
        };
    }

    private static void WritePromptAutoResolution(string reason, MinecraftLaunchPrompt prompt, II18nService i18n)
    {
        var builder = new StringBuilder();
        builder.Append(reason);
        builder.Append(": ");
        builder.Append(i18n.T(prompt.Title));
        var message = i18n.T(prompt.Message);
        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append(" - ");
            builder.Append(message.Replace(Environment.NewLine, " ", StringComparison.Ordinal));
        }

        WriteStatusLine(builder.ToString());
    }

    private static I18nService CreateI18nService(FrontendRuntimePaths runtimePaths)
    {
        var localeDirectory = Path.Combine(AppContext.BaseDirectory, "Locales");
        var availableLocales = Directory.EnumerateFiles(localeDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToArray();
        var settingsManager = new I18nSettingsManager(
            new FrontendConfigProviderAdapter(runtimePaths.OpenSharedConfigProvider()),
            initialLocaleProvider: () => System.Globalization.CultureInfo.CurrentUICulture.Name,
            availableLocales: availableLocales);
        return new I18nService(settingsManager);
    }

    private static void WriteStatusLine(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine(message);
        }
    }
}
