Imports PCL.Core.App
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchSessionArgumentShell

    Public Function BuildWatcherWorkflowRequest(instance As McInstance,
                                                selectedJava As JavaEntry,
                                                loginResult As McLoginResult,
                                                isTestLaunch As Boolean,
                                                getNativesFolder As Func(Of String)) As MinecraftLaunchWatcherWorkflowRequest
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))
        If getNativesFolder Is Nothing Then Throw New ArgumentNullException(NameOf(getNativesFolder))

        Dim allocatedRam = PageInstanceSetup.GetRam(instance, Not selectedJava.Installation.Is64Bit)
        Return New MinecraftLaunchWatcherWorkflowRequest(
            New MinecraftLaunchSessionLogRequest(
                VersionBaseName,
                VersionCode,
                instance.Info.VanillaName,
                If(instance.Info.Vanilla?.ToString(), ""),
                instance.Info.Drop,
                instance.Info.Reliable,
                McAssetsGetIndexName(instance),
                instance.InheritInstanceName,
                allocatedRam,
                McFolderSelected,
                instance.PathInstance,
                instance.PathIndie = instance.PathInstance,
                instance.IsHmclFormatJson,
                If(selectedJava IsNot Nothing, selectedJava.ToString(), Nothing),
                getNativesFolder(),
                loginResult.Name,
                loginResult.AccessToken,
                loginResult.ClientToken,
                loginResult.Uuid,
                loginResult.Type),
            New MinecraftLaunchWatcherRequest(
                Setup.Get("VersionArgumentTitle", instance:=instance),
                Setup.Get("VersionArgumentTitleEmpty", instance:=instance),
                Setup.Get("LaunchArgumentTitle"),
                selectedJava.Installation.JavaFolder,
                File.Exists(selectedJava.Installation.JavaFolder & "\jstack.exe")),
            isTestLaunch)
    End Function

    Public Function ReplaceArgumentTokens(text As String,
                                          replaceTime As Boolean,
                                          selectedJava As JavaEntry,
                                          instance As McInstance,
                                          loginState As LoadState,
                                          loginInput As McLoginData,
                                          loginResult As McLoginResult,
                                          Optional escapeHandler As Func(Of String, String) = Nothing) As String
        If text Is Nothing Then Return Nothing

        Dim replacer =
        Function(s As String) As String
            If s Is Nothing Then Return ""
            If escapeHandler Is Nothing Then Return s
            If s.Contains(":\") Then s = ShortenPath(s)
            Return escapeHandler(s)
        End Function

        text = text.Replace("{pcl_version}", replacer(VersionBaseName))
        text = text.Replace("{pcl_version_code}", replacer(VersionCode))
        text = text.Replace("{pcl_version_branch}", replacer(VersionBranchName))
        text = text.Replace("{identify}", replacer(LauncherIdentity.LauncherId))
        text = text.Replace("{path}", replacer(Basics.CurrentDirectory))
        text = text.Replace("{path_with_name}", replacer(Basics.ExecutablePath))
        text = text.Replace("{path_temp}", replacer(PathTemp))
        If replaceTime Then
            text = text.Replace("{date}", replacer(Date.Now.ToString("yyyy'/'M'/'d")))
            text = text.Replace("{time}", replacer(Date.Now.ToString("HH':'mm':'ss")))
        End If

        text = text.Replace("{java}", replacer(selectedJava?.Installation.JavaFolder))
        text = text.Replace("{minecraft}", replacer(McFolderSelected))
        If instance?.IsLoaded Then
            text = text.Replace("{version_path}", replacer(instance.PathInstance)) : text = text.Replace("{verpath}", replacer(instance.PathInstance))
            text = text.Replace("{version_indie}", replacer(instance.PathIndie)) : text = text.Replace("{verindie}", replacer(instance.PathIndie))
            text = text.Replace("{name}", replacer(instance.Name))
            If {"unknown", "old", "pending"}.Contains(instance.Info.VanillaName.ToLower) Then
                text = text.Replace("{version}", replacer(instance.Name))
            Else
                text = text.Replace("{version}", replacer(instance.Info.VanillaName))
            End If
        Else
            text = text.Replace("{version_path}", replacer(Nothing)) : text = text.Replace("{verpath}", replacer(Nothing))
            text = text.Replace("{version_indie}", replacer(Nothing)) : text = text.Replace("{verindie}", replacer(Nothing))
            text = text.Replace("{name}", replacer(Nothing))
            text = text.Replace("{version}", replacer(Nothing))
        End If

        If loginState = LoadState.Finished Then
            text = text.Replace("{user}", replacer(loginResult.Name))
            text = text.Replace("{uuid}", replacer(loginResult.Uuid?.ToLower))
            Select Case loginInput.Type
                Case McLoginType.Legacy
                    text = text.Replace("{login}", replacer("离线"))
                Case McLoginType.Ms
                    text = text.Replace("{login}", replacer("正版"))
                Case McLoginType.Auth
                    text = text.Replace("{login}", replacer("Authlib-Injector"))
            End Select
        Else
            text = text.Replace("{user}", replacer(Nothing))
            text = text.Replace("{uuid}", replacer(Nothing))
            text = text.Replace("{login}", replacer(Nothing))
        End If

        Return text
    End Function

End Module
