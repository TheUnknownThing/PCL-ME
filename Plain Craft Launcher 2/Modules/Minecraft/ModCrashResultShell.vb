Imports PCL.Core.Logging
Imports PCL.Core.Minecraft
Imports PCL.Core.UI
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Utils.OS

Public Partial Class CrashAnalyzer

    Public Sub Output(IsHandAnalyze As Boolean, Optional ExtraFiles As List(Of String) = Nothing)
        Dim resultText = GetAnalyzeResult(IsHandAnalyze)
        Dim prompt = MinecraftCrashWorkflowService.BuildOutputPrompt(
            New MinecraftCrashOutputPromptRequest(
                resultText,
                IsHandAnalyze,
                DirectFile IsNot Nothing,
                _version IsNot Nothing))
        Dim response = ModCrashPromptShell.ShowOutputPrompt(prompt, DirectFile, PathTemp & "Crash.txt")
        Select Case response.Kind
            Case MinecraftCrashPromptResponseKind.OpenInstanceSettings
                ModCrashPromptShell.OpenInstanceSettings(_version)
            Case MinecraftCrashPromptResponseKind.ExportReport
                If Not ModCrashExportShell.TryExportCurrentReport(TempFolder, OutputFiles, ExtraFiles) Then Return
        End Select
    End Sub

    Private Function GetAnalyzeResult(IsHandAnalyze As Boolean) As String
        If Not CrashReasons.Any() Then
            If IsHandAnalyze Then
                Return "很抱歉，PCL 无法确定错误原因。"
            End If
            Return $"很抱歉，你的游戏出现了一些问题……{vbCrLf}如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。"
        End If

        Dim Results As New List(Of String)
        Const LoaderIncompatibleResultText = "Mod 加载器版本与 Mod 不兼容，请前往 实例设置 - 修改 更换加载器版本。\n\n详细信息：\n"
        For Each Reason In CrashReasons
            Dim Additional As List(Of String) = Reason.Value
            Select Case Reason.Key
                Case CrashReason.Mod文件被解压
                    Results.Add("由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。")
                Case CrashReason.内存不足
                    Results.Add("Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n你可以尝试在 更多 → 百宝箱 中选择 内存优化，然后再启动游戏。\n如果还是不行，请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\h")
                Case CrashReason.使用OpenJ9
                    Results.Add("游戏因为使用 OpenJ9 而崩溃了。\n请在启动设置的 Java 选择一项中改用非 OpenJ9 的 Java，然后再启动游戏。")
                Case CrashReason.使用JDK
                    Results.Add("游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用 JRE 8（Java 8），然后再启动游戏。\n如果你没有安装 JRE 8，你可以从网络中下载、安装一个。")
                Case CrashReason.Java版本过高
                    Results.Add("游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。")
                Case CrashReason.Java版本不兼容
                    Results.Add("游戏不兼容你当前使用的 Java。\n如果没有合适的 Java，可以从网络中下载、安装一个。")
                Case CrashReason.Mod名称包含特殊字符
                    Results.Add("由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号（-）、下划线（_）和小数点，然后再启动游戏。")
                Case CrashReason.MixinBootstrap缺失
                    Results.Add("由于缺失 MixinBootstrap，导致游戏崩溃。\n请尝试安装 MixinBootstrap。若安装后依然崩溃，可以尝试在文件名前添加英文感叹号。")
                Case CrashReason.使用32位Java导致JVM无法分配足够多的内存
                    If Environment.Is64BitOperatingSystem Then
                        Results.Add("你似乎正在使用 32 位 Java，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n请在启动设置的 Java 选择一项中改用 64 位的 Java 再启动游戏，然后再启动游戏。\n如果你没有安装 64 位的 Java，你可以从网络中下载、安装一个。")
                    Else
                        Results.Add("你正在使用 32 位的操作系统，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n你或许只能重装 64 位的操作系统来解决此问题。\n如果你的电脑内存在 2GB 以内，那或许只能换台电脑了……\h")
                    End If
                Case CrashReason.Mod缺少前置或MC版本错误
                    If Additional.Any Then
                        Dim info = Additional.Join("\n - ")
                        If info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint) Then
                            Results.Add(LoaderIncompatibleResultText & info)
                        Else
                            Results.Add("由于未安装正确的前置 Mod，导致游戏退出。\n缺失的依赖项：\n - " & info & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                        End If
                    Else
                        Results.Add("由于未安装正确的前置 Mod，导致游戏退出。\n请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.堆栈分析发现关键字
                    If Additional.Count = 1 Then
                        Results.Add("你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：" & Additional.First & "。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h")
                    Else
                        Results.Add("你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - " & Join(Additional, ", ") & "\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h")
                    End If
                Case CrashReason.堆栈分析发现Mod名称, CrashReason.怀疑Mod导致游戏崩溃
                    If Additional.Count = 1 Then
                        Results.Add("PCL 怀疑名为 " & Additional.First & " 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.确定Mod导致游戏崩溃
                    If Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 导致了游戏出错：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.ModMixin失败
                    If Additional.Count = 0 Then
                        Results.Add("部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\e\h")
                    ElseIf Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 导致了游戏出错：\n - " & Join(Additional, "\n - ") & "\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.Mod配置文件导致游戏崩溃
                    If Additional(1) Is Nothing Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错。\n\e\h")
                    Else
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错：\n其配置文件 " & Additional(1) & " 存在异常，无法读取。")
                    End If
                Case CrashReason.Mod初始化失败
                    If Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 初始化失败，导致游戏出错：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.特定方块导致崩溃
                    If Additional.Count = 1 Then
                        Results.Add("游戏似乎因为方块 " & Additional.First & " 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    Else
                        Results.Add("游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    End If
                Case CrashReason.Mod重复安装
                    If Additional.Count >= 2 Then
                        Results.Add("你重复安装了多个相同的 Mod：\n - " & Join(Additional, "\n - ") & "\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。")
                    Else
                        Results.Add("你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\e\h")
                    End If
                Case CrashReason.特定实体导致崩溃
                    If Additional.Count = 1 Then
                        Results.Add("游戏似乎因为实体 " & Additional.First & " 出现了问题。\n\n你可以创建一个新世界，并生成一个该实体，然后观察游戏的运行情况：\n - 若正常运行，则是该实体导致出错，你或许需要使用一些方式删除此实体。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    Else
                        Results.Add("游戏似乎因为世界中的某些实体出现了问题。\n\n你可以创建一个新世界，并生成各种实体，观察游戏的运行情况：\n - 若正常运行，则是某些实体导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    End If
                Case CrashReason.OptiFine与Forge不兼容
                    Results.Add("由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。")
                Case CrashReason.ShadersMod与OptiFine同时安装
                    Results.Add("无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。")
                Case CrashReason.低版本Forge与高版本Java不兼容
                    Results.Add("由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java")
                Case CrashReason.实例Json中存在多个Forge
                    Results.Add("可能由于其他启动器修改了 Forge 版本，当前实例的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。")
                Case CrashReason.玩家手动触发调试崩溃
                    Results.Add("* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？")
                Case CrashReason.Mod需要Java11
                    Results.Add("你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。")
                Case CrashReason.极短的程序输出
                    Results.Add($"程序返回了以下信息：\n{Additional.First}\n\h")
                Case CrashReason.OptiFine导致无法加载世界
                    Results.Add("你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\h")
                Case CrashReason.显卡驱动不支持导致无法设置像素格式, CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.显卡不支持OpenGL
                    If LogAll.Contains("hd graphics ") Then
                        Results.Add("你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\h")
                    Else
                        Results.Add("你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\h")
                    End If
                Case CrashReason.材质过大或显卡配置不足
                    Results.Add("你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\h")
                Case CrashReason.NightConfig的Bug
                    Results.Add("由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\h")
                Case CrashReason.光影或资源包导致OpenGL1282错误
                    Results.Add("你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\h")
                Case CrashReason.Mod过多导致超出ID限制
                    Results.Add("你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。")
                Case CrashReason.文件或内容校验失败
                    Results.Add("部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\h")
                Case CrashReason.Forge安装不完整
                    Results.Add("由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请前往实例设置重置该实例，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\h")
                Case CrashReason.Fabric报错
                    If Additional.Count = 1 Then
                        Results.Add("Fabric 提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Mod互不兼容
                    If Additional.Count = 1 Then
                        Dim info = Additional.First
                        If info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint) Then
                            Results.Add(LoaderIncompatibleResultText & info)
                        Else
                            Results.Add("你所安装的 Mod 不兼容：\n" & info & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                        End If
                    Else
                        Results.Add("你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Mod加载器报错
                    If Additional.Count = 1 Then
                        Results.Add("Mod 加载器提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Fabric报错并给出解决方案
                    If Additional.Count = 1 Then
                        Results.Add("Fabric 提供了以下解决方案：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Forge报错
                    If Additional.Count = 1 Then
                        Results.Add("Forge 提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.没有可用的分析文件
                    Results.Add("你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\h")
                Case Else
                    Results.Add("PCL 获取到了没有详细信息的错误原因（" & CrashReasons.First.Key & "），请向 PCL 作者提交反馈以获取详情。\h")
            End Select
        Next

        Dim isLauncherLatest = False
        Try
            isLauncherLatest = GetVersionStatus() = VersionStatus.Latest
        Catch ex As Exception
            Log(ex, "确认启动器更新失败", LogLevel.Feedback)
        End Try

        Return Join(Results, "\n\n此外，").
            Replace("\n", vbCrLf).
            Replace("\h", "").
            Replace("\e", If(IsHandAnalyze, "", vbCrLf & "你可以查看错误报告了解错误具体是如何发生的。")).
            Replace(vbCrLf, vbCr).Replace(vbLf, vbCr).Replace(vbCr, vbCrLf).
            Trim(vbCrLf.ToCharArray) &
            If(Not Results.Any(Function(r) r.EndsWithF("\h")) OrElse IsHandAnalyze, "",
                vbCrLf & "如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。" &
                If(isLauncherLatest, "",
                vbCrLf & vbCrLf & "此外，你正在使用老版本 PCL，更新 PCL 或许也能解决这个问题。" & vbCrLf & "你可以点击 设置 → 启动器 → 检查更新 来更新 PCL。"))
    End Function

End Class
