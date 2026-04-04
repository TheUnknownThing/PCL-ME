Imports PCL.Core.App
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Minecraft.Launch.Utils

Public Module ModLaunchArgumentWorkflowShell

    Public Function BuildLaunchArguments(instance As McInstance,
                                         options As ModLaunch.McLaunchOptions,
                                         selectedJava As JavaEntry,
                                         loginResult As McLoginResult,
                                         authBaseUrl As String,
                                         getNativesFolder As Func(Of String),
                                         loader As LoaderTask(Of String, List(Of McLibToken)),
                                         logMessage As Action(Of String)) As String
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
        If selectedJava Is Nothing Then Throw New ArgumentNullException(NameOf(selectedJava))
        If getNativesFolder Is Nothing Then Throw New ArgumentNullException(NameOf(getNativesFolder))
        If loader Is Nothing Then Throw New ArgumentNullException(NameOf(loader))

        logMessage?.Invoke("开始获取 Minecraft 启动参数")

        Dim arguments As String
        If instance.JsonObject("arguments") IsNot Nothing AndAlso instance.JsonObject("arguments")("jvm") IsNot Nothing Then
            logMessage?.Invoke("获取新版 JVM 参数")
            arguments = BuildModernJvmArguments(instance, selectedJava, loginResult, authBaseUrl, getNativesFolder())
            logMessage?.Invoke("新版 JVM 参数获取成功：")
            logMessage?.Invoke(arguments)
        Else
            logMessage?.Invoke("获取旧版 JVM 参数")
            arguments = BuildLegacyJvmArguments(instance, selectedJava, loginResult, authBaseUrl, getNativesFolder())
            logMessage?.Invoke("旧版 JVM 参数获取成功：")
            logMessage?.Invoke(arguments)
        End If

        If Not String.IsNullOrEmpty(instance.JsonObject("minecraftArguments")) Then
            logMessage?.Invoke("获取旧版 Game 参数")
            arguments += " " & BuildLegacyGameArguments(instance)
            logMessage?.Invoke("旧版 Game 参数获取成功")
        End If
        If instance.JsonObject("arguments") IsNot Nothing AndAlso instance.JsonObject("arguments")("game") IsNot Nothing Then
            logMessage?.Invoke("获取新版 Game 参数")
            arguments += " " & BuildModernGameArguments(instance)
            logMessage?.Invoke("新版 Game 参数获取成功")
        End If

        Dim argumentGame As String = Setup.Get("VersionAdvanceGame", instance:=instance)
        Dim replaceArguments = BuildReplacementArguments(instance, selectedJava, loginResult, getNativesFolder, loader, logMessage)
        Dim server As String = If(String.IsNullOrEmpty(options.ServerIp), Setup.Get("VersionServerEnter", instance), options.ServerIp)
        Dim argumentPlan = MinecraftLaunchArgumentWorkflowService.BuildPlan(
            New MinecraftLaunchArgumentPlanRequest(
                arguments,
                selectedJava.Installation.MajorVersion,
                Setup.Get("LaunchArgumentWindowType") = 0,
                options.ExtraArgs,
                If(argumentGame = "", Setup.Get("LaunchAdvanceGame"), argumentGame),
                replaceArguments,
                options.WorldName,
                server,
                instance.ReleaseTime,
                instance.Info.HasOptiFine))
        If argumentPlan.ShouldWarnAboutLegacyServerWithOptiFine Then
            Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintType.Critical)
        End If

        logMessage?.Invoke("Minecraft 启动参数：")
        logMessage?.Invoke(argumentPlan.FinalArguments)
        Return argumentPlan.FinalArguments
    End Function

    Private Function BuildLegacyJvmArguments(instance As McInstance,
                                             selectedJava As JavaEntry,
                                             loginResult As McLoginResult,
                                             authBaseUrl As String,
                                             nativesFolder As String) As String
        Dim totalMemory = Math.Floor(PageInstanceSetup.GetRam(instance, Not selectedJava.Installation.Is64Bit) * 1024)
        Dim youngMemory = Math.Floor(PageInstanceSetup.GetRam(instance, Not selectedJava.Installation.Is64Bit) * 1024 * 0.15)
        Dim proxyAddress = ModLaunchArgumentShell.TryGetLaunchProxyAddress(instance)

        Return MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
            New MinecraftLaunchLegacyJvmArgumentRequest(
                ModLaunchArgumentShell.GetSelectedJvmArgumentOverrides(instance),
                youngMemory,
                totalMemory,
                nativesFolder,
                selectedJava.Installation.MajorVersion,
                ModLaunchArgumentShell.BuildAuthlibInjectorArgument(loginResult.Type, authBaseUrl, PathPure, includeDetailedHttpError:=True),
                ModLaunchArgumentShell.GetDebugLog4jConfigurationPath(instance),
                ModLaunchArgumentShell.GetRendererAgentArgument(instance, PathPure),
                If(proxyAddress Is Nothing, Nothing, ModLaunchArgumentShell.GetLaunchProxyScheme(proxyAddress)),
                If(proxyAddress Is Nothing, Nothing, proxyAddress.AbsoluteUri),
                If(proxyAddress Is Nothing, CType(Nothing, Integer?), proxyAddress.Port),
                ModLaunchArgumentShell.ShouldUseJavaWrapper(instance),
                PathPure.TrimEnd("\"),
                If(ModLaunchArgumentShell.ShouldUseJavaWrapper(instance), ModLaunchArgumentShell.ExtractJavaWrapperShell(PathPure), Nothing),
                ModLaunchArgumentShell.GetMainClassOrThrow(instance)))
    End Function

    Private Function BuildModernJvmArguments(instance As McInstance,
                                             selectedJava As JavaEntry,
                                             loginResult As McLoginResult,
                                             authBaseUrl As String,
                                             nativesFolder As String) As String
        Dim totalMemory = Math.Floor(PageInstanceSetup.GetRam(instance) * 1024)
        Dim youngMemory = Math.Floor(PageInstanceSetup.GetRam(instance) * 1024 * 0.15)
        Dim proxyAddress = ModLaunchArgumentShell.TryGetLaunchProxyAddress(instance)

        Return MinecraftLaunchJvmArgumentService.BuildModernArguments(
            New MinecraftLaunchModernJvmArgumentRequest(
                MinecraftLaunchJsonArgumentService.ExtractValues(
                    New MinecraftLaunchJsonArgumentRequest(
                        ModLaunchArgumentShell.CollectArgumentSectionJsons(instance, "jvm"),
                        Environment.OSVersion.Version.ToString(),
                        Is32BitSystem)).
                    ToList(),
                ModLaunchArgumentShell.GetSelectedJvmArgumentOverrides(instance),
                CType(Setup.Get("LaunchPreferredIpStack"), JvmPreferredIpStack),
                youngMemory,
                totalMemory,
                ModLaunchArgumentShell.ShouldUseRetroWrapper(instance),
                selectedJava.Installation.MajorVersion,
                ModLaunchArgumentShell.BuildAuthlibInjectorArgument(loginResult.Type, authBaseUrl, PathPure, includeDetailedHttpError:=False),
                ModLaunchArgumentShell.GetDebugLog4jConfigurationPath(instance),
                ModLaunchArgumentShell.GetRendererAgentArgument(instance, PathPure),
                If(proxyAddress Is Nothing, Nothing, ModLaunchArgumentShell.GetLaunchProxyScheme(proxyAddress)),
                If(proxyAddress Is Nothing, Nothing, proxyAddress.AbsoluteUri),
                If(proxyAddress Is Nothing, CType(Nothing, Integer?), proxyAddress.Port),
                ModLaunchArgumentShell.ShouldUseJavaWrapper(instance),
                PathPure.TrimEnd("\"),
                If(ModLaunchArgumentShell.ShouldUseJavaWrapper(instance), ModLaunchArgumentShell.ExtractJavaWrapperShell(PathPure), Nothing),
                ModLaunchArgumentShell.GetMainClassOrThrow(instance)))
    End Function

    Private Function BuildLegacyGameArguments(instance As McInstance) As String
        Dim plan = MinecraftLaunchGameArgumentService.BuildLegacyPlan(
            New MinecraftLaunchLegacyGameArgumentRequest(
                instance.JsonObject("minecraftArguments").ToString(),
                ModLaunchArgumentShell.ShouldUseRetroWrapper(instance),
                instance.Info.HasForge OrElse instance.Info.HasLiteLoader,
                instance.Info.HasOptiFine))
        ModLaunchArgumentShell.ApplyGameArgumentPlan(plan, instance)
        Return plan.Arguments
    End Function

    Private Function BuildModernGameArguments(instance As McInstance) As String
        Dim plan = MinecraftLaunchGameArgumentService.BuildModernPlan(
            New MinecraftLaunchModernGameArgumentRequest(
                MinecraftLaunchJsonArgumentService.ExtractValues(
                    New MinecraftLaunchJsonArgumentRequest(
                        ModLaunchArgumentShell.CollectArgumentSectionJsons(instance, "game"),
                        Environment.OSVersion.Version.ToString(),
                        Is32BitSystem)).
                    ToList(),
                instance.Info.HasForge OrElse instance.Info.HasLiteLoader,
                instance.Info.HasOptiFine))
        ModLaunchArgumentShell.ApplyGameArgumentPlan(plan, instance)
        Return plan.Arguments
    End Function

    Private Function BuildReplacementArguments(instance As McInstance,
                                               selectedJava As JavaEntry,
                                               loginResult As McLoginResult,
                                               getNativesFolder As Func(Of String),
                                               loader As LoaderTask(Of String, List(Of McLibToken)),
                                               logMessage As Action(Of String)) As Dictionary(Of String, String)
        Dim argumentInfo As String = Setup.Get("VersionArgumentInfo", instance:=instance)

        Dim launcherWindowWidth As Double? = Nothing
        Dim launcherWindowHeight As Double? = Nothing
        If Setup.Get("LaunchArgumentWindowType") = 2 Then
            RunInUiWait(
                Sub()
                    launcherWindowWidth = GetPixelSize(FrmMain.PanForm.ActualWidth)
                    launcherWindowHeight = GetPixelSize(FrmMain.PanForm.ActualHeight)
                End Sub)
        End If

        Dim resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(
            New MinecraftLaunchResolutionRequest(
                CInt(Setup.Get("LaunchArgumentWindowType")),
                launcherWindowWidth,
                launcherWindowHeight,
                29.5 * DPI / 96,
                CInt(Setup.Get("LaunchArgumentWindowWidth")),
                CInt(Setup.Get("LaunchArgumentWindowHeight")),
                instance.Info.Drop,
                selectedJava.Installation.MajorVersion,
                selectedJava.Installation.Version.Revision,
                instance.Info.HasOptiFine,
                instance.Info.HasForge,
                DPI / 96))
        If resolutionPlan.LogMessage IsNot Nothing Then logMessage?.Invoke(resolutionPlan.LogMessage)

        Dim libList As List(Of McLibToken) = McLibListGet(instance, True)
        loader.Output = libList
        Dim retroWrapperPath As String = Nothing
        If ModLaunchArgumentShell.ShouldUseRetroWrapper(instance) Then
            Dim wrapperPath As String = McFolderSelected & "libraries\retrowrapper\RetroWrapper.jar"
            Try
                WriteFile(wrapperPath, GetResourceStream("Resources/retro-wrapper.jar"))
                retroWrapperPath = ShortenPath(wrapperPath)
            Catch ex As Exception
                Log(ex, "RetroWrapper 释放失败")
            End Try
        End If

        Dim classpathPlan = MinecraftLaunchClasspathService.BuildPlan(
            New MinecraftLaunchClasspathRequest(
                libList.Select(Function(library) New MinecraftLaunchClasspathLibrary(
                    library.Name,
                    ShortenPath(library.LocalPath),
                    library.IsNatives)).ToList(),
                Config.Instance.ClasspathHead(instance.PathInstance).
                    Split(";"c).
                    Where(Function(library) Not String.IsNullOrWhiteSpace(library)).
                    Select(Function(library) ShortenPath(library)).
                    ToList(),
                retroWrapperPath,
                ";"))

        Dim replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(
            New MinecraftLaunchReplacementValueRequest(
                ";",
                ShortenPath(getNativesFolder()),
                ShortenPath(McFolderSelected & "libraries"),
                ShortenPath(McFolderSelected & "libraries"),
                "PCLCE",
                VersionCode.ToString(),
                instance.Name,
                If(argumentInfo = "", Setup.Get("LaunchArgumentInfo"), argumentInfo),
                ShortenPath(Left(instance.PathIndie, instance.PathIndie.Count - 1)),
                ShortenPath(McFolderSelected & "assets"),
                "{}",
                If(loginResult.Name, ""),
                If(loginResult.Uuid, ""),
                If(loginResult.AccessToken, ""),
                "msa",
                resolutionPlan.Width,
                resolutionPlan.Height,
                ShortenPath(McFolderSelected & "assets\virtual\legacy"),
                McAssetsGetIndexName(instance),
                classpathPlan.JoinedClasspath))

        Dim gameArguments As New Dictionary(Of String, String)
        For Each entry In replacementPlan.Values
            gameArguments.Add(entry.Key, entry.Value)
        Next
        Return gameArguments
    End Function

End Module
