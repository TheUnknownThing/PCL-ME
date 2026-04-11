using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PCL.Core.App.Essentials;

internal interface ILauncherPlatformSecretStore
{
    bool IsSupported { get; }
    byte[] ReadSecret(string secretId);
    void WriteSecret(string secretId, byte[] secretValue);
}

internal sealed class LauncherProcessPlatformSecretStore : ILauncherPlatformSecretStore
{
    private const string LinuxToolName = "secret-tool";
    private const string MacToolName = "security";
    private const string LinuxApplicationAttributeKey = "application";
    private const string LinuxApplicationAttributeValue = "PCLCE";
    private const string LinuxSecretAttributeKey = "secret-id";
    private const string SecretLabel = "PCL CE Encryption Key";
    private const string MacServiceName = "org.pcl.community.pclce.encryption-key";

    public bool IsSupported =>
        OperatingSystem.IsMacOS() ? CanResolveCommand(MacToolName) :
        OperatingSystem.IsLinux() ? CanResolveCommand(LinuxToolName) :
        false;

    public byte[] ReadSecret(string secretId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);

        var base64 = OperatingSystem.IsMacOS()
            ? RunCommand(
                MacToolName,
                [
                    "find-generic-password",
                    "-w",
                    "-a",
                    secretId,
                    "-s",
                    MacServiceName
                ])
            : OperatingSystem.IsLinux()
                ? RunCommand(
                    LinuxToolName,
                    [
                        "lookup",
                        LinuxApplicationAttributeKey,
                        LinuxApplicationAttributeValue,
                        LinuxSecretAttributeKey,
                        secretId
                    ])
                : throw new PlatformNotSupportedException("Platform secret storage is not supported on this platform.");

        try
        {
            return Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Stored launcher key payload is invalid.", ex);
        }
    }

    public void WriteSecret(string secretId, byte[] secretValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
        ArgumentNullException.ThrowIfNull(secretValue);

        var base64 = Convert.ToBase64String(secretValue);
        if (OperatingSystem.IsMacOS())
        {
            RunCommand(
                MacToolName,
                [
                    "add-generic-password",
                    "-U",
                    "-a",
                    secretId,
                    "-s",
                    MacServiceName,
                    "-w",
                    base64
                ]);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            TryClearLinuxSecret(secretId);
            RunCommand(
                LinuxToolName,
                [
                    "store",
                    $"--label={SecretLabel}",
                    LinuxApplicationAttributeKey,
                    LinuxApplicationAttributeValue,
                    LinuxSecretAttributeKey,
                    secretId
                ],
                standardInput: base64);
            return;
        }

        throw new PlatformNotSupportedException("Platform secret storage is not supported on this platform.");
    }

    private static void TryClearLinuxSecret(string secretId)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            RunCommand(
                LinuxToolName,
                [
                    "clear",
                    LinuxApplicationAttributeKey,
                    LinuxApplicationAttributeValue,
                    LinuxSecretAttributeKey,
                    secretId
                ]);
        }
        catch
        {
            // Best effort. Some secret-service implementations simply return a not-found error here.
        }
    }

    private static bool CanResolveCommand(string commandName)
    {
        if (Path.IsPathRooted(commandName))
        {
            return File.Exists(commandName);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            if (File.Exists(Path.Combine(segment, commandName)))
            {
                return true;
            }
        }

        return false;
    }

    private static string RunCommand(string fileName, IReadOnlyList<string> arguments, string? standardInput = null)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardInput = standardInput is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            if (standardInput is not null)
            {
                process.StandardInput.Write(standardInput);
                process.StandardInput.Close();
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"The platform secret-store command '{fileName}' failed."
                        : error.Trim());
            }

            return output;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"The platform secret-store command '{fileName}' is unavailable on this system.",
                ex);
        }
    }
}
