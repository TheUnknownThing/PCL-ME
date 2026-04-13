Imports PCL.Core.Minecraft

Public Module ModCrashPromptShell

    Public Function RunOutputPrompt(prompt As MinecraftCrashOutputPrompt, onViewLog As Action) As MinecraftCrashOutputPromptButton
        If prompt Is Nothing OrElse prompt.Buttons Is Nothing OrElse prompt.Buttons.Count = 0 Then Return Nothing

        Dim button1Action As Action = Nothing
        Dim button2Action As Action = Nothing
        Dim button3Action As Action = Nothing
        If prompt.Buttons.Count >= 1 AndAlso Not prompt.Buttons(0).ClosesPrompt Then button1Action = Sub() RunOutputPromptAction(prompt.Buttons(0).Action, onViewLog)
        If prompt.Buttons.Count >= 2 AndAlso Not prompt.Buttons(1).ClosesPrompt Then button2Action = Sub() RunOutputPromptAction(prompt.Buttons(1).Action, onViewLog)
        If prompt.Buttons.Count >= 3 AndAlso Not prompt.Buttons(2).ClosesPrompt Then button3Action = Sub() RunOutputPromptAction(prompt.Buttons(2).Action, onViewLog)

        Dim result = MyMsgBox(
            prompt.Message,
            prompt.Title,
            prompt.Buttons(0).Label,
            If(prompt.Buttons.Count >= 2, prompt.Buttons(1).Label, ""),
            If(prompt.Buttons.Count >= 3, prompt.Buttons(2).Label, ""),
            Button1Action:=button1Action,
            Button2Action:=button2Action,
            Button3Action:=button3Action)

        If result >= 1 AndAlso result <= prompt.Buttons.Count Then Return prompt.Buttons(result - 1)
        Return Nothing
    End Function

    Private Sub RunOutputPromptAction(action As MinecraftCrashOutputPromptActionKind, onViewLog As Action)
        Select Case action
            Case MinecraftCrashOutputPromptActionKind.ViewLog
                onViewLog?.Invoke()
        End Select
    End Sub

End Module
