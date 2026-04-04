Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils

Public Module ModLaunchLoginWorkflowShell

    Public McLoginLoader As New LoaderTask(Of McLoginData, McLoginResult)("登录", AddressOf McLoginStart, AddressOf McLoginInput, ThreadPriority.BelowNormal) With {.ReloadTimeout = 1, .ProgressWeight = 15, .Block = False}
    Public McLoginMsLoader As New LoaderTask(Of McLoginMs, McLoginResult)("Loader Login Ms", AddressOf McLoginMsStart) With {.ReloadTimeout = 1}
    Public McLoginLegacyLoader As New LoaderTask(Of McLoginLegacy, McLoginResult)("Loader Login Legacy", AddressOf McLoginLegacyStart)
    Public McLoginAuthLoader As New LoaderTask(Of McLoginServer, McLoginResult)("Loader Login Auth", AddressOf McLoginServerStart) With {.ReloadTimeout = 1000 * 60 * 10}

    Private McLoginMsRefreshTime As Long = 0

    Public Function McLoginInput() As McLoginData
        Dim loginData As McLoginData = Nothing
        Try
            loginData = GetLoginData()
        Catch ex As Exception
            Log(ex, "获取登录输入信息失败", LogLevel.Feedback)
        End Try
        Return loginData
    End Function

    Private Sub McLoginStart(data As LoaderTask(Of McLoginData, McLoginResult))
        Log("[Profile] 开始加载选定档案")
        Dim checkResult As String = IsProfileValid()
        If Not checkResult = "" Then Throw New ArgumentException(checkResult)

        Dim loader As LoaderBase = Nothing
        Select Case data.Input.Type
            Case McLoginType.Ms
                loader = McLoginMsLoader
            Case McLoginType.Legacy
                loader = McLoginLegacyLoader
            Case McLoginType.Auth
                loader = McLoginAuthLoader
        End Select

        loader.WaitForExit(data.Input, McLoginLoader, data.IsForceRestarting)
        data.Output = CType(loader, Object).Output
        RunInUi(Sub() FrmLaunchLeft.RefreshPage(False))
        Log("[Profile] 选定档案加载完成")
    End Sub

    Private Sub McLoginMsStart(data As LoaderTask(Of McLoginMs, McLoginResult))
        Dim input As McLoginMs = data.Input
        Dim logUsername As String = input.UserName
        ProfileLog("验证方式：正版（" & If(String.IsNullOrEmpty(logUsername), "尚未登录", logUsername) & "）")
        ModLaunchMicrosoftLoginShell.RunLogin(
            New ModLaunchMicrosoftLoginShell.MicrosoftLoginExecutionContext With {
                .Data = data,
                .Input = input,
                .OAuthClientId = OAuthClientId,
                .ShouldReuseCachedLogin = MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(
                    New MinecraftLaunchMicrosoftSessionReuseRequest(
                        data.IsForceRestarting,
                        input.AccessToken,
                        McLoginMsRefreshTime,
                        TimeUtils.GetTimeTick())),
                .HasOAuthRefreshToken = Not String.IsNullOrEmpty(input.OAuthRefreshToken),
                .IsCreatingProfile = IsCreatingProfile,
                .SelectedProfileIndex = ModLaunchProfileShell.GetSelectedProfileIndex(SelectedProfile, ProfileList),
                .StoredProfiles = ModLaunchProfileShell.GetStoredProfiles(ProfileList),
                .CreateCurrentMicrosoftLoginResult = AddressOf CreateCurrentMicrosoftLoginResultForSelectedProfile,
                .CreateMicrosoftLoginResult = AddressOf ModLaunchProfileShell.CreateMicrosoftLoginResult,
                .CreateMicrosoftLoginResultFromStored = AddressOf ModLaunchProfileShell.CreateMicrosoftLoginResultFromStored,
                .ApplyProfileMutationPlan = AddressOf ApplyCurrentProfileMutationPlan,
                .SaveProfile = AddressOf SaveProfile})
        McLoginMsRefreshTime = TimeUtils.GetTimeTick()
        ProfileLog("正版验证完成")
    End Sub

    Private Sub McLoginServerStart(data As LoaderTask(Of McLoginServer, McLoginResult))
        Dim input As McLoginServer = data.Input
        ProfileLog("验证方式：" & input.Description)
        ModLaunchThirdPartyLoginShell.RunLogin(
            New ModLaunchThirdPartyLoginShell.ThirdPartyLoginExecutionContext With {
                .Data = data,
                .IsCreatingProfile = IsCreatingProfile,
                .SelectedProfile = SelectedProfile,
                .SelectedProfileIndex = ModLaunchProfileShell.GetSelectedProfileIndex(SelectedProfile, ProfileList),
                .ApplyProfileMutationPlan = AddressOf ApplyCurrentProfileMutationPlan,
                .SaveProfile = AddressOf SaveProfile})
    End Sub

    Private Sub McLoginLegacyStart(data As LoaderTask(Of McLoginLegacy, McLoginResult))
        Dim input As McLoginLegacy = data.Input
        ProfileLog($"验证方式：离线（{input.UserName}, {input.Uuid}）")
        data.Progress = 0.1
        With data.Output
            .Name = input.UserName
            .Uuid = SelectedProfile.Uuid
            .Type = "Legacy"
        End With
        data.Output.AccessToken = data.Output.Uuid
        data.Output.ClientToken = data.Output.Uuid
    End Sub

    Private Function CreateCurrentMicrosoftLoginResultForSelectedProfile(input As McLoginMs) As McLoginResult
        Return ModLaunchProfileShell.CreateCurrentMicrosoftLoginResult(SelectedProfile, input)
    End Function

    Private Sub ApplyCurrentProfileMutationPlan(plan As MinecraftLaunchProfileMutationPlan)
        ModLaunchProfileShell.ApplyProfileMutationPlan(
            plan,
            ProfileList,
            SelectedProfile,
            IsCreatingProfile,
            Sub(message) Hint(message, HintType.Critical))
    End Sub

End Module
