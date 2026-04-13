Imports System.IO
Imports PCL.Core.App
Imports PCL.Core.App.Essentials
Imports PCL.Core.App.IoC
Imports PCL.Core.Utils
Imports PCL.Core.Utils.OS

Public Class Application

#If DEBUGRESERVED Then
    ''' <summary>
    ''' 用于开始程序时的一些测试。
    ''' </summary>
    Private Sub Test()
        Try
            ModDevelop.Start()
        Catch ex As Exception
            Log(ex, "开发者模式测试出错", LogLevel.Msgbox)
        End Try
    End Sub
#End If

    Public Sub New()
        '注册生命周期事件
        Lifecycle.When(LifecycleState.Loaded, AddressOf Application_Startup)
    End Sub

    '开始
    Private Sub Application_Startup() '(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            ModApplicationRuntimeShell.PrepareRuntime()
            '检查参数调用
            Dim startupPlan = LauncherStartupWorkflowService.BuildPlan(
                New LauncherStartupWorkflowRequest(
                    Basics.CommandLineArguments,
                    ExePath,
                    PathTemp,
                    PathAppdata,
                    VersionBaseName.Contains("beta"),
                    NtInterop.GetCurrentOsVersion(),
                    Not Is32BitSystem,
                    Setup.Get("UiLauncherLogo")))
            ModApplicationStartupShell.ExecuteImmediateCommand(startupPlan.ImmediateCommand)
            '初始化文件结构
            Dim bootstrapResult = startupPlan.Bootstrap
            ModApplicationStartupShell.ApplyBootstrap(bootstrapResult)
#If False Then
            '检测单例
            Dim ShouldWaitForExit As Boolean = args.Length > 0 AndAlso args(0) = "--wait" '要求等待已有的 PCL 退出
            Dim WaitRetryCount As Integer = 0
WaitRetry:
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "Plain Craft Launcher Community Edition ")
            If WindowHwnd = IntPtr.Zero Then FindWindow(Nothing, "Plain Craft Launcher 2 Community Edition ")
            If WindowHwnd <> IntPtr.Zero Then
                If ShouldWaitForExit AndAlso WaitRetryCount < 20 Then '至多等待 10 秒
                    WaitRetryCount += 1
                    Thread.Sleep(500)
                    GoTo WaitRetry
                End If
                '将已有的 PCL 窗口拖出来
                ShowWindowToTop(WindowHwnd)
                '播放提示音并退出
                Beep()
                Environment.[Exit](ProcessReturnValues.Cancel)
            End If
#End If
            ModApplicationStartupShell.ApplyVisualPlan(startupPlan.Visual)
            ModApplicationStartupShell.ApplyEnvironmentWarning(startupPlan.EnvironmentWarningPrompt)
            '计时
            Log("[Start] 第一阶段加载用时：" & TimeUtils.GetTimeTick() - ApplicationStartTick & " ms")
            ApplicationStartTick = TimeUtils.GetTimeTick()
            '执行测试
#If DEBUGRESERVED Then
            Test()
#End If
            AniControlEnabled += 1
        Catch ex As Exception
            ModApplicationRuntimeShell.HandleInitializationFailure(ex)
        End Try
    End Sub

    '结束
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain.EndProgram(False)
    End Sub

#If False

    '异常
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        e.Handled = True
        If IsProgramEnded Then Return
        FeedbackInfo()
        Dim Detail As String = e.Exception.ToString()
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") Then ' “自动错误判断” 的结果分析
            OpenWebsite("https://get.dot.net/8")
            Log(e.Exception, "你的 .NET 桌面运行时版本过低或损坏，请下载并重新安装 .NET 8！", LogLevel.Critical, "运行环境错误")
        Else
            Log(e.Exception, "程序出现未知错误", LogLevel.Critical, "锟斤拷烫烫烫")
        End If
    End Sub

    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean

#End If

    '切换窗口

    '控件模板事件
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
    End Sub

    Public Shared ReadOnly ShowingTooltips As New List(Of Border)
    Private Sub TooltipLoaded(sender As Object, e As EventArgs)
        ShowingTooltips.Add(CType(sender, Border))
    End Sub
    Private Sub TooltipUnloaded(sender As Object, e As RoutedEventArgs)
        ShowingTooltips.Remove(CType(sender, Border))
    End Sub

End Class
