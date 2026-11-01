using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Java;

public static class JavaBrandDetector
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

    public static JavaBrandType Detect(string? vendorText)
    {
        if (string.IsNullOrWhiteSpace(vendorText)) return JavaBrandType.Unknown;
        var match = BrandMap.Keys.FirstOrDefault(key => vendorText.Contains(key, StringComparison.OrdinalIgnoreCase));
        return match == null ? JavaBrandType.Unknown : BrandMap[match];
    }
}
