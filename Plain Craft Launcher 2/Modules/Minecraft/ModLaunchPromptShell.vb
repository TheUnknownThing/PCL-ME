Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchPromptShell

    Public Function RunMicrosoftDeviceCodeLoginPrompt(prepareJson As JObject) As MicrosoftDeviceCodePromptResult
        Dim converter As New MyMsgBoxConverter With {.Content = prepareJson, .ForceWait = True, .Type = MyMsgBoxType.Login}
        WaitingMyMsgBox.Add(converter)
        While converter.Result Is Nothing
            Thread.Sleep(100)
        End While

        If TypeOf converter.Result Is RestartException Then
            Return New MicrosoftDeviceCodePromptResult(MicrosoftDeviceCodePromptResultKind.PasswordLoginRequired)
        End If

        If TypeOf converter.Result Is Exception Then
            Return New MicrosoftDeviceCodePromptResult(MicrosoftDeviceCodePromptResultKind.Failed, [Error]:=CType(converter.Result, Exception))
        End If

        Dim result = CType(converter.Result, String())
        Return New MicrosoftDeviceCodePromptResult(
            MicrosoftDeviceCodePromptResultKind.Succeeded,
            result(0),
            result(1))
    End Function

    Public Sub RunLaunchPrompt(prompt As MinecraftLaunchPrompt, launchOptions As ModLaunch.McLaunchOptions)
        If prompt Is Nothing OrElse prompt.Buttons Is Nothing OrElse prompt.Buttons.Count = 0 Then Return

        Dim button1Action As Action = Nothing
        Dim button2Action As Action = Nothing
        Dim button3Action As Action = Nothing
        If prompt.Buttons.Count >= 1 AndAlso Not prompt.Buttons(0).ClosesPrompt Then button1Action = Sub() RunLaunchPromptButtonActions(prompt.Buttons(0).Actions, launchOptions)
        If prompt.Buttons.Count >= 2 AndAlso Not prompt.Buttons(1).ClosesPrompt Then button2Action = Sub() RunLaunchPromptButtonActions(prompt.Buttons(1).Actions, launchOptions)
        If prompt.Buttons.Count >= 3 AndAlso Not prompt.Buttons(2).ClosesPrompt Then button3Action = Sub() RunLaunchPromptButtonActions(prompt.Buttons(2).Actions, launchOptions)

        Dim result = MyMsgBox(
            prompt.Message,
            prompt.Title,
            prompt.Buttons(0).Label,
            If(prompt.Buttons.Count >= 2, prompt.Buttons(1).Label, ""),
            If(prompt.Buttons.Count >= 3, prompt.Buttons(2).Label, ""),
            prompt.IsWarning,
            Button1Action:=button1Action,
            Button2Action:=button2Action,
            Button3Action:=button3Action)

        If result >= 1 AndAlso result <= prompt.Buttons.Count Then
            Dim selectedButton = prompt.Buttons(result - 1)
            If selectedButton.ClosesPrompt Then RunLaunchPromptButtonActions(selectedButton.Actions, launchOptions)
        End If
    End Sub

    Private Sub RunLaunchPromptButtonActions(actions As IReadOnlyList(Of MinecraftLaunchPromptAction), launchOptions As ModLaunch.McLaunchOptions)
        For Each promptAction In actions
            Select Case promptAction.Kind
                Case MinecraftLaunchPromptActionKind.OpenUrl
                    OpenWebsite(promptAction.Value)
                Case MinecraftLaunchPromptActionKind.AppendLaunchArgument
                    If promptAction.Value = "--demo" Then Hint("游戏将以试玩模式启动！", HintType.Critical)
                    launchOptions.ExtraArgs.Add(promptAction.Value)
                Case MinecraftLaunchPromptActionKind.PersistNonAsciiPathWarningDisabled
                    Setup.Set("HintDisableGamePathCheckTip", True)
                Case MinecraftLaunchPromptActionKind.Abort
                    Throw New Exception("$$")
                Case MinecraftLaunchPromptActionKind.Continue
            End Select
        Next
    End Sub

    Public Function RunAccountDecisionPrompt(prompt As MinecraftLaunchAccountDecisionPrompt) As MinecraftLaunchAccountDecisionOption
        If prompt Is Nothing OrElse prompt.Options Is Nothing OrElse prompt.Options.Count = 0 Then Throw New ArgumentException("缺少可用的账号流程操作。", NameOf(prompt))

        Dim result = MyMsgBox(
            prompt.Message,
            prompt.Title,
            prompt.Options(0).Label,
            If(prompt.Options.Count >= 2, prompt.Options(1).Label, ""),
            If(prompt.Options.Count >= 3, prompt.Options(2).Label, ""),
            prompt.IsWarning)
        If result < 1 OrElse result > prompt.Options.Count Then result = prompt.Options.Count

        Dim selectedOption = prompt.Options(result - 1)
        If selectedOption.Url IsNot Nothing Then OpenWebsite(selectedOption.Url)
        If selectedOption.Followup IsNot Nothing Then
            MyMsgBox(selectedOption.Followup.Message, selectedOption.Followup.Title, IsWarn:=selectedOption.Followup.IsWarning)
        End If
        Return selectedOption
    End Function

    Public Function RunJavaPrompt(prompt As MinecraftLaunchJavaPrompt) As MinecraftLaunchJavaPromptOption
        If prompt Is Nothing OrElse prompt.Options Is Nothing OrElse prompt.Options.Count = 0 Then Throw New ArgumentException("缺少可用的 Java 操作。", NameOf(prompt))

        Dim result = MyMsgBox(
            prompt.Message,
            prompt.Title,
            prompt.Options(0).Label,
            If(prompt.Options.Count >= 2, prompt.Options(1).Label, ""),
            If(prompt.Options.Count >= 3, prompt.Options(2).Label, ""))
        If result < 1 OrElse result > prompt.Options.Count Then result = prompt.Options.Count
        Return prompt.Options(result - 1)
    End Function

    Public Function RunAuthProfileSelectionPrompt(title As String, options As IReadOnlyList(Of MinecraftLaunchAuthProfileOption)) As MinecraftLaunchAuthProfileOption
        If String.IsNullOrWhiteSpace(title) Then Throw New ArgumentException("缺少角色选择标题。", NameOf(title))
        If options Is Nothing OrElse options.Count = 0 Then Throw New ArgumentException("缺少可选角色。", NameOf(options))

        Dim selectionControls As New List(Of IMyRadio)
        For Each profile In options
            selectionControls.Add(New MyRadioBox With {.Text = profile.Name})
        Next

        Dim selectedIndex As Integer = MyMsgBoxSelect(selectionControls, title)
        Return options(selectedIndex)
    End Function

    Public Sub ShowThirdPartyLoginFailure(failure As MinecraftLaunchThirdPartyLoginFailure)
        If failure Is Nothing Then Throw New ArgumentNullException(NameOf(failure))
        MyMsgBox(failure.DialogMessage, failure.DialogTitle, IsWarn:=True)
    End Sub

    Public Enum MicrosoftDeviceCodePromptResultKind
        Succeeded = 0
        PasswordLoginRequired = 1
        Failed = 2
    End Enum

    Public Structure MicrosoftDeviceCodePromptResult
        Public Sub New(kind As MicrosoftDeviceCodePromptResultKind,
                       Optional accessToken As String = Nothing,
                       Optional refreshToken As String = Nothing,
                       Optional [error] As Exception = Nothing)
            Me.Kind = kind
            Me.AccessToken = accessToken
            Me.RefreshToken = refreshToken
            Me.Error = [error]
        End Sub

        Public Kind As MicrosoftDeviceCodePromptResultKind
        Public AccessToken As String
        Public RefreshToken As String
        Public [Error] As Exception
    End Structure

End Module
