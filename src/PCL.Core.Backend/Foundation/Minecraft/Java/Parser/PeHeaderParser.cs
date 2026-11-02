using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Parser;

public class PeHeaderParser : IJavaParser
{
    private static readonly Dictionary<string, JavaBrandType> BrandMap = new()
    {
        ["Eclipse"] = JavaBrandType.EclipseTemurin,
        ["Temurin"] = JavaBrandType.EclipseTemurin,
        ["Bellsoft"] = JavaBrandType.Liberica,
        ["Microsoft"] = JavaBrandType.Microsoft,
        ["Amazon"] = JavaBrandType.Corretto,
        ["Azul"] = JavaBrandType.Zulu,
        ["IBM"] = JavaBrandType.IBMSemeru,
        ["Oracle"] = JavaBrandType.Oracle,
        ["Tencent"] = JavaBrandType.TencentKona,
        ["OpenJDK"] = JavaBrandType.OpenJDK,
        ["Alibaba"] = JavaBrandType.Dragonwell,
        ["GraalVM"] = JavaBrandType.GraalVmCommunity,
        ["JetBrains"] = JavaBrandType.JetBrains
    };

    public JavaInstallation? Parse(string javaExePath)
    {
        try
        {
            if (!File.Exists(javaExePath))
            {
                return null;
            }

            LogWrapper.Info("Java", $"Parsing Java executable metadata for {javaExePath}");

            var versionInfo = FileVersionInfo.GetVersionInfo(javaExePath);
            var fileVersion = Version.Parse(versionInfo.FileVersion ?? "0.0.0.0");
            var companyName = NormalizeCompanyName(versionInfo);
            var brand = DetermineBrand(companyName);

            var javaFolder = Path.GetDirectoryName(javaExePath)!;
            var isJre = !File.Exists(Path.Combine(javaFolder, "javac.exe"));

            var peData = PEHeaderReader.ReadPEHeader(javaExePath);
            var arch = peData.Machine;
            var is64Bit = PEHeaderReader.IsMachine64Bit(arch);

            return new JavaInstallation(
                javaFolder,
                fileVersion,
                brand,
                arch,
                is64Bit,
                isJre);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"[Java] Failed to parse {javaExePath}");
            return null;
        }
    }

    private static string NormalizeCompanyName(FileVersionInfo info)
    {
        var name = info.CompanyName ?? info.FileDescription ?? info.ProductName ?? string.Empty;

        if (name.Contains("Oracle", StringComparison.OrdinalIgnoreCase) || name == "N/A")
        {
            if ((info.FileDescription?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (info.ProductName?.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return "Oracle";
            }

            return "OpenJDK";
        }

        return name;
    }

    private static JavaBrandType DetermineBrand(string output)
    {
        var match = BrandMap.Keys
            .FirstOrDefault(key => output.Contains(key, StringComparison.OrdinalIgnoreCase));
        return match is not null ? BrandMap[match] : JavaBrandType.Unknown;
    }
}
