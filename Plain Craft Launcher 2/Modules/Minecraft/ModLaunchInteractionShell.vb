Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchInteractionShell

    Public Sub RunPrecheckPrompts(precheckResult As MinecraftLaunchPrecheckResult, launchOptions As ModLaunch.McLaunchOptions)
        If precheckResult Is Nothing Then Throw New ArgumentNullException(NameOf(precheckResult))

        If precheckResult.Prompts.Count > 0 Then
            ModLaunchPromptShell.RunLaunchPrompt(precheckResult.Prompts(0), launchOptions)
        End If
#If BETA Then
        If launchOptions?.SaveBatch Is Nothing Then '保存脚本时不提示
            RunInNewThread(
                Sub()
                    Dim supportPrompt = MinecraftLaunchShellService.GetSupportPrompt(Setup.Get("SystemLaunchCount"))
                    If supportPrompt IsNot Nothing Then ModLaunchPromptShell.RunLaunchPrompt(supportPrompt, launchOptions)
                End Sub, "Donate")
        End If
#End If
        For index = 1 To precheckResult.Prompts.Count - 1
            ModLaunchPromptShell.RunLaunchPrompt(precheckResult.Prompts(index), launchOptions)
        Next
    End Sub

    Public Function RequestMicrosoftDeviceCodeOAuthTokens(data As LoaderTask(Of McLoginMs, McLoginResult),
                                                          oAuthClientId As String) As ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult
        If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))
        If String.IsNullOrWhiteSpace(oAuthClientId) Then Throw New ArgumentException("缺少微软 OAuth Client ID。", NameOf(oAuthClientId))

        Do
            ModLaunch.McLaunchLog("开始正版验证 Step 1/6（原始登录）")
            Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest(oAuthClientId)
            Dim responseBody = ModLaunchMicrosoftRequestShell.ExecuteFormPost(requestPlan)

            Dim promptPlan = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(responseBody)
            ModLaunch.McLaunchLog(promptPlan.LogMessage)

            Dim promptResult = ModLaunchPromptShell.RunMicrosoftDeviceCodeLoginShell(promptPlan)
            If promptResult.Kind = MicrosoftDeviceCodeShellResultKind.RetryDeviceCodeLogin Then Continue Do

            Return New ModLaunchMicrosoftLoginShell.MicrosoftOAuthStepResult With {
                .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded,
                .AccessToken = promptResult.AccessToken,
                .RefreshToken = promptResult.RefreshToken}
        Loop
    End Function

    Public Sub EnsureMicrosoftOwnership(accessToken As String)
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))

        Dim requestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(accessToken)
        Dim result = ModLaunchMicrosoftRequestShell.ExecuteBearerGet(requestPlan)
        Dim ownershipPrompt = MinecraftLaunchMicrosoftFailureWorkflowService.TryGetOwnershipFailurePrompt(result)
        If ownershipPrompt IsNot Nothing Then
            ModLaunchPromptShell.ShowMicrosoftOwnershipPrompt(ownershipPrompt)
            Throw New Exception("$$")
        End If
    End Sub

    Public Function ResolveAuthlibAuthenticateSelection(authenticatePlan As MinecraftLaunchAuthlibAuthenticatePlan) As String
        If authenticatePlan Is Nothing Then Throw New ArgumentNullException(NameOf(authenticatePlan))

        If Not String.IsNullOrWhiteSpace(authenticatePlan.NoticeMessage) Then
            Hint(authenticatePlan.NoticeMessage, HintType.Critical)
        End If
        If authenticatePlan.Kind = MinecraftLaunchAuthProfileSelectionKind.Fail Then
            Throw New Exception(authenticatePlan.FailureMessage)
        End If
        If authenticatePlan.Kind = MinecraftLaunchAuthProfileSelectionKind.PromptForSelection Then
            ProfileLog("要求玩家选择角色")
            Dim selectedId As String = Nothing
            RunInUiWait(
                Sub()
                    Dim selectedProfile = ModLaunchPromptShell.RunAuthProfileSelectionPrompt(authenticatePlan.PromptTitle, authenticatePlan.PromptOptions)
                    selectedId = selectedProfile.Id
                End Sub)
            Dim promptSelection = authenticatePlan.PromptOptions.First(Function(profile) profile.Id = selectedId)
            ProfileLog("玩家选择的角色：" & promptSelection.Name)
            Return selectedId
        End If

        If authenticatePlan.NeedsRefresh Then
            ProfileLog("根据缓存选择的角色：" & authenticatePlan.SelectedProfileName)
        End If

        Return authenticatePlan.SelectedProfileId
    End Function

End Module
