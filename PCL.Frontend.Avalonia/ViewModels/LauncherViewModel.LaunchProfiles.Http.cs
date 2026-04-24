using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private async Task<string> SendLaunchProfileRequestAsync(
        MinecraftLaunchHttpRequestPlan plan,
        CancellationToken cancellationToken = default)
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
                LogWrapper.Warn(
                    "Authlib",
                    $"HTTP {(int)response.StatusCode} {plan.Method} {plan.Url} failed: {SummarizeForLog(responseBody)}");
            }

            throw new LaunchProfileRequestException(plan.Url, (int)response.StatusCode, responseBody);
        }

        if (IsAuthlibRequest(plan.Url))
        {
            LogWrapper.Info(
                "Authlib",
                $"HTTP {(int)response.StatusCode} {plan.Method} {plan.Url} succeeded: {SummarizeForLog(responseBody)}");
        }

        return responseBody;
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

    private HttpClient CreateLaunchProfileHttpClient()
    {
        var client = CreateToolHttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
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

    private string GetLaunchProfileFriendlyError(Exception exception)
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

            return T("launch.profile.refresh.errors.request_failed_status", ("status_code", requestException.StatusCode));
        }

        return exception.Message.Trim().TrimStart('$');
    }
}
