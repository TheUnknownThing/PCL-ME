Imports PCL.Core.App
Imports PCL.Core.App.Essentials

Public Module ModStartupPromptShell

    Public Function RunStartupPrompt(prompt As LauncherStartupPrompt, Optional onExitLauncher As Action = Nothing) As Boolean
        If prompt Is Nothing OrElse prompt.Buttons Is Nothing OrElse prompt.Buttons.Count = 0 Then Return True

        Dim button1Action As Action = Nothing
        Dim button2Action As Action = Nothing
        Dim button3Action As Action = Nothing
        If prompt.Buttons.Count >= 1 AndAlso Not prompt.Buttons(0).ClosesPrompt Then button1Action = Sub() RunStartupPromptActions(prompt.Buttons(0).Actions, onExitLauncher)
        If prompt.Buttons.Count >= 2 AndAlso Not prompt.Buttons(1).ClosesPrompt Then button2Action = Sub() RunStartupPromptActions(prompt.Buttons(1).Actions, onExitLauncher)
        If prompt.Buttons.Count >= 3 AndAlso Not prompt.Buttons(2).ClosesPrompt Then button3Action = Sub() RunStartupPromptActions(prompt.Buttons(2).Actions, onExitLauncher)

        Dim result = MyMsgBox(
            prompt.Message,
            prompt.Title,
            prompt.Buttons(0).Label,
            If(prompt.Buttons.Count >= 2, prompt.Buttons(1).Label, ""),
            If(prompt.Buttons.Count >= 3, prompt.Buttons(2).Label, ""),
            IsWarn:=prompt.IsWarning,
            Button1Action:=button1Action,
            Button2Action:=button2Action,
            Button3Action:=button3Action)

        If result >= 1 AndAlso result <= prompt.Buttons.Count Then
            Dim selectedButton = prompt.Buttons(result - 1)
            If selectedButton.ClosesPrompt Then Return RunStartupPromptActions(selectedButton.Actions, onExitLauncher)
        End If
        Return True
    End Function

    Private Function RunStartupPromptActions(actions As IReadOnlyList(Of LauncherStartupPromptAction), onExitLauncher As Action) As Boolean
        For Each promptAction In actions
            Select Case promptAction.Kind
                Case LauncherStartupPromptActionKind.Accept
                    Setup.Set("SystemEula", True)
                Case LauncherStartupPromptActionKind.OpenUrl
                    OpenWebsite(promptAction.Value)
                Case LauncherStartupPromptActionKind.ExitLauncher
                    If onExitLauncher Is Nothing Then
                        Environment.Exit(ProcessReturnValues.Cancel)
                    Else
                        onExitLauncher()
                    End If
                    Return False
                Case LauncherStartupPromptActionKind.SetTelemetryEnabled
                    Config.System.TelemetryConfig.SetValue(Boolean.Parse(promptAction.Value), forceNewValue:=True)
                Case LauncherStartupPromptActionKind.Reject, LauncherStartupPromptActionKind.Continue
            End Select
        Next
        Return True
    End Function

End Module
