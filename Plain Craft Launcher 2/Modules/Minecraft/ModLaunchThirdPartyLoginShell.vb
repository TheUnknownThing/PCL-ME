Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchThirdPartyLoginShell

    Public Structure AuthlibLoginStepResult
        Public LoginResult As McLoginResult
        Public NeedsRefresh As Boolean
    End Structure

    Public Class ThirdPartyLoginExecutionContext
        Public Property Data As LoaderTask(Of McLoginServer, McLoginResult)
        Public Property IsCreatingProfile As Boolean
        Public Property SelectedProfile As McProfile
        Public Property SelectedProfileIndex As Integer?
        Public Property ApplyProfileMutationPlan As Action(Of MinecraftLaunchProfileMutationPlan)
        Public Property SaveProfile As Action
    End Class

    Public Sub RunLogin(context As ThirdPartyLoginExecutionContext)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        If context.Data Is Nothing Then Throw New ArgumentNullException(NameOf(context.Data))
        If context.ApplyProfileMutationPlan Is Nothing Then Throw New ArgumentNullException(NameOf(context.ApplyProfileMutationPlan))
        If context.SaveProfile Is Nothing Then Throw New ArgumentNullException(NameOf(context.SaveProfile))

        Dim currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetInitialStep(
            New MinecraftLaunchThirdPartyLoginExecutionRequest(
                context.Data.Input.ForceReselectProfile OrElse context.IsCreatingProfile))
        Do
            context.Data.Progress = currentStep.Progress
            Select Case currentStep.Kind
                Case MinecraftLaunchThirdPartyLoginStepKind.ValidateCachedSession
                    Try
                        If context.Data.IsAborted Then Throw New ThreadInterruptedException
                        context.Data.Output = ModLaunchAuthlibStepShell.ValidateCachedSession(context.Data.Input, context.SelectedProfile)
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterValidateSuccess()
                    Catch ex As HttpWebException
                        Dim allMessage = ex.ToString()
                        ProfileLog("验证登录失败：" & allMessage)
                        Dim resolution = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveValidationHttpFailure(
                            allMessage,
                            ex.InnerHttpException.WebResponse)
                        If resolution.Kind = MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped Then
                            ProfileLog("已触发超时登录失败")
                            ModLaunchPromptShell.ShowThirdPartyFailureIfPresent(resolution)
                            Throw New Exception(resolution.WrappedExceptionMessage)
                        End If
                        currentStep = resolution.NextStep
                    Catch ex As Exception
                        Dim allMessage = ex.ToString()
                        ProfileLog("验证登录失败：" & allMessage)
                        Dim resolution = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveValidationFailure(allMessage)
                        ModLaunchPromptShell.ShowThirdPartyFailureIfPresent(resolution)
                        Throw
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession
                    Try
                        If context.Data.IsAborted Then Throw New ThreadInterruptedException
                        context.Data.Output = ModLaunchAuthlibStepShell.RefreshCachedSession(
                            context.Data.Input,
                            context.SelectedProfile,
                            context.SelectedProfileIndex,
                            context.ApplyProfileMutationPlan)
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshSuccess(currentStep.HasRetriedRefresh)
                    Catch ex As Exception
                        ProfileLog("刷新登录失败：" & ex.ToString())
                        Dim resolution = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveRefreshFailure(ex.ToString(), currentStep.HasRetriedRefresh)
                        ModLaunchPromptShell.ShowThirdPartyFailureIfPresent(resolution)
                        currentStep = resolution.NextStep
                        If resolution.Kind = MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped Then
                            Throw New Exception(resolution.WrappedExceptionMessage, ex)
                        End If
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.Authenticate
                    Try
                        If context.Data.IsAborted Then Throw New ThreadInterruptedException
                        Dim loginStepResult = ModLaunchAuthlibStepShell.Authenticate(
                            context.Data.Input,
                            context.SelectedProfile,
                            context.SelectedProfileIndex,
                            context.ApplyProfileMutationPlan,
                            context.SaveProfile)
                        context.Data.Output = loginStepResult.LoginResult
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterLoginSuccess(loginStepResult.NeedsRefresh)
                        If currentStep.Kind = MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession AndAlso currentStep.HasRetriedRefresh Then
                            ProfileLog("重新进行刷新登录")
                        End If
                    Catch ex As HttpWebException
                        ProfileLog("验证失败：" & ex.ToString())
                        Dim resolution = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveLoginHttpFailure(
                            ex.ToString(),
                            ex.InnerHttpException.WebResponse)
                        ModLaunchPromptShell.ShowThirdPartyFailureIfPresent(resolution)
                        Throw New Exception(resolution.WrappedExceptionMessage)
                    Catch ex As Exception
                        ProfileLog("验证失败：" & ex.ToString())
                        Dim resolution = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveLoginFailure(ex.ToString())
                        ModLaunchPromptShell.ShowThirdPartyFailureIfPresent(resolution)
                        Throw New Exception(resolution.WrappedExceptionMessage)
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.Finish
                    Exit Do
                Case Else
                    Throw New InvalidOperationException("未知的第三方登录执行步骤。")
            End Select
        Loop
    End Sub

End Module
