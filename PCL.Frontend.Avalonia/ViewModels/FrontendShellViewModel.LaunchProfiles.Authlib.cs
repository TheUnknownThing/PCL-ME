using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string DefaultAuthlibServer = "https://littleskin.cn/api/yggdrasil";

    public string LaunchAuthlibServer
    {
        get => _launchAuthlibServer;
        set => SetProperty(ref _launchAuthlibServer, value);
    }

    public string LaunchAuthlibLoginName
    {
        get => _launchAuthlibLoginName;
        set => SetProperty(ref _launchAuthlibLoginName, value);
    }

    public string LaunchAuthlibPassword
    {
        get => _launchAuthlibPassword;
        set => SetProperty(ref _launchAuthlibPassword, value);
    }

    public string LaunchAuthlibStatusText
    {
        get => _launchAuthlibStatusText;
        private set => SetProperty(ref _launchAuthlibStatusText, value);
    }

    public bool HasLaunchAuthlibStatus => !string.IsNullOrWhiteSpace(LaunchAuthlibStatusText);

    private Task LoginAuthlibLaunchProfileAsync()
    {
        PopulateAuthlibDefaults();
        LaunchAuthlibStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.AuthlibEditor);
        return Task.CompletedTask;
    }

    private async Task SubmitAuthlibLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.authlib_login")))
        {
            return;
        }

        try
        {
            var serverBaseUrl = NormalizeAuthlibServerBaseUrl(LaunchAuthlibServer);
            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.invalid_server");
                return;
            }

            var loginName = LaunchAuthlibLoginName.Trim();
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(LaunchAuthlibPassword))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.required_fields");
                return;
            }

            var requestedClientToken = CreateAuthlibClientToken();
            var authenticateJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                    serverBaseUrl,
                    loginName,
                    LaunchAuthlibPassword,
                    requestedClientToken));
            var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
                new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                    ForceReselectProfile: false,
                    CachedProfileId: null,
                    AuthenticateResponseJson: authenticateJson,
                    RequestedClientToken: requestedClientToken));
            if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
            {
                LaunchAuthlibStatusText = (authenticatePlan.FailureMessage ?? T("launch.profile.authlib.errors.login_failed")).TrimStart('$');
                return;
            }

            var selectedProfileId = authenticatePlan.SelectedProfileId
                                    ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
            if (string.IsNullOrWhiteSpace(selectedProfileId))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.no_roles");
                return;
            }

            var metadataJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl);
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
                new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                    authenticatePlan,
                    authenticateJson,
                    metadataJson,
                    IsExistingProfile: false,
                    SelectedProfileIndex: GetSelectedProfileIndex(profileDocument),
                    serverBaseUrl,
                    loginName,
                    LaunchAuthlibPassword,
                    selectedProfileId));
            var mutatedDocument = FrontendProfileStorageService.ApplyMutation(
                profileDocument,
                authenticateResult.MutationPlan,
                out var updatedProfileIndex);
            var completedProfileName = authenticateResult.Session.ProfileName;
            if (authenticateResult.NeedsRefresh)
            {
                var refreshJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                        serverBaseUrl,
                        authenticateResult.Session.AccessToken,
                        authenticateResult.Session.ClientToken,
                        authenticateResult.Session.ProfileName,
                        authenticateResult.Session.ProfileId));
                var refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
                    new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                        refreshJson,
                        updatedProfileIndex ?? GetSelectedProfileIndex(mutatedDocument),
                        serverBaseUrl,
                        authenticateResult.ServerName,
                        loginName,
                        LaunchAuthlibPassword));
                mutatedDocument = FrontendProfileStorageService.ApplyMutation(
                    mutatedDocument,
                    refreshResult.MutationPlan,
                    out _);
                completedProfileName = refreshResult.Session.ProfileName;
            }

            FrontendProfileStorageService.Save(_shellActionService.RuntimePaths, mutatedDocument);
            LaunchAuthlibStatusText = string.Empty;
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.authlib_login"), T("launch.profile.authlib.completed", ("profile_name", completedProfileName)));
        }
        catch (Exception ex)
        {
            LaunchAuthlibStatusText = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity(T("launch.profile.activities.authlib_login_failed"), LaunchAuthlibStatusText);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task<LaunchProfileRefreshResult> RefreshSelectedLaunchProfileCoreAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false,
        Action<string>? onStatusChanged = null)
    {
        if (!TryGetSelectedStoredLaunchProfile(out var profileDocument, out var selectedProfileIndex, out var selectedProfile))
        {
            return new LaunchProfileRefreshResult(false, T("launch.profile.selection.empty"));
        }

        return selectedProfile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => await RefreshMicrosoftLaunchProfileAsync(
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            MinecraftLaunchStoredProfileKind.Authlib => await RefreshAuthlibLaunchProfileAsync(
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            _ => new LaunchProfileRefreshResult(false, T("launch.profile.refresh.not_required"))
        };
    }

    private async Task<LaunchProfileRefreshResult> RefreshAuthlibLaunchProfileAsync(
        MinecraftLaunchProfileDocument profileDocument,
        int selectedProfileIndex,
        MinecraftLaunchPersistedProfile selectedProfile,
        CancellationToken cancellationToken,
        bool forceRefresh,
        Action<string>? onStatusChanged)
    {
        var serverBaseUrl = NormalizeAuthlibServerBaseUrl(selectedProfile.Server ?? LaunchAuthlibServer);
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.invalid_server"));
        }

        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
            !string.IsNullOrWhiteSpace(selectedProfile.ClientToken))
        {
            try
            {
                onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.validate_session"));
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildValidateRequest(
                        serverBaseUrl,
                        selectedProfile.AccessToken,
                        selectedProfile.ClientToken),
                    cancellationToken);
                return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.valid", ("profile_name", selectedProfile.Username ?? T("launch.profile.entry.unnamed"))));
            }
            catch (LaunchProfileRequestException)
            {
                // Continue with refresh/authenticate fallback when the cached token is no longer accepted.
            }
        }

        var hasRefreshableSession = !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
                                    !string.IsNullOrWhiteSpace(selectedProfile.ClientToken);
        if (hasRefreshableSession)
        {
            var accessToken = selectedProfile.AccessToken!;
            var clientToken = selectedProfile.ClientToken!;
            try
            {
                onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.refresh_session"));
                var refreshJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                        serverBaseUrl,
                        accessToken,
                        clientToken),
                    cancellationToken);
                var metadataJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl, cancellationToken);
                var serverName = ResolveAuthlibServerName(serverBaseUrl, metadataJson);
                var refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
                    new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                        refreshJson,
                        selectedProfileIndex,
                        serverBaseUrl,
                        serverName,
                        selectedProfile.LoginName ?? string.Empty,
                        selectedProfile.Password ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(selectedProfile.Uuid) &&
                    !string.Equals(refreshResult.Session.ProfileId, selectedProfile.Uuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.profile_changed"));
                }

                FrontendProfileStorageService.Save(
                    _shellActionService.RuntimePaths,
                    FrontendProfileStorageService.ApplyMutation(profileDocument, refreshResult.MutationPlan, out _));
                return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.completed", ("profile_name", refreshResult.Session.ProfileName)), ShouldInvalidateAvatarCache: true);
            }
            catch (LaunchProfileRequestException ex)
            {
                if (IsAuthlibCredentialExpired(ex.ResponseBody))
                {
                    throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.relogin_required"));
                }

                throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.failed", ("message", GetLaunchProfileFriendlyError(ex))));
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.LoginName) || string.IsNullOrWhiteSpace(selectedProfile.Password))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.incomplete_session"));
        }

        onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.reauthenticate"));
        var requestedClientToken = CreateAuthlibClientToken();
        var authenticateJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                serverBaseUrl,
                selectedProfile.LoginName,
                selectedProfile.Password,
                requestedClientToken),
            cancellationToken);
        var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                ForceReselectProfile: false,
                CachedProfileId: selectedProfile.Uuid,
                AuthenticateResponseJson: authenticateJson,
                RequestedClientToken: requestedClientToken));
        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
        {
            throw new InvalidOperationException((authenticatePlan.FailureMessage ?? T("launch.profile.authlib.errors.login_failed")).TrimStart('$'));
        }

        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection &&
            string.IsNullOrWhiteSpace(authenticatePlan.SelectedProfileId))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.multiple_roles"));
        }

        var selectedProfileId = authenticatePlan.SelectedProfileId
                                ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            throw new InvalidOperationException(T("launch.profile.authlib.errors.no_roles"));
        }

        var metadataResponseJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl, cancellationToken);
        var authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
            new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                authenticatePlan,
                authenticateJson,
                metadataResponseJson,
                IsExistingProfile: true,
                SelectedProfileIndex: selectedProfileIndex,
                serverBaseUrl,
                selectedProfile.LoginName,
                selectedProfile.Password,
                selectedProfileId));

        var mutationPlan = authenticateResult.MutationPlan;
        var completedProfileName = authenticateResult.Session.ProfileName;
        if (authenticateResult.NeedsRefresh)
        {
            var refreshJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                    serverBaseUrl,
                    authenticateResult.Session.AccessToken,
                    authenticateResult.Session.ClientToken,
                    authenticateResult.Session.ProfileName,
                    authenticateResult.Session.ProfileId),
                cancellationToken);
            var refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
                new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                    refreshJson,
                    selectedProfileIndex,
                    serverBaseUrl,
                    authenticateResult.ServerName,
                    selectedProfile.LoginName,
                    selectedProfile.Password));
            mutationPlan = refreshResult.MutationPlan;
            completedProfileName = refreshResult.Session.ProfileName;
        }

        FrontendProfileStorageService.Save(
            _shellActionService.RuntimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));
        return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.reauthenticated", ("profile_name", completedProfileName)), ShouldInvalidateAvatarCache: true);
    }

    private void ApplyLittleSkinLaunchProfilePreset()
    {
        LaunchAuthlibServer = DefaultAuthlibServer;
        LaunchAuthlibStatusText = string.Empty;
        AddActivity(T("launch.profile.authlib.activities.use_littleskin"), LaunchAuthlibServer);
    }

    private void PopulateAuthlibDefaults()
    {
        if (_launchComposition.SelectedProfile.Kind == MinecraftLaunchProfileKind.Auth &&
            !string.IsNullOrWhiteSpace(_launchComposition.SelectedProfile.AuthServer))
        {
            LaunchAuthlibServer = _launchComposition.SelectedProfile.AuthServer!;
        }
        else if (!string.IsNullOrWhiteSpace(_instanceComposition.Setup.AuthServer))
        {
            LaunchAuthlibServer = _instanceComposition.Setup.AuthServer;
        }
        else if (string.IsNullOrWhiteSpace(LaunchAuthlibServer))
        {
            LaunchAuthlibServer = DefaultAuthlibServer;
        }
    }

    private async Task<string> TryReadAuthlibMetadataJsonAsync(string serverBaseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(serverBaseUrl),
                cancellationToken);
        }
        catch
        {
            var serverName = TryGetHostLabel(serverBaseUrl) ?? "Authlib Server";
            return JsonSerializer.Serialize(new
            {
                meta = new
                {
                    serverName
                }
            });
        }
    }

    private static string NormalizeAuthlibServerBaseUrl(string serverInput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverInput);

        var normalized = serverInput.Trim().TrimEnd('/');
        if (normalized.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return normalized + "/authserver";
    }

    private static string ResolveAuthlibServerName(string serverBaseUrl, string? metadataResponseJson)
    {
        if (!string.IsNullOrWhiteSpace(metadataResponseJson))
        {
            try
            {
                return MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(metadataResponseJson);
            }
            catch
            {
                try
                {
                    if (JsonNode.Parse(metadataResponseJson) is JsonObject root)
                    {
                        var topLevelName = root["serverName"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(topLevelName))
                        {
                            return topLevelName;
                        }

                        var metaName = root["meta"]?["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(metaName))
                        {
                            return metaName;
                        }
                    }
                }
                catch
                {
                    // Ignore malformed metadata and fallback to host below.
                }
            }
        }

        return Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "Authlib Server";
    }

    private static string? TryGetHostLabel(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static bool IsAuthlibCredentialExpired(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        var error = TryReadJsonField(responseBody, "error");
        var message = TryReadJsonField(responseBody, "errorMessage")
                      ?? TryReadJsonField(responseBody, "message");

        return string.Equals(error, "ForbiddenOperationException", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(message) &&
                message.Contains("Invalid token", StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateAuthlibClientToken()
    {
        return Guid.NewGuid().ToString("N");
    }
}
