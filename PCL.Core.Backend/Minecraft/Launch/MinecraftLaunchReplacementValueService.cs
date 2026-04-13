using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchReplacementValueService
{
    public static MinecraftLaunchReplacementValuePlan BuildPlan(MinecraftLaunchReplacementValueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["${classpath_separator}"] = request.ClasspathSeparator,
            ["${natives_directory}"] = request.NativesDirectory,
            ["${library_directory}"] = request.LibraryDirectory,
            ["${libraries_directory}"] = request.LibrariesDirectory,
            ["${launcher_name}"] = request.LauncherName,
            ["${launcher_version}"] = request.LauncherVersion,
            ["${version_name}"] = request.VersionName,
            ["${version_type}"] = request.VersionType,
            ["${game_directory}"] = request.GameDirectory,
            ["${assets_root}"] = request.AssetsRoot,
            ["${user_properties}"] = request.UserProperties,
            ["${auth_player_name}"] = request.AuthPlayerName,
            ["${auth_uuid}"] = request.AuthUuid,
            ["${auth_access_token}"] = request.AccessToken,
            ["${access_token}"] = request.AccessToken,
            ["${auth_session}"] = request.AccessToken,
            ["${user_type}"] = request.UserType,
            ["${resolution_width}"] = request.ResolutionWidth.ToString(CultureInfo.InvariantCulture),
            ["${resolution_height}"] = request.ResolutionHeight.ToString(CultureInfo.InvariantCulture),
            ["${game_assets}"] = request.GameAssetsDirectory,
            ["${assets_index_name}"] = request.AssetsIndexName,
            ["${classpath}"] = request.Classpath
        };

        return new MinecraftLaunchReplacementValuePlan(values);
    }
}

public sealed record MinecraftLaunchReplacementValueRequest(
    string ClasspathSeparator,
    string NativesDirectory,
    string LibraryDirectory,
    string LibrariesDirectory,
    string LauncherName,
    string LauncherVersion,
    string VersionName,
    string VersionType,
    string GameDirectory,
    string AssetsRoot,
    string UserProperties,
    string AuthPlayerName,
    string AuthUuid,
    string AccessToken,
    string UserType,
    int ResolutionWidth,
    int ResolutionHeight,
    string GameAssetsDirectory,
    string AssetsIndexName,
    string Classpath);

public sealed record MinecraftLaunchReplacementValuePlan(
    IReadOnlyDictionary<string, string> Values);
