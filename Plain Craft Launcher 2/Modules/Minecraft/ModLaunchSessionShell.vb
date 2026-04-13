Imports System.Windows
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchSessionShell

    Public Sub CompleteScriptExport(scriptExportPlan As MinecraftLaunchScriptExportPlan,
                                    logMessage As Action(Of String),
                                    setAbortHint As Action(Of String),
                                    abortLaunch As Action)
        If scriptExportPlan Is Nothing Then Throw New ArgumentNullException(NameOf(scriptExportPlan))

        logMessage?.Invoke(scriptExportPlan.CompletionLogMessage)
        setAbortHint?.Invoke(scriptExportPlan.AbortHint)
        OpenExplorer(scriptExportPlan.RevealInShellPath)
        abortLaunch?.Invoke()
    End Sub

    Public Sub ApplyMusicAction(action As MinecraftLaunchMusicShellAction)
        Select Case action.Kind
            Case MinecraftLaunchMusicActionKind.Pause
                RunInUi(Sub()
                            If MusicPause() AndAlso action.LogMessage <> "" Then Log(action.LogMessage)
                        End Sub)
            Case MinecraftLaunchMusicActionKind.Resume
                RunInUi(Sub()
                            If MusicResume() AndAlso action.LogMessage <> "" Then Log(action.LogMessage)
                        End Sub)
        End Select
    End Sub

    Public Sub ApplyVideoBackgroundAction(action As MinecraftLaunchVideoBackgroundShellAction)
        Select Case action.Kind
            Case MinecraftLaunchVideoBackgroundActionKind.Pause
                ModVideoBack.IsGaming = True
                VideoPause()
            Case MinecraftLaunchVideoBackgroundActionKind.Play
                ModVideoBack.IsGaming = False
                VideoPlay()
        End Select
    End Sub

    Public Sub ApplyLauncherAction(action As MinecraftLaunchShellAction)
        Select Case action.Kind
            Case MinecraftLaunchShellActionKind.ExitLauncher
                RunInUi(Sub() FrmMain.EndProgram(False))
            Case MinecraftLaunchShellActionKind.HideLauncher
                RunInUi(Sub() FrmMain.Hidden = True)
            Case MinecraftLaunchShellActionKind.MinimizeLauncher
                RunInUi(Sub() FrmMain.WindowState = WindowState.Minimized)
            Case MinecraftLaunchShellActionKind.ShowLauncher
                RunInUi(Sub() FrmMain.Hidden = False)
        End Select
    End Sub

    Public Sub AttachRealtimeLog(watcher As Watcher, attachedMessage As String, logMessage As Action(Of String))
        If watcher Is Nothing Then Throw New ArgumentNullException(NameOf(watcher))

        If FrmLogLeft Is Nothing Then RunInUiWait(Sub() FrmLogLeft = New PageLogLeft)
        If FrmLogRight Is Nothing Then RunInUiWait(Sub()
                                                       AniControlEnabled += 1
                                                       FrmLogRight = New PageLogRight
                                                       AniControlEnabled -= 1
                                                   End Sub)
        FrmLogLeft.Add(watcher)
        If attachedMessage IsNot Nothing Then logMessage?.Invoke(attachedMessage)
    End Sub

End Module
