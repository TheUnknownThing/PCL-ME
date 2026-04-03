Imports System.Net
Imports System.Net.Http
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchMicrosoftStepShell

    Public Function RefreshOAuthTokens(code As String, oAuthClientId As String) As ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult
        ModLaunch.McLaunchLog("开始正版验证 Step 1/6（刷新登录）")
        If String.IsNullOrEmpty(code) Then Throw New ArgumentException("传入的 Code 为空", NameOf(code))

        Dim result As String = Nothing
        Try
            Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOAuthRefreshRequest(code, oAuthClientId)
            result = ModLaunchMicrosoftRequestShell.ExecuteFormPost(requestPlan)
        Catch ex As ThreadInterruptedException
            Log(ex, "加载线程已终止")
        Catch ex As Exception
            Dim failure = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveOAuthRefreshFailure(ex.Message)
            If failure.Kind = MinecraftLaunchMicrosoftFailureResolutionKind.RequireRelogin Then
                Return New ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult With {
                    .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.RequireRelogin}
            End If

            ProfileLog("正版验证 Step 1/6 获取 OAuth Token 失败：" & ex.ToString())
            If ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure) Then
                Return New ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult With {
                    .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.IgnoreAndContinue}
            End If

            Throw
        End Try

        Dim refreshResult = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(result)
        Return New ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult With {
            .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded,
            .AccessToken = refreshResult.AccessToken,
            .RefreshToken = refreshResult.RefreshToken}
    End Function

    Public Function GetXboxLiveToken(accessToken As String) As ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 2/6: 获取 XBLToken")
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))

        Dim result As String = Nothing
        Try
            Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(accessToken)
            result = ModLaunchMicrosoftRequestShell.ExecuteJsonPost(requestPlan)
        Catch ex As Exception
            ProfileLog("正版验证 Step 2/6 获取 XBLToken 失败：" & ex.ToString())
            If ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(MinecraftLaunchMicrosoftFailureWorkflowService.GetRetryableStepFailure("Step 2")) Then
                Return New ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult With {
                    .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
            End If

            Throw
        End Try

        Return New ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Value = MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(result)}
    End Function

    Public Function GetXboxSecurityToken(xblTokenResult As ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult) As ModLaunchMicrosoftLoginShell.MicrosoftXstsStepResult
        ProfileLog("开始正版验证 Step 3/6: 获取 XSTSToken")
        If String.IsNullOrEmpty(xblTokenResult.Value) Then Throw New ArgumentException("XBLToken 为空，无法获取数据", NameOf(xblTokenResult))

        Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(xblTokenResult.Value)
        Dim response = ModLaunchMicrosoftRequestShell.ExecuteJsonPostWithStatus(requestPlan)
        Dim result = response.Body

        If Not response.IsSuccessStatusCode Then
            Dim failure = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveXstsFailure(result)
            If failure.Kind = MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort Then
                ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure)
            Else
                ProfileLog("正版验证 Step 3/6 获取 XSTSToken 失败：" & response.StatusCode)
                If ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure) Then
                    Return New ModLaunchMicrosoftLoginShell.MicrosoftXstsStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                End If
                Throw New HttpRequestException(
                    $"HTTP request failed with status code {CInt(response.StatusCode)} ({response.StatusCode}).",
                    Nothing,
                    response.StatusCode)
            End If
        End If

        Dim xstsResult = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(result)
        Return New ModLaunchMicrosoftLoginShell.MicrosoftXstsStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .XstsToken = xstsResult.Token,
            .UserHash = xstsResult.UserHash}
    End Function

    Public Function GetMinecraftAccessToken(tokens As ModLaunchMicrosoftLoginShell.MicrosoftXstsStepResult) As ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 4/6: 获取 Minecraft AccessToken")
        If String.IsNullOrEmpty(tokens.XstsToken) OrElse String.IsNullOrEmpty(tokens.UserHash) Then Throw New ArgumentException("传入的 XSTSToken 或者 UHS 错误", NameOf(tokens))

        Dim result As String
        Try
            Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(tokens.UserHash, tokens.XstsToken)
            result = ModLaunchMicrosoftRequestShell.ExecuteJsonPost(requestPlan)
        Catch ex As HttpRequestException
            Dim failure = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveMinecraftAccessTokenFailure(ex.StatusCode)
            If failure.Kind = MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException Then
                If ex.StatusCode.Equals(HttpStatusCode.TooManyRequests) Then
                    Log(ex, "正版验证 Step 4 汇报 429")
                ElseIf ex.StatusCode = HttpStatusCode.Forbidden Then
                    Log(ex, "正版验证 Step 4 汇报 403")
                End If
                ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure)
                Throw New InvalidOperationException("微软登录失败处理未终止流程。")
            Else
                ProfileLog("正版验证 Step 4/6 获取 MC AccessToken 失败：" & ex.ToString())
                If ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure) Then
                    Return New ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                End If

                Throw
            End If
        End Try

        Dim accessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(result)
        If String.IsNullOrWhiteSpace(accessToken) Then Throw New Exception("获取到的 Minecraft AccessToken 为空，登录流程异常！")
        Return New ModLaunchMicrosoftLoginShell.MicrosoftStringStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Value = accessToken}
    End Function

    Public Function GetMinecraftProfile(accessToken As String) As ModLaunchMicrosoftLoginShell.MicrosoftProfileStepResult
        ProfileLog("开始正版验证 Step 6/6: 获取玩家 ID 与 UUID 等相关信息")
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))

        Dim result As String
        Try
            Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(accessToken)
            result = ModLaunchMicrosoftRequestShell.ExecuteBearerGet(requestPlan)
        Catch ex As HttpRequestException
            Dim failure = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveMinecraftProfileFailure(ex.StatusCode)
            If failure.Kind = MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException Then
                Log(ex, "正版验证 Step 6 汇报 429")
                ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure)
                Throw New InvalidOperationException("微软登录失败处理未终止流程。")
            ElseIf failure.Kind = MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort Then
                Log(ex, "正版验证 Step 6 汇报 404")
                ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure, runPromptInBackground:=True)
                Throw New InvalidOperationException("微软登录失败处理未终止流程。")
            Else
                ProfileLog("正版验证 Step 6/6 获取玩家档案信息失败：" & ex.ToString())
                If ModLaunchPromptShell.TryHandleMicrosoftFailureResolution(failure) Then
                    Return New ModLaunchMicrosoftLoginShell.MicrosoftProfileStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                End If

                Throw
            End If
        End Try

        Dim profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(result)
        Return New ModLaunchMicrosoftLoginShell.MicrosoftProfileStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Uuid = profileResponse.Uuid,
            .UserName = profileResponse.UserName,
            .ProfileJson = profileResponse.ProfileJson}
    End Function

End Module
