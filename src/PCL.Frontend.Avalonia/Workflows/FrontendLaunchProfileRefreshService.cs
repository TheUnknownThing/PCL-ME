using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchProfileRefreshService
{
    public static async Task<FrontendLaunchProfileRefreshResult> RefreshSelectedProfileAsync(
        FrontendRuntimePaths runtimePaths,
        II18nService i18n,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false,
        Action<string>? onStatusChanged = null)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(i18n);

        if (!TryGetSelectedStoredLaunchProfile(runtimePaths, out var profileDocument, out var selectedProfileIndex, out var selectedProfile))
        {
            return new FrontendLaunchProfileRefreshResult(false, i18n.T("launch.profile.selection.empty"));
        }

        return selectedProfile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => await RefreshMicrosoftLaunchProfileAsync(
                runtimePaths,
                i18n,
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            MinecraftLaunchStoredProfileKind.Authlib => await RefreshAuthlibLaunchProfileAsync(
                runtimePaths,
                i18n,
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            _ => new FrontendLaunchProfileRefreshResult(false, i18n.T("launch.profile.refresh.not_required"))
        };
    }

    private static async Task<FrontendLaunchProfileRefreshResult> RefreshMicrosoftLaunchProfileAsync(
        FrontendRuntimePaths runtimePaths,
        II18nService i18n,
        MinecraftLaunchProfileDocument profileDocument,
        int selectedProfileIndex,
        MinecraftLaunchPersistedProfile selectedProfile,
        CancellationToken cancellationToken,
        bool forceRefresh,
        Action<string>? onStatusChanged)
    {
        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken))
        {
            try
            {
                onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.validate_session"));
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(selectedProfile.AccessToken),
                    cancellationToken);
                return new FrontendLaunchProfileRefreshResult(true, i18n.T("launch.profile.refresh.microsoft.valid", new Dictionary<string, object?>
                {
                    ["profile_name"] = selectedProfile.Username ?? i18n.T("launch.profile.entry.unnamed")
                }));
            }
            catch (LaunchProfileRequestException)
            {
                // Fall back to the refresh workflow when the access token is no longer accepted.
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.RefreshToken))
        {
            throw new InvalidOperationException(i18n.T("launch.profile.refresh.microsoft.errors.missing_refresh_token"));
        }

        var clientId = ResolveMicrosoftClientId(i18n);
        onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.refresh_account"));

        string oauthRefreshJson;
        try
        {
            oauthRefreshJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildOAuthRefreshRequest(selectedProfile.RefreshToken, clientId),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex)
        {
            var resolution = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveOAuthRefreshFailure(GetLaunchProfileFriendlyError(i18n, ex));
            if (resolution.Kind == MinecraftLaunchMicrosoftFailureResolutionKind.RequireRelogin)
            {
                throw new InvalidOperationException(i18n.T("launch.profile.refresh.microsoft.errors.relogin_required"));
            }

            throw new InvalidOperationException(i18n.T("launch.profile.refresh.microsoft.errors.failed", new Dictionary<string, object?>
            {
                ["message"] = GetLaunchProfileFriendlyError(i18n, ex)
            }));
        }

        var oauthTokens = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(oauthRefreshJson);

        onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.get_xbox_live_token"));
        var xboxLiveResponseJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(oauthTokens.AccessToken),
            cancellationToken);

        string xstsResponseJson;
        try
        {
            onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.get_xsts_token"));
            xstsResponseJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(
                    MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(xboxLiveResponseJson)),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex)
        {
            var prompt = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(ex.ResponseBody);
            if (prompt is not null)
            {
                throw new InvalidOperationException(i18n.T(prompt.MessageText));
            }

            throw new InvalidOperationException(i18n.T("launch.profile.refresh.microsoft.errors.failed", new Dictionary<string, object?>
            {
                ["message"] = GetLaunchProfileFriendlyError(i18n, ex)
            }));
        }

        var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(xstsResponseJson);

        onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.get_minecraft_access_token"));
        var minecraftAccessTokenJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(
                xstsResponse.UserHash,
                xstsResponse.Token),
            cancellationToken);
        var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(minecraftAccessTokenJson);

        onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.check_ownership"));
        var ownershipJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(minecraftAccessToken),
            cancellationToken);
        if (!MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(ownershipJson))
        {
            throw new InvalidOperationException(i18n.T(MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt().MessageText));
        }

        string profileJson;
        try
        {
            onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.microsoft.sync_profile"));
            profileJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(minecraftAccessToken),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(i18n.T(MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt().MessageText));
        }

        var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(profileJson);
        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: false,
                SelectedProfileIndex: selectedProfileIndex,
                profileDocument.Profiles.Select(ToStoredProfile).ToArray(),
                profileResponse.Uuid,
                profileResponse.UserName,
                minecraftAccessToken,
                oauthTokens.RefreshToken,
                profileResponse.ProfileJson));

        FrontendProfileStorageService.Save(
            runtimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));

        return new FrontendLaunchProfileRefreshResult(
            true,
            i18n.T("launch.profile.refresh.microsoft.completed", new Dictionary<string, object?>
            {
                ["profile_name"] = profileResponse.UserName
            }),
            ShouldInvalidateAvatarCache: true);
    }

    private static async Task<FrontendLaunchProfileRefreshResult> RefreshAuthlibLaunchProfileAsync(
        FrontendRuntimePaths runtimePaths,
        II18nService i18n,
        MinecraftLaunchProfileDocument profileDocument,
        int selectedProfileIndex,
        MinecraftLaunchPersistedProfile selectedProfile,
        CancellationToken cancellationToken,
        bool forceRefresh,
        Action<string>? onStatusChanged)
    {
        var serverBaseUrl = NormalizeAuthlibServerBaseUrl(selectedProfile.Server);
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.invalid_server"));
        }

        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
            !string.IsNullOrWhiteSpace(selectedProfile.ClientToken))
        {
            try
            {
                onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.authlib.validate_session"));
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildValidateRequest(
                        serverBaseUrl,
                        selectedProfile.AccessToken,
                        selectedProfile.ClientToken),
                    cancellationToken);
                return new FrontendLaunchProfileRefreshResult(true, i18n.T("launch.profile.refresh.authlib.valid", new Dictionary<string, object?>
                {
                    ["profile_name"] = selectedProfile.Username ?? i18n.T("launch.profile.entry.unnamed")
                }));
            }
            catch (LaunchProfileRequestException)
            {
                // Continue with refresh/reauthentication.
            }
        }

        var hasRefreshableSession = !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
                                    !string.IsNullOrWhiteSpace(selectedProfile.ClientToken);
        if (hasRefreshableSession)
        {
            try
            {
                onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.authlib.refresh_session"));
                var refreshJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                        serverBaseUrl,
                        selectedProfile.AccessToken!,
                        selectedProfile.ClientToken!),
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
                    throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.profile_changed"));
                }

                FrontendProfileStorageService.Save(
                    runtimePaths,
                    FrontendProfileStorageService.ApplyMutation(profileDocument, refreshResult.MutationPlan, out _));
                return new FrontendLaunchProfileRefreshResult(
                    true,
                    i18n.T("launch.profile.refresh.authlib.completed", new Dictionary<string, object?>
                    {
                        ["profile_name"] = refreshResult.Session.ProfileName
                    }),
                    ShouldInvalidateAvatarCache: true);
            }
            catch (LaunchProfileRequestException ex)
            {
                if (IsAuthlibCredentialExpired(ex.ResponseBody))
                {
                    throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.relogin_required"));
                }

                throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.failed", new Dictionary<string, object?>
                {
                    ["message"] = GetLaunchProfileFriendlyError(i18n, ex)
                }));
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.LoginName) || string.IsNullOrWhiteSpace(selectedProfile.Password))
        {
            throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.incomplete_session"));
        }

        onStatusChanged?.Invoke(i18n.T("launch.profile.refresh.authlib.reauthenticate"));
        var requestedClientToken = Guid.NewGuid().ToString("N");
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
            throw new InvalidOperationException((authenticatePlan.FailureMessage ?? i18n.T("launch.profile.authlib.errors.login_failed")).TrimStart('$'));
        }

        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection &&
            string.IsNullOrWhiteSpace(authenticatePlan.SelectedProfileId))
        {
            throw new InvalidOperationException(i18n.T("launch.profile.refresh.authlib.errors.multiple_roles"));
        }

        var selectedProfileId = authenticatePlan.SelectedProfileId
                                ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            throw new InvalidOperationException(i18n.T("launch.profile.authlib.errors.no_roles"));
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
            runtimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));
        return new FrontendLaunchProfileRefreshResult(
            true,
            i18n.T("launch.profile.refresh.authlib.reauthenticated", new Dictionary<string, object?>
            {
                ["profile_name"] = completedProfileName
            }),
            ShouldInvalidateAvatarCache: true);
    }

    private static async Task<string> SendLaunchProfileRequestAsync(
        MinecraftLaunchHttpRequestPlan plan,
        CancellationToken cancellationToken)
    {
        using var client = CreateLaunchProfileHttpClient();
        using var request = new HttpRequestMessage(new HttpMethod(plan.Method), plan.Url);
        if (plan.Headers is not null)
        {
            foreach (var header in plan.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(plan.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plan.BearerToken);
        }

        if (!string.IsNullOrWhiteSpace(plan.Body))
        {
            request.Content = new StringContent(
                plan.Body,
                Encoding.UTF8,
                string.IsNullOrWhiteSpace(plan.ContentType) ? "application/json" : plan.ContentType);
        }

        if (IsAuthlibRequest(plan.Url))
        {
            LogWrapper.Info("Authlib", $"HTTP {plan.Method} {plan.Url}");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (IsAuthlibRequest(plan.Url))
            {
                LogWrapper.Warn("Authlib", $"HTTP {(int)response.StatusCode} {plan.Method} {plan.Url} failed: {SummarizeForLog(responseBody)}");
            }

            throw new LaunchProfileRequestException(plan.Url, (int)response.StatusCode, responseBody);
        }

        if (IsAuthlibRequest(plan.Url))
        {
            LogWrapper.Info("Authlib", $"HTTP {(int)response.StatusCode} {plan.Method} {plan.Url} succeeded: {SummarizeForLog(responseBody)}");
        }

        return responseBody;
    }

    private static HttpClient CreateLaunchProfileHttpClient()
    {
        var client = FrontendHttpProxyService.CreateLauncherHttpClient(
            TimeSpan.FromSeconds(30),
            "PCL-ME-Avalonia/1.0");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static bool TryGetSelectedStoredLaunchProfile(
        FrontendRuntimePaths runtimePaths,
        out MinecraftLaunchProfileDocument document,
        out int selectedProfileIndex,
        out MinecraftLaunchPersistedProfile selectedProfile)
    {
        document = FrontendProfileStorageService.Load(runtimePaths).Document;
        if (document.Profiles.Count == 0)
        {
            selectedProfileIndex = 0;
            selectedProfile = null!;
            return false;
        }

        selectedProfileIndex = GetSelectedProfileIndex(document);
        if (selectedProfileIndex < 0 || selectedProfileIndex >= document.Profiles.Count)
        {
            selectedProfile = null!;
            return false;
        }

        selectedProfile = document.Profiles[selectedProfileIndex];
        return true;
    }

    private static int GetSelectedProfileIndex(MinecraftLaunchProfileDocument document)
    {
        if (document.Profiles.Count == 0)
        {
            return 0;
        }

        return document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
            ? document.LastUsedProfile
            : 0;
    }

    private static string ResolveMicrosoftClientId(II18nService i18n)
    {
        var clientId = FrontendEmbeddedSecrets.GetMicrosoftClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException(i18n.T("launch.profile.microsoft.errors.missing_client_id"), "MS_CLIENT_ID");
        }

        return clientId;
    }

    private static async Task<string> TryReadAuthlibMetadataJsonAsync(string serverBaseUrl, CancellationToken cancellationToken)
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

    private static string NormalizeAuthlibServerBaseUrl(string? serverInput)
    {
        if (string.IsNullOrWhiteSpace(serverInput))
        {
            return string.Empty;
        }

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
                    // Ignore malformed metadata and fall back to the host name.
                }
            }
        }

        return Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "Authlib Server";
    }

    private static string GetLaunchProfileFriendlyError(II18nService i18n, Exception exception)
    {
        if (exception is LaunchProfileRequestException requestException)
        {
            var directMessage = TryReadJsonField(requestException.ResponseBody, "errorMessage")
                                ?? TryReadJsonField(requestException.ResponseBody, "message")
                                ?? TryReadJsonField(requestException.ResponseBody, "error_description")
                                ?? TryReadJsonField(requestException.ResponseBody, "error");
            if (!string.IsNullOrWhiteSpace(directMessage))
            {
                return directMessage.Trim().TrimStart('$');
            }

            return i18n.T("launch.profile.refresh.errors.request_failed_status", new Dictionary<string, object?>
            {
                ["status_code"] = requestException.StatusCode
            });
        }

        return exception.Message.Trim().TrimStart('$');
    }

    private static string? TryReadJsonField(string json, string propertyName)
    {
        try
        {
            return JsonNode.Parse(json)?[propertyName]?.ToString();
        }
        catch
        {
            return null;
        }
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

    private static bool IsAuthlibRequest(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("/authserver", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("yggdrasil", StringComparison.OrdinalIgnoreCase);
    }

    private static string SummarizeForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var redacted = Regex.Replace(
            value,
            "\"(accessToken|refreshToken|clientToken|password)\"\\s*:\\s*\"[^\"]*\"",
            "\"$1\":\"***\"",
            RegexOptions.IgnoreCase);
        redacted = redacted.Replace(Environment.NewLine, " ");
        redacted = redacted.Replace("\n", " ").Replace("\r", " ");
        return redacted.Length > 320
            ? redacted[..320] + "..."
            : redacted;
    }

    private static string? TryGetHostLabel(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static MinecraftLaunchStoredProfile ToStoredProfile(MinecraftLaunchPersistedProfile profile)
    {
        return new MinecraftLaunchStoredProfile(
            profile.Kind,
            profile.Uuid ?? string.Empty,
            profile.Username ?? string.Empty,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.LoginName,
            profile.Password,
            profile.ClientToken,
            profile.SkinHeadId,
            profile.RawJson);
    }

    private sealed class LaunchProfileRequestException(string url, int statusCode, string responseBody)
        : Exception($"HTTP {statusCode}: {url}")
    {
        public int StatusCode { get; } = statusCode;

        public string ResponseBody { get; } = responseBody;
    }
}

internal sealed record FrontendLaunchProfileRefreshResult(
    bool WasChecked,
    string Message,
    bool ShouldInvalidateAvatarCache = false);
