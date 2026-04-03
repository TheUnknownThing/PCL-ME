Imports System.Collections.Generic
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchMicrosoftLoginShell

    Public Structure MicrosoftOAuthStepResult
        Public Outcome As MinecraftLaunchMicrosoftOAuthRefreshOutcome
        Public AccessToken As String
        Public RefreshToken As String
    End Structure

    Public Structure MicrosoftStringStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public Value As String
    End Structure

    Public Structure MicrosoftXstsStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public XstsToken As String
        Public UserHash As String
    End Structure

    Public Structure MicrosoftProfileStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public Uuid As String
        Public UserName As String
        Public ProfileJson As String
    End Structure

    Public Class MicrosoftLoginExecutionContext
        Public Property Data As LoaderTask(Of McLoginMs, McLoginResult)
        Public Property Input As McLoginMs
        Public Property OAuthClientId As String
        Public Property ShouldReuseCachedLogin As Boolean
        Public Property HasOAuthRefreshToken As Boolean
        Public Property IsCreatingProfile As Boolean
        Public Property SelectedProfileIndex As Integer?
        Public Property StoredProfiles As List(Of MinecraftLaunchStoredProfile)
        Public Property CreateCurrentMicrosoftLoginResult As Func(Of McLoginMs, McLoginResult)
        Public Property CreateMicrosoftLoginResult As Func(Of String, String, String, String, McLoginResult)
        Public Property CreateMicrosoftLoginResultFromStored As Func(Of MinecraftLaunchStoredProfile, McLoginResult)
        Public Property ApplyProfileMutationPlan As Action(Of MinecraftLaunchProfileMutationPlan)
        Public Property SaveProfile As Action
    End Class

    Public Sub RunLogin(context As MicrosoftLoginExecutionContext)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        If context.Data Is Nothing Then Throw New ArgumentNullException(NameOf(context.Data))
        If context.Input Is Nothing Then Throw New ArgumentNullException(NameOf(context.Input))
        If String.IsNullOrWhiteSpace(context.OAuthClientId) Then Throw New ArgumentException("缺少微软 OAuth Client ID。", NameOf(context.OAuthClientId))

        Dim currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetInitialStep(
            New MinecraftLaunchMicrosoftLoginExecutionRequest(
                context.ShouldReuseCachedLogin,
                context.HasOAuthRefreshToken))
        Dim oAuthAccessToken As String = Nothing
        Dim oAuthRefreshToken As String = Nothing
        Dim xblToken As New MicrosoftStringStepResult
        Dim xstsTokens As New MicrosoftXstsStepResult
        Dim accessToken As New MicrosoftStringStepResult
        Dim profileResult As New MicrosoftProfileStepResult

        Do
            context.Data.Progress = currentStep.Progress
            If context.Data.IsAborted Then Throw New ThreadInterruptedException

            Select Case currentStep.Kind
                Case MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession
                    context.Data.Output = context.CreateCurrentMicrosoftLoginResult(context.Input)
                    Exit Do
                Case MinecraftLaunchMicrosoftLoginStepKind.RequestDeviceCodeOAuthTokens
                    Dim oAuthTokens = ModLaunchInteractionShell.RequestMicrosoftDeviceCodeOAuthTokens(context.Data, context.OAuthClientId)
                    oAuthAccessToken = oAuthTokens.AccessToken
                    oAuthRefreshToken = oAuthTokens.RefreshToken
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterDeviceCodeOAuthSuccess()
                Case MinecraftLaunchMicrosoftLoginStepKind.RefreshOAuthTokens
                    Dim oAuthTokens = ModLaunchMicrosoftStepShell.RefreshOAuthTokens(context.Input.OAuthRefreshToken, context.OAuthClientId)
                    Dim refreshOutcome = oAuthTokens.Outcome
                    If refreshOutcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded Then
                        oAuthAccessToken = oAuthTokens.AccessToken
                        oAuthRefreshToken = oAuthTokens.RefreshToken
                    End If
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(refreshOutcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetXboxLiveToken
                    xblToken = ModLaunchMicrosoftStepShell.GetXboxLiveToken(oAuthAccessToken)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxLiveToken(xblToken.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetXboxSecurityToken
                    xstsTokens = ModLaunchMicrosoftStepShell.GetXboxSecurityToken(xblToken)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxSecurityToken(xstsTokens.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftAccessToken
                    accessToken = ModLaunchMicrosoftStepShell.GetMinecraftAccessToken(xstsTokens)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftAccessToken(accessToken.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.VerifyOwnership
                    ModLaunchInteractionShell.EnsureMicrosoftOwnership(accessToken.Value)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterOwnershipVerification()
                Case MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftProfile
                    profileResult = ModLaunchMicrosoftStepShell.GetMinecraftProfile(accessToken.Value)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftProfile(profileResult.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.ApplyProfileMutation
                    Dim microsoftMutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
                        New MinecraftLaunchMicrosoftProfileMutationRequest(
                            context.IsCreatingProfile,
                            context.SelectedProfileIndex,
                            context.StoredProfiles,
                            profileResult.Uuid,
                            profileResult.UserName,
                            accessToken.Value,
                            oAuthRefreshToken,
                            profileResult.ProfileJson))
                    context.ApplyProfileMutationPlan.Invoke(microsoftMutationPlan)
                    If microsoftMutationPlan.Kind <> MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate Then
                        context.SaveProfile.Invoke()
                        context.Data.Output = context.CreateMicrosoftLoginResult(accessToken.Value, profileResult.UserName, profileResult.Uuid, profileResult.ProfileJson)
                    Else
                        context.Data.Output = context.CreateMicrosoftLoginResultFromStored(microsoftMutationPlan.UpdateProfile)
                    End If
                    Exit Do
                Case Else
                    Throw New InvalidOperationException("未知的微软登录执行步骤。")
            End Select
        Loop
    End Sub

End Module
