Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchAuthlibStepShell

    Public Function ValidateCachedSession(input As McLoginServer,
                                          selectedProfile As McProfile) As McLoginResult
        ProfileLog("验证登录开始（Validate, Authlib")

        Dim accessToken As String = ""
        Dim clientToken As String = ""
        Dim uuid As String = ""
        Dim name As String = ""
        If selectedProfile IsNot Nothing Then
            accessToken = selectedProfile.AccessToken
            clientToken = selectedProfile.ClientToken
            uuid = selectedProfile.Uuid
            name = selectedProfile.Username
        End If

        Dim requestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildValidateRequest(
            input.BaseUrl,
            accessToken,
            clientToken)
        ModLaunchAuthlibRequestShell.ExecuteRequest(requestPlan)

        ProfileLog("验证登录成功（Validate, Authlib")
        Return New McLoginResult With {
            .AccessToken = accessToken,
            .ClientToken = clientToken,
            .Uuid = uuid,
            .Name = name,
            .Type = "Auth"}
    End Function

    Public Function RefreshCachedSession(input As McLoginServer,
                                         selectedProfile As McProfile,
                                         selectedProfileIndex As Integer?,
                                         applyProfileMutationPlan As Action(Of MinecraftLaunchProfileMutationPlan)) As McLoginResult
        If selectedProfile Is Nothing Then Throw New InvalidOperationException("当前没有可刷新的第三方验证档案。")

        ProfileLog("刷新登录开始（Refresh, Authlib")
        Dim requestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
            input.BaseUrl,
            selectedProfile.Username,
            selectedProfile.Uuid,
            selectedProfile.AccessToken)
        Dim refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
            New MinecraftLaunchAuthlibRefreshWorkflowRequest(
                ModLaunchAuthlibRequestShell.ExecuteRequest(requestPlan),
                selectedProfileIndex,
                selectedProfile.Server,
                selectedProfile.ServerName,
                input.UserName,
                input.Password))

        Dim loginResult = New McLoginResult With {
            .AccessToken = refreshResult.Session.AccessToken,
            .ClientToken = refreshResult.Session.ClientToken,
            .Uuid = refreshResult.Session.ProfileId,
            .Name = refreshResult.Session.ProfileName,
            .Type = "Auth"}
        applyProfileMutationPlan.Invoke(refreshResult.MutationPlan)
        ProfileLog("刷新登录成功（Refresh, Authlib）")
        Return loginResult
    End Function

    Public Function Authenticate(input As McLoginServer,
                                 selectedProfile As McProfile,
                                 selectedProfileIndex As Integer?,
                                 applyProfileMutationPlan As Action(Of MinecraftLaunchProfileMutationPlan),
                                 saveProfile As Action) As ModLaunchThirdPartyLoginShell.AuthlibLoginStepResult
        Try
            ProfileLog("登录开始（Login, Authlib）")
            Dim authenticateRequestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                input.BaseUrl,
                input.UserName,
                input.Password)
            Dim authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
                New MinecraftLaunchAuthlibAuthenticatePlanRequest(
                    input.ForceReselectProfile,
                    If(selectedProfile IsNot Nothing, selectedProfile.Uuid, Nothing),
                    ModLaunchAuthlibRequestShell.ExecuteRequest(authenticateRequestPlan)))
            Dim selectedId = ModLaunchInteractionShell.ResolveAuthlibAuthenticateSelection(authenticatePlan)

            Dim metadataRequestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(input.BaseUrl)
            Dim authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
                New MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                    authenticatePlan,
                    ModLaunchAuthlibRequestShell.ExecuteMetadataRequest(metadataRequestPlan.Url),
                    input.IsExist,
                    selectedProfileIndex,
                    input.BaseUrl,
                    input.UserName,
                    input.Password,
                    selectedId))
            Dim loginResult = New McLoginResult With {
                .AccessToken = authenticateResult.Session.AccessToken,
                .ClientToken = authenticateResult.Session.ClientToken,
                .Name = authenticateResult.Session.ProfileName,
                .Uuid = authenticateResult.Session.ProfileId,
                .Type = "Auth"}
            applyProfileMutationPlan.Invoke(authenticateResult.MutationPlan)
            saveProfile.Invoke()
            ProfileLog("登录成功（Login, Authlib）")
            Return New ModLaunchThirdPartyLoginShell.AuthlibLoginStepResult With {
                .LoginResult = loginResult,
                .NeedsRefresh = authenticateResult.NeedsRefresh}
        Catch ex As HttpWebException
            Throw
        Catch ex As Exception
            ProfileLog("第三方验证失败: " & ex.ToString())
            If ex.Message.StartsWithF("$") Then
                Throw
            Else
                Throw New Exception("登录失败：" & ex.Message, ex)
            End If
        End Try
    End Function

End Module
