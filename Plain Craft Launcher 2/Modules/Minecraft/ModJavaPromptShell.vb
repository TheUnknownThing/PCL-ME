Imports PCL.Core.Minecraft.Launch

Public Module ModJavaPromptShell

    Public Function ConfirmJavaDownload(versionDescription As String, Optional forcedManualDownload As Boolean = False) As Boolean
        If String.IsNullOrWhiteSpace(versionDescription) Then Throw New ArgumentException("缺少 Java 版本说明。", NameOf(versionDescription))

        If forcedManualDownload Then
            Dim prompt As New MinecraftLaunchJavaPrompt(
                $"PCL 未找到 {versionDescription}。" & vbCrLf &
                $"请自行搜索并安装 {versionDescription}，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                "未找到 Java",
                {
                    New MinecraftLaunchJavaPromptOption("确定", MinecraftLaunchJavaPromptDecision.Abort)
                })
            RunJavaPrompt(prompt)
            Return False
        End If

        Dim decision = RunJavaPrompt(
            New MinecraftLaunchJavaPrompt(
                $"PCL 未找到 {versionDescription}，是否需要 PCL 自动下载？" & vbCrLf &
                $"如果你已经安装了 {versionDescription}，可以在 设置 → 启动选项 → 游戏 Java 中手动导入。",
                "自动下载 Java？",
                {
                    New MinecraftLaunchJavaPromptOption("自动下载", MinecraftLaunchJavaPromptDecision.Download),
                    New MinecraftLaunchJavaPromptOption("取消", MinecraftLaunchJavaPromptDecision.Abort)
                }))
        Return decision.Decision = MinecraftLaunchJavaPromptDecision.Download
    End Function

    Public Sub ShowJavaSelectionFailureHint(message As String)
        If String.IsNullOrWhiteSpace(message) Then Return
        Hint(message, HintType.Critical)
    End Sub

End Module
