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
    private static FrontendInstallApplyRequest BuildInstallRequest(
        FrontendModpackPackage package,
        FrontendModpackInstallRequest request,
        II18nService? i18n)
    {
        var minecraftChoice = ResolveMinecraftChoice(package.MinecraftVersion, request.DownloadSourceIndex, i18n);
        var primaryChoice =
            ResolveLoaderChoice("Forge", package.MinecraftVersion, package.ForgeVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("NeoForge", package.MinecraftVersion, package.NeoForgeVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Fabric", package.MinecraftVersion, package.FabricVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Quilt", package.MinecraftVersion, package.QuiltVersion, request.DownloadSourceIndex, i18n);
        var optiFineChoice = ResolveLoaderChoice("OptiFine", package.MinecraftVersion, package.OptiFineVersion, request.DownloadSourceIndex, i18n);

        return new FrontendInstallApplyRequest(
            request.LauncherDirectory,
            request.InstanceName,
            request.DownloadSourceIndex,
            minecraftChoice,
            primaryChoice,
            LiteLoaderChoice: null,
            OptiFineChoice: optiFineChoice,
            FabricApiChoice: null,
            LegacyFabricApiChoice: null,
            QslChoice: null,
            OptiFabricChoice: null,
            UseInstanceIsolation: true,
            RunRepair: true,
            ForceCoreRefresh: true,
            PreserveExistingManagedModFiles: true);
    }

    private static FrontendInstallChoice ResolveMinecraftChoice(string version, int downloadSourceIndex, II18nService? i18n = null)
    {
        var choices = FrontendInstallWorkflowService.GetMinecraftCatalogChoices(version, downloadSourceIndex, i18n);
        var choice = choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Metadata?["rawVersion"]?.GetValue<string>(), version, StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.minecraft_choice_missing", ("version", version)));
    }

    private static FrontendInstallChoice? ResolveLoaderChoice(
        string optionTitle,
        string minecraftVersion,
        string? requestedVersion,
        int downloadSourceIndex,
        II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return null;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion, downloadSourceIndex, i18n);
        var choice = choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, requestedVersion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Title, requestedVersion, StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new InvalidOperationException(
            ModpackText(
                i18n,
                "resource_detail.modpack.workflow.errors.loader_choice_missing",
                ("option_title", optionTitle),
                ("requested_version", requestedVersion)));
    }

    private static void FinalizeInstalledInstance(FrontendModpackPackage package, FrontendModpackInstallRequest request)
    {
        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(request.TargetDirectory);
        provider.Set("VersionArgumentIndieV2", true);
        PersistKnownMinecraftVersion(provider, package.MinecraftVersion);
        provider.Set("VersionModpackVersion", package.PackageVersion ?? string.Empty);
        provider.Set("VersionModpackSource", request.ProjectSource ?? string.Empty);
        provider.Set("VersionModpackId", request.ProjectId ?? string.Empty);
        provider.Set("CustomInfo", request.ProjectDescription ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(package.LaunchJvmArguments))
        {
            provider.Set("VersionAdvanceJvm", package.LaunchJvmArguments);
        }

        if (!string.IsNullOrWhiteSpace(package.LaunchGameArguments))
        {
            provider.Set("VersionAdvanceGame", package.LaunchGameArguments);
        }

        if (!string.IsNullOrWhiteSpace(request.IconPath) && File.Exists(request.IconPath))
        {
            var logoDirectory = Path.Combine(request.TargetDirectory, "PCL");
            Directory.CreateDirectory(logoDirectory);
            File.Copy(request.IconPath, Path.Combine(logoDirectory, "Logo.png"), true);
            provider.Set("Logo", "PCL/Logo.png");
            provider.Set("LogoCustom", true);
        }

        provider.Sync();
    }

    private static void PersistKnownMinecraftVersion(YamlFileProvider provider, string? minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return;
        }

        var trimmedVersion = minecraftVersion.Trim();
        provider.Set("VersionVanillaName", trimmedVersion);
        provider.Set("VersionVanilla", FrontendVersionManifestInspector.ParseComparableVanillaVersion(trimmedVersion).ToString());
    }
}
