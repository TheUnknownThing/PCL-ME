Imports System.Windows
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchSessionShell

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

End Module
