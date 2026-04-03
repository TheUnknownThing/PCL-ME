Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Media.Effects
Imports PCL.Core.App
Imports PCL.Core.App.Essentials
Imports PCL.Core.App.IoC
Imports PCL.Core.Logging
Imports PCL.Core.UI
Imports PCL.Core.UI.Theme
Imports PCL.Core.Utils
Imports PCL.Core.Utils.OS

Public Class FormMain

    Private ReadOnly _startupWorkflowPlan As LauncherMainWindowStartupWorkflowPlan

#Region "基础"

    '更新日志
    Private Sub ShowUpdateLog()
        ModUpdateLogShell.ShowUpdateLogFromFile($"{PathTemp}CEUpdateLog.md", VersionBranchName, VersionBaseName)
    End Sub

    '窗口加载
    Private IsWindowLoadFinished As Boolean = False
    Public Sub New()
        ApplicationStartTick = TimeUtils.GetTimeTick()
        '刷新主题
        'ThemeCheckAll(False)
        'ThemeRefreshColor()
        AddHandler ThemeService.ColorModeChanged, Sub(mode, theme) ThemeRefresh()
        AddHandler ThemeService.ColorThemeChanged, AddressOf ThemeRefresh
        '窗体参数初始化
        FrmMain = Me
        FrmLaunchLeft = New PageLaunchLeft
        FrmLaunchRight = New PageLaunchRight
        '版本号改变
        Dim LastVersion As Integer = States.System.LastVersion
        If LastVersion < VersionCode Then
            '触发升级
            UpgradeSub(LastVersion)
        ElseIf LastVersion > VersionCode Then
            '触发降级
            DowngradeSub(LastVersion)
        End If
        _startupWorkflowPlan = LauncherMainWindowStartupWorkflowService.BuildPlan(New LauncherMainWindowStartupWorkflowRequest(
            Not Setup.IsUnset("LaunchArgumentIndieV2"),
            Not Setup.IsUnset("LaunchArgumentIndie"),
            If(Setup.IsUnset("LaunchArgumentIndie"), 0, CInt(Setup.Get("LaunchArgumentIndie"))),
            Not Setup.IsUnset("WindowHeight"),
            CInt(Setup.GetDefault("LaunchArgumentIndie")),
            CInt(Setup.GetDefault("LaunchArgumentIndieV2")),
            GetStartupSpecialBuildKind(),
            Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT") IsNot Nothing,
            Setup.Get("SystemEula"),
            Config.System.TelemetryConfig.IsDefault(),
            Setup.Get("SystemCount")))
        '版本隔离设置迁移
        Dim versionIsolationMigration = _startupWorkflowPlan.VersionIsolationMigration
        If versionIsolationMigration.ShouldStoreVersionIsolationV2 Then
            Setup.Set("LaunchArgumentIndieV2", versionIsolationMigration.VersionIsolationV2Value)
            Log(versionIsolationMigration.LogMessage)
        End If
        Setup.Load("UiLauncherTheme")
        '注册拖拽事件（不能直接加 Handles，否则没用；#6340）
        [AddHandler](DragDrop.DragEnterEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        [AddHandler](DragDrop.DragOverEvent, New DragEventHandler(AddressOf HandleDrag), handledEventsToo:=True)
        '注册 MsgBox 事件
        AddHandler MsgBoxWrapper.OnShow, AddressOf MsgBoxWrapper_OnShow
        '注册 Hint 事件
        AddHandler HintWrapper.OnShow, AddressOf HintWrapper_OnShow
        '加载 UI
        InitializeComponent()
        Opacity = 0
        Try
            Height = Setup.Get("WindowHeight")
            Width = Setup.Get("WindowWidth")
        Catch ex As Exception '修复 #2019
            Log(ex, "读取窗口默认大小失败", LogLevel.Hint)
            Height = MinHeight + 100
            Width = MinWidth + 100
        End Try
        '管理员权限下文件拖拽
        If ProcessInterop.IsAdmin() Then 
            Log("[Start] PCL 当前正以管理员权限运行")
            Static helper As New DragHelper()
            AddHandler Me.SourceInitialized,
                Sub()
                    Dim windowInterop As New WindowInteropHelper(Me)
                    helper.HwndSource = HwndSource.FromHwnd(windowInterop.Handle)
                    helper.AddHook()
                End Sub

            AddHandler Me.Closing,Sub() helper.RemoveHook()
            AddHandler Helper.DragDrop, Sub() ModMainWindowDragShell.ProcessFileDrag(helper.DropFilePaths)
        End If
        If Not IsNothing(FrmLaunchLeft.Parent) Then FrmLaunchLeft.SetValue(ContentPresenter.ContentProperty, Nothing)
        If Not IsNothing(FrmLaunchRight.Parent) Then FrmLaunchRight.SetValue(ContentPresenter.ContentProperty, Nothing)
        PanMainLeft.Child = FrmLaunchLeft
        PageLeft = FrmLaunchLeft
        PanMainRight.Child = FrmLaunchRight
        PageRight = FrmLaunchRight
        FrmLaunchRight.PageState = MyPageRight.PageStates.ContentStay
        '调试模式提醒
        If ModeDebug Then Hint("[调试模式] PCL 正以调试模式运行，这可能会导致性能下降，若无必要请不要开启！")
        '尽早执行的加载池
        McFolderListLoader.Start(0) '为了让下载已存在文件检测可以正常运行，必须跑一次；为了让启动按钮尽快可用，需要尽早执行；为了与 PageLaunchLeft 联动，需要为 0 而不是 GetUuid

        Log("[Start] 第二阶段加载用时：" & TimeUtils.GetTimeTick() - ApplicationStartTick & " ms")
        '注册生命周期状态事件
        Lifecycle.When(LifecycleState.WindowCreated, AddressOf FormMain_Loaded)
    End Sub

    Private Sub FormMain_Loaded() '(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        FormMain_SizeChanged()
        ApplicationStartTick = TimeUtils.GetTimeTick()
        FrmHandle = New WindowInteropHelper(Me).Handle
        BtnExtraUpdateRestart.ShowCheck = AddressOf BtnExtraUpdateRestart_ShowCheck
        BtnExtraDownload.ShowCheck = AddressOf BtnExtraDownload_ShowCheck
        BtnExtraBack.ShowCheck = AddressOf BtnExtraBack_ShowCheck
        BtnExtraApril.ShowCheck = AddressOf BtnExtraApril_ShowCheck
        BtnExtraShutdown.ShowCheck = AddressOf BtnExtraShutdown_ShowCheck
        BtnExtraLog.ShowCheck = AddressOf BtnExtraLog_ShowCheck
        BtnExtraApril.ShowRefresh()
        ModMainWindowLoadedShell.PrepareLoadedWindow(Me, AddressOf AddResizer, AddressOf RemoveResizer)
        Application.Current.Resources("BlurSamplingRate") = Setup.Get("UiBlurSamplingRate") * 0.01
        Application.Current.Resources("BlurType") = CType(Setup.Get("UiBlurType"), KernelType)
        If Setup.Get("UiBlur") Then
            Application.Current.Resources("BlurRadius") = Setup.Get("UiBlurValue") * 1.0
        Else
            Application.Current.Resources("BlurRadius") = 0.0
        End If
        ModMainWindowPresentationShell.PresentLoadedWindow(
            Me,
            AddressOf AttachWindowHook,
            AddressOf CompleteWindowPresentation)
        ModMainWindowStartupThreadShell.StartConsentPromptThread(
            _startupWorkflowPlan.Consent,
            Sub() EndProgram(False))
        '加载池
        ModMainWindowStartupThreadShell.StartLoaderInitializationThread(
            AddressOf RunCountSub,
            AddressOf TryClearTaskTemp)

        Log("[Start] 第三阶段加载用时：" & TimeUtils.GetTimeTick() - ApplicationStartTick & " ms")
    End Sub

    Private Sub AttachWindowHook()
        Dim HwndSource As Interop.HwndSource = PresentationSource.FromVisual(Me)
        HwndSource.AddHook(New Interop.HwndSourceHook(AddressOf WndProc))
    End Sub

    Private Sub CompleteWindowPresentation()
        RenderTransform = Nothing
        IsWindowLoadFinished = True
        Log($"[System] DPI：{DPI}，系统版本：{Environment.OSVersion.VersionString}，PCL 位置：{ExePathWithName}")
    End Sub

    Private Shared Function GetStartupSpecialBuildKind() As LauncherStartupSpecialBuildKind
#If DEBUG Then
        Return LauncherStartupSpecialBuildKind.Debug
#ElseIf DEBUGCI Then
        Return LauncherStartupSpecialBuildKind.Ci
#Else
        Return LauncherStartupSpecialBuildKind.None
#End If
    End Function

    '根据打开次数触发的事件
    Private Sub RunCountSub()
        ModMainWindowStartupShell.ApplyMilestone(_startupWorkflowPlan.Milestone)
    End Sub
    '升级与降级事件
    Private Sub UpgradeSub(LastVersionCode As Integer)
        Log("[Start] 版本号从 " & LastVersionCode & " 升高到 " & VersionCode)
        ApplyVersionTransition(LastVersionCode)
    End Sub
    Private Sub DowngradeSub(LastVersionCode As Integer)
        Log("[Start] 版本号从 " & LastVersionCode & " 降低到 " & VersionCode)
        ApplyVersionTransition(LastVersionCode)
    End Sub

    Private Sub ApplyVersionTransition(lastVersionCode As Integer)
        Dim workflowPlan = LauncherVersionTransitionWorkflowService.BuildPlan(New LauncherVersionTransitionWorkflowRequest(
            New LauncherVersionTransitionRequest(
                lastVersionCode,
                VersionCode,
                IsBetaBuild(),
                GetHighestRecordedVersionCode(),
                CInt(Setup.Get("LaunchArgumentWindowType")),
                If(Setup.IsUnset("UiLauncherThemeHide"), Nothing, Setup.Get("UiLauncherThemeHide").ToString()),
                If(Setup.IsUnset("UiLauncherThemeHide2"), Nothing, Setup.Get("UiLauncherThemeHide2").ToString()),
                File.Exists(ExePath & "PCL\CustomSkin.png"),
                File.Exists(PathTemp & "CustomSkin.png"),
                File.Exists(PathAppdata & "CustomSkin.png"),
                Not Setup.IsUnset("ToolDownloadTranslate"),
                Not Setup.IsUnset("ToolDownloadTranslateV2"),
                If(Setup.IsUnset("ToolDownloadTranslate"), 0, CInt(Setup.Get("ToolDownloadTranslate")))),
            ExePath & "PCL\CustomSkin.png",
            PathTemp & "CustomSkin.png",
            PathAppdata & "CustomSkin.png"))
        ModMainWindowStartupShell.ApplyVersionTransition(
            workflowPlan,
            AddressOf MigrateOldProfile,
            AddressOf ShowCEAnnounce,
            AddressOf ShowUpdateLog)
    End Sub

    Private Shared Function GetHighestRecordedVersionCode() As Integer
#If BETA Then
        Return Setup.Get("SystemHighestBetaVersionReg")
#Else
        Return Setup.Get("SystemHighestAlphaVersionReg")
#End If
    End Function

    Private Shared Function IsBetaBuild() As Boolean
#If BETA Then
        Return True
#Else
        Return False
#End If
    End Function

#End Region

#Region "自定义窗口"
    
    Private CanResize As Boolean = True
    
    ' 重写窗口边缘判定以使 DWM 自带的 resizer 行为看起来比较正常
    Private Function _SizeWndProc(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        ' 窗口活动常量
        Const WM_NCHITTEST = &H84
        Const HTCLIENT = 1
        Const HTLEFT = 10
        Const HTRIGHT = 11
        Const HTTOP = 12
        Const HTTOPLEFT = 13
        Const HTTOPRIGHT = 14
        Const HTBOTTOM = 15
        Const HTBOTTOMLEFT = 16
        Const HTBOTTOMRIGHT = 17
        
        ' WPF 尺寸的 offset
        Const offsetWpf = 6
        Const hitWidthWpf = 5
        
        ' 过滤非 WM_NCHITTEST 事件
        If msg <> WM_NCHITTEST Then Return IntPtr.Zero
        
        ' 提取鼠标坐标
        ' 没妈的 VB 强转还得检查一下幻想的妈是不是还活着
        Dim mouseBytes As Byte() = BitConverter.GetBytes(lParam.ToInt64())
        Dim xMouse As Short = BitConverter.ToInt16(mouseBytes, 0)
        Dim yMouse As Short = BitConverter.ToInt16(mouseBytes, 2)
        
        ' 获取窗口参数
        Dim windowRect = WindowInterop.GetWindowRectangle(hWnd)
        Dim windowBounds = windowRect.ToWindowBounds()

        ' 判断鼠标是否在窗口范围内
        Dim isInWindow As Boolean = _
                (xMouse >= windowRect.Left AndAlso xMouse <= windowRect.Right) AndAlso
                (yMouse >= windowRect.Top AndAlso yMouse <= windowRect.Bottom)

        ' 过滤不在窗口内的请求
        If Not isInWindow Then Return IntPtr.Zero

        ' 如果 CanResize 为 False，直接返回 HTCLIENT
        If Not CanResize Then Return New IntPtr(HTCLIENT)

        ' 真实像素尺寸的 offset
        Dim dpi = VisualTreeHelper.GetDpi(Me)
        Dim offsetPxX = offsetWpf * dpi.DpiScaleX
        Dim offsetPxY = offsetWpf * dpi.DpiScaleY
        Dim hitWidthPxX = hitWidthWpf * dpi.DpiScaleX
        Dim hitWidthPxY = hitWidthWpf * dpi.DpiScaleY

        ' 计算鼠标相对于窗口左上角的物理像素位置
        Dim relX As Integer = xMouse - windowRect.Left
        Dim relY As Integer = yMouse - windowRect.Top
        Dim w As Integer = windowBounds.Width
        Dim h As Integer = windowBounds.Height

        ' 判定是否命中偏移后的热区
        Dim inLeft As Boolean = (relX >= offsetPxX AndAlso relX <= offsetPxX + hitWidthPxX)
        Dim inRight As Boolean = (relX <= w - offsetPxX AndAlso relX >= w - offsetPxX - hitWidthPxX)
        Dim inTop As Boolean = (relY >= offsetPxY AndAlso relY <= offsetPxY + hitWidthPxY)
        Dim inBottom As Boolean = (relY <= h - offsetPxY AndAlso relY >= h - offsetPxY - hitWidthPxY)

        handled = True ' 接管该区域的消息

        ' 返回结果
        If inTop AndAlso inLeft Then Return New IntPtr(HTTOPLEFT)
        If inTop AndAlso inRight Then Return New IntPtr(HTTOPRIGHT)
        If inBottom AndAlso inLeft Then Return New IntPtr(HTBOTTOMLEFT)
        If inBottom AndAlso inRight Then Return New IntPtr(HTBOTTOMRIGHT)
        If inLeft Then Return New IntPtr(HTLEFT)
        If inRight Then Return New IntPtr(HTRIGHT)
        If inTop Then Return New IntPtr(HTTOP)
        If inBottom Then Return New IntPtr(HTBOTTOM)

        ' 如果在 0-offset 范围内，返回 HTCLIENT 杀掉默认缩放
        Return New IntPtr(HTCLIENT)
    End Function

    Protected Overrides Sub OnSourceInitialized(e As EventArgs)
        MyBase.OnSourceInitialized(e)
        ModMainWindowChromeShell.InitializeCustomWindow(Me, AddressOf _SizeWndProc)
    End Sub

    '关闭
    Private Sub FormMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        EndProgram(True)
        e.Cancel = True
    End Sub
    ''' <summary>
    ''' 正常关闭程序。程序将在执行此方法后约 0.3s 退出。
    ''' </summary>
    ''' <param name="SendWarning">是否在还有下载任务未完成时发出警告。</param>
    ''' <param name="isUpdating">是否正在更新重启</param>
    Public Sub EndProgram(SendWarning As Boolean, Optional isUpdating As Boolean = False)
        If Not ModMainWindowShutdownShell.ConfirmShutdown(SendWarning) Then Return
        '关闭联机大厅
        'Await LobbyController.CloseAsync().ConfigureAwait(False)
        '存储上次使用的档案编号
        SaveProfile()
        ModMainWindowShutdownShell.RunShutdown(
            Me,
            Sub() EndProgramForce(force:=False, isUpdating:=isUpdating))
    End Sub
    Private Shared IsLogShown As Boolean = False
    Public Shared Sub EndProgramForce(
                                            Optional ReturnCode As ProcessReturnValues = ProcessReturnValues.Success, 
                                            Optional force As Boolean = True,
                                            Optional isUpdating As Boolean = False)
        'On Error Resume Next
        '关闭联机大厅
        'Await LobbyController.CloseAsync().ConfigureAwait(False)
        IsProgramEnded = True
        AniControlEnabled += 1
        If IsUpdateWaitingRestart AndAlso Not isUpdating Then UpdateRestart(False, triggerRestart := False)
        If ReturnCode = ProcessReturnValues.Exception Then
            If Not IsLogShown Then
                FeedbackInfo()
                Log("请在 https://github.com/PCL-Community/PCL2-CE/issues 提交错误报告，以便于社区解决此问题！（这也有可能是原版 PCL 的问题）")
                IsLogShown = True
                ShellOnly(LogWrapper.CurrentLogger.CurrentLogFiles.Last())
            End If
            Thread.Sleep(500) '防止 PCL 在记事本打开前就被掐掉
        End If
        Log("[System] 程序已退出，返回值：" & GetStringFromEnum(ReturnCode))
        'If ReturnCode <> ProcessReturnValues.Success Then Environment.Exit(ReturnCode)
        'Process.GetCurrentProcess.Kill()
        Lifecycle.Shutdown(ReturnCode, force)
    End Sub
    Private Sub BtnTitleClose_Click(sender As Object, e As RoutedEventArgs) Handles BtnTitleClose.Click
        EndProgram(True)
    End Sub

    '移动
    Private Sub FormDragMove(sender As Object, e As MouseButtonEventArgs) Handles PanTitle.MouseLeftButtonDown, PanMsg.MouseLeftButtonDown
        'On Error Resume Next
        If sender.IsMouseDirectlyOver Then DragMove()
    End Sub

    '改变大小
    ''' <summary>
    ''' 是否可以向注册表储存尺寸改变信息。以此避免初始化时误储存。
    ''' </summary>
    Public IsSizeSaveable As Boolean = False
    Private Sub FormMain_SizeChanged() Handles Me.SizeChanged
        If IsSizeSaveable Then
            States.UI.WindowHeight = Height
            States.UI.WindowWidth = Width
        End If
        If PanBack IsNot Nothing Then
            RectForm.Rect = New Rect(0, 0, PanBack.ActualWidth, PanBack.ActualHeight)

            Dim formWidth As Double = PanBack.ActualWidth + 0.001
            Dim formHeight As Double = PanBack.ActualHeight + 0.001

            PanForm.Width = formWidth
            PanForm.Height = formHeight
            PanMain.Width = formWidth

            If PanTitle IsNot Nothing Then
                PanMain.Height = Math.Max(0, formHeight - PanTitle.ActualHeight)
            Else
                PanMain.Height = formHeight
            End If

            VideoBack.Width = formWidth
            VideoBack.Height = formHeight
        End If
        If WindowState = WindowState.Maximized Then WindowState = WindowState.Normal '修复 #1938
    End Sub

    '标题栏改变大小
    Private Sub PanTitle_SizeChanged() Handles PanTitleLeft.SizeChanged
        If PanTitleMain.ColumnDefinitions(0).ActualWidth - 30 <= 0 Then
            PanTitleLeft.ColumnDefinitions(0).MaxWidth = 0
        Else
            PanTitleLeft.ColumnDefinitions(0).MaxWidth = PanTitleMain.ColumnDefinitions(0).ActualWidth - 30
        End If
    End Sub

    '最小化
    Private Sub BtnTitleMin_Click() Handles BtnTitleMin.Click
        WindowState = WindowState.Minimized
    End Sub

    '“帮助”
    Private Sub BtnTitleHelp_Click() Handles BtnTitleHelp.Click
        OpenWebsite("https://www.bilibili.com/video/BV1uT4y1P7CX")
    End Sub
#End Region

#Region "窗体事件"
    Public Sub AddResizer()
        CanResize = True
    End Sub
    Public Sub RemoveResizer()
        CanResize = False
    End Sub

    '按键事件
    Private Sub FormMain_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.IsRepeat Then Return
        '调用弹窗：回车选择第一个，Esc 选择最后一个
        If PanMsg.Children.Count > 0 Then
            If e.Key = Key.Enter Then
                CType(PanMsg.Children(0), Object).Btn1_Click()
                Return
            ElseIf e.Key = Key.Escape Then
                Dim Msg As Object = PanMsg.Children(0)
                If TypeOf Msg IsNot MyMsgInput AndAlso TypeOf Msg IsNot MyMsgSelect AndAlso Msg.Btn3.Visibility = Visibility.Visible Then
                    Msg.Btn3_Click()
                ElseIf Msg.Btn2.Visibility = Visibility.Visible Then
                    Msg.Btn2_Click()
                Else
                    Msg.Btn1_Click()
                End If
                Return
            End If
        End If
        '按 ESC 返回上一级
        If e.Key = Key.Escape Then TriggerPageBack()
        '更改隐藏实例可见性
        If e.Key = Key.F11 AndAlso PageCurrent = FormMain.PageType.InstanceSelect Then
            FrmSelectRight.ShowHidden = Not FrmSelectRight.ShowHidden
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Return
        End If
        '更改功能隐藏可见性
        If e.Key = Key.F12 Then
            PageSetupUI.HiddenForceShow = Not PageSetupUI.HiddenForceShow
            If PageSetupUI.HiddenForceShow Then
                Hint("功能隐藏设置已暂时关闭！", HintType.Finish)
            Else
                Hint("功能隐藏设置已重新开启！", HintType.Finish)
            End If
            PageSetupUI.HiddenRefresh()
            Return
        End If
        '按 F5 刷新页面
        If e.Key = Key.F5 Then
            If TypeOf PageLeft Is IRefreshable Then CType(PageLeft, IRefreshable).Refresh()
            If TypeOf PageRight Is IRefreshable Then CType(PageRight, IRefreshable).Refresh()
            Return
        End If
        '调用启动游戏
        If e.Key = Key.Enter AndAlso PageCurrent = FormMain.PageType.Launch Then
            If IsAprilEnabled AndAlso Not IsAprilGiveup Then
                Hint("木大！")
            Else
                FrmLaunchLeft.LaunchButtonClick()
            End If
        End If
        '修复按下 Alt 后误认为弹出系统菜单导致的冻结
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then e.Handled = True
    End Sub
    Private Sub FormMain_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseDown
        '鼠标侧键返回上一级
        If FrmMain.PanMsg.Children.Count > 0 OrElse WaitingMyMsgBox.Any Then Return '弹窗中（#5513）
        If e.ChangedButton = MouseButton.XButton1 OrElse e.ChangedButton = MouseButton.XButton2 Then TriggerPageBack()
    End Sub
    Private Sub TriggerPageBack()
        If PageCurrent = PageType.Download AndAlso PageCurrentSub = PageSubType.DownloadInstall AndAlso FrmDownloadInstall.IsInSelectPage Then
            FrmDownloadInstall.ExitSelectPage()
        ElseIf PageCurrent = PageType.InstanceSetup AndAlso PageCurrentSub = PageSubType.VersionInstall AndAlso FrmInstanceInstall.IsInSelectPage Then
            FrmInstanceInstall.ExitSelectPage()
        Else
            PageBack()
        End If
    End Sub

    '切回窗口
    Private Sub FormMain_Activated() Handles Me.Activated
        Try
            ModMainWindowFocusShell.HandleActivated(PageCurrent, PageCurrentSub)
        Catch ex As Exception
            Log(ex, "切回窗口时出错", LogLevel.Feedback)
        End Try
    End Sub

    '文件拖放
    Private Sub HandleDrag(sender As Object, e As DragEventArgs)
        ModMainWindowDragShell.HandleDrag(e)
    End Sub
    Private Sub FrmMain_Drop(sender As Object, e As DragEventArgs) Handles Me.Drop
        ModMainWindowDragShell.HandleDrop(e)
    End Sub

    '接受到 Windows 窗体事件
    Public IsSystemTimeChanged As Boolean = False
    Private Function WndProc(hwnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr, ByRef handled As Boolean) As IntPtr
        handled = ModMainWindowWindowShell.HandleWindowMessage(
            hwnd,
            msg,
            lParam,
            IsWindowLoadFinished,
            AddressOf ShowWindowToTop,
            Sub(value) IsSystemTimeChanged = value)
        Return IntPtr.Zero
    End Function

    '窗口隐藏与置顶
    Private _Hidden As Boolean = False
    Public Property Hidden As Boolean
        Get
            Return _Hidden
        End Get
        Set(value As Boolean)
            If _Hidden = value Then Return
            _Hidden = value
            ModMainWindowWindowShell.ApplyHiddenState(Me, value, AddressOf ShowWindowToTop)
        End Set
    End Property
    '解决龙猫的非通用实现史山
    Protected Overrides Sub OnActivated(e As EventArgs)
        MyBase.OnActivated(e)
        If Hidden Then Hidden = False
    End Sub
    ''' <summary>
    ''' 把当前窗口拖到最前面。
    ''' </summary>
    Public Sub ShowWindowToTop()
        ModMainWindowWindowShell.ShowWindowToTop(Me, FrmHandle, Sub() _Hidden = False)
    End Sub
    '背景视频循环播放
    Private Sub VideoEnded(sender As Object, e As RoutedEventArgs)
        VideoBack.Position = TimeSpan.Zero
        VideoBack.Play()
    End Sub
    '最小化时暂停背景视频
    Private Sub WindowStateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        Select Case Me.WindowState
            Case WindowState.Minimized
                ModVideoBack.IsMinimized = True
                VideoPause()
            Case WindowState.Normal
                ModVideoBack.IsMinimized = False
                VideoPlay()
        End Select
    End Sub

#End Region

#Region "切换页面"

    '页面种类与属性
    '注意，这一枚举在 “切换页面” EventType 中调用，应视作公开 API 的一部分
    ''' <summary>
    ''' 页面种类。
    ''' </summary>
    Public Enum PageType
        ''' <summary>
        ''' 启动。
        ''' </summary>
        Launch = 0
        ''' <summary>
        ''' 下载。
        ''' </summary>
        Download = 1
        ''' <summary>
        ''' 联机。
        ''' </summary>
        Tools = 3
        ''' <summary>
        ''' 设置。
        ''' </summary>
        Setup = 2
        ''' <summary>
        ''' 实例选择。这是一个副页面。
        ''' </summary>
        InstanceSelect = 5
        ''' <summary>
        ''' 任务管理。这是一个副页面。
        ''' </summary>
        TaskManager = 6
        ''' <summary>
        ''' 实例设置。这是一个副页面。
        ''' </summary>
        InstanceSetup = 7
        ''' <summary>
        ''' 资源工程详情。这是一个副页面。
        ''' </summary>
        CompDetail = 8
        ''' <summary>
        ''' 帮助详情。这是一个副页面。
        ''' </summary>
        HelpDetail = 9
        ''' <summary>
        ''' 游戏实时日志。这是一个副页面。
        ''' </summary>
        GameLog = 10
        ''' <summary>
        ''' 存档详细管理，这是一个副页面。
        ''' </summary>
        VersionSaves = 12
        ''' <summary>
        ''' 主页市场，这是一个副页面。
        ''' </summary>
        HomePageMarket = 13
    End Enum
    ''' <summary>
    ''' 次要页面种类。其数值必须与 StackPanel 中的下标一致。
    ''' </summary>
    Public Enum PageSubType
        [Default] = 0
        DownloadInstall = 1
        DownloadMod = 2
        DownloadPack = 3
        DownloadDataPack = 4
        DownloadResourcePack = 5
        DownloadShader = 6
        DownloadWorld = 7
        DownloadCompFavorites = 8
        DownloadClient = 9
        DownloadOptiFine = 10
        DownloadForge = 11
        DownloadNeoForge = 12
        DownloadCleanroom = 13
        DownloadFabric = 14
        DownloadQuilt = 15
        DownloadLiteLoader = 16
        DownloadLabyMod = 17
        DownloadLegacyFabric = 18

        SetupLaunch = 0
        SetupUI = 1
        SetupGameManage = 2
        SetupLink = 3
        SetupAbout = 4
        SetupLog = 5
        SetupFeedback = 6
        SetupGameLink = 7
        SetupUpdate = 8
        SetupJava = 9
        SetupLauncherMisc = 10

        ToolsGameLink = 1
        ToolsLauncherHelp = 2
        ToolsTest = 3

        VersionOverall = 0
        VersionSetup = 1
        VersionExport = 2
        VersionWorld = 3
        VersionScreenshot = 4
        VersionMod = 5
        VersionModDisabled = 6
        VersionResourcePack = 7
        VersionShader = 8
        VersionSchematic = 9
        VersionInstall = 10
        VersionServer = 11
        VersionSavesInfo = 0
        VersionSavesBackup = 1
        VersionSavesDatapack = 2
    End Enum
    ''' <summary>
    ''' 获取次级页面的名称。若并非次级页面则返回空字符串，故可以以此判断是否为次级页面。
    ''' </summary>
    Private Function PageNameGet(Stack As PageStackData) As String
        Select Case Stack.Page
            Case PageType.InstanceSelect
                Return "实例选择"
            Case PageType.TaskManager
                Return "任务管理"
            Case PageType.GameLog
                Return "实时日志"
            Case PageType.InstanceSetup
                Return "实例设置 - " & If(PageInstanceLeft.Instance Is Nothing, "未知实例", PageInstanceLeft.Instance.Name)
            Case PageType.CompDetail
                Return "资源下载 - " & CType(Stack.Additional(0), CompProject).TranslatedName
            Case PageType.HelpDetail
                Return CType(Stack.Additional(0), HelpEntry).Title
            Case PageType.VersionSaves
                Return $"存档管理 - {GetFolderNameFromPath(Stack.Additional)}"
            Case PageType.HomePageMarket
                Return "主页市场"
            Case Else
                Return ""
        End Select
    End Function
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh(Type As PageStackData)
        LabTitleInner.Text = PageNameGet(Type)
    End Sub
    ''' <summary>
    ''' 刷新次级页面的名称。
    ''' </summary>
    Public Sub PageNameRefresh()
        PageNameRefresh(PageCurrent)
    End Sub

    '页面状态存储
    ''' <summary>
    ''' 当前的主页面。
    ''' </summary>
    Public PageCurrent As PageStackData = PageType.Launch
    ''' <summary>
    ''' 上一个主页面。
    ''' </summary>
    Public PageLast As PageStackData = PageType.Launch
    ''' <summary>
    ''' 当前的子页面。
    ''' </summary>
    Public ReadOnly Property PageCurrentSub As PageSubType
        Get
            Select Case PageCurrent
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    Return FrmDownloadLeft.PageID
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    Return FrmSetupLeft.PageID
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    Return FrmInstanceLeft.PageID
                Case Else
                    Return 0 '没有子页面
            End Select
        End Get
    End Property
    ''' <summary>
    ''' 上层页面的编号堆栈，用于返回。
    ''' </summary>
    Public PageStack As New List(Of PageStackData)
    Public Class PageStackData

        Public Page As PageType
        Public Additional As Object

        Public Overrides Function Equals(other As Object) As Boolean
            If other Is Nothing Then Return False
            If TypeOf other Is PageStackData Then
                Dim PageOther As PageStackData = other
                If Page <> PageOther.Page Then Return False
                If Additional Is Nothing Then
                    Return PageOther.Additional Is Nothing
                Else
                    Return PageOther.Additional IsNot Nothing AndAlso Additional.Equals(PageOther.Additional)
                End If
            ElseIf TypeOf other Is Integer Then
                If Page <> other Then Return False
                Return Additional Is Nothing
            Else
                Return False
            End If
        End Function
        Public Shared Operator =(left As PageStackData, right As PageStackData) As Boolean
            Return EqualityComparer(Of PageStackData).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As PageStackData, right As PageStackData) As Boolean
            Return Not left = right
        End Operator
        Public Shared Widening Operator CType(Value As PageType) As PageStackData
            Return New PageStackData With {.Page = Value}
        End Operator
        Public Shared Widening Operator CType(Value As PageStackData) As PageType
            Return Value.Page
        End Operator
    End Class
    Public PageLeft As MyPageLeft, PageRight As MyPageRight

    '引发实际页面切换的入口
    Private IsChangingPage As Boolean = False
    ''' <summary>
    ''' 切换页面，并引起对应选择 UI 的改变。
    ''' </summary>
    Public Sub PageChange(Stack As PageStackData, Optional SubType As PageSubType = PageSubType.Default)
        If PageNameGet(Stack) = "" Then
            '切换到主页面
            PageChangeExit()
            IsChangingPage = True '防止下面的勾选直接触发了 PageChangeActual
            CType(PanTitleSelect.Children(Stack), MyRadioButton).SetChecked(True, True, PageNameGet(PageCurrent) = "")
            IsChangingPage = False
            Select Case Stack.Page
                Case PageType.Download
                    If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                    For Each item In FrmDownloadLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
                Case PageType.Setup
                    If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                    If TypeOf FrmSetupLeft.PanItem.Children(SubType) Is MyListItem Then CType(FrmSetupLeft.PanItem.Children(SubType), MyListItem).SetChecked(True, True, Stack = PageCurrent)
            End Select
            PageChangeActual(Stack, SubType)
        Else
            '切换到次页面
            Select Case Stack.Page
                Case PageType.InstanceSetup
                    If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                    For Each item In FrmInstanceLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
                Case PageType.VersionSaves
                    If FrmInstanceSavesLeft Is Nothing Then FrmInstanceSavesLeft = New PageInstanceSavesLeft
                    For Each item In FrmInstanceSavesLeft.PanItem.Children
                        If item.GetType() Is GetType(MyListItem) AndAlso Val(item.tag) = SubType Then
                            CType(item, MyListItem).SetChecked(True, True, Stack = PageCurrent)
                            Exit For
                        End If
                    Next
            End Select
            PageChangeActual(Stack, SubType)
        End If
    End Sub
    ''' <summary>
    ''' 通过点击导航栏改变页面。
    ''' </summary>
    Private Sub BtnTitleSelect_Click(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnTitleSelect0.Check, BtnTitleSelect1.Check, BtnTitleSelect2.Check, BtnTitleSelect3.Check
        If IsChangingPage Then Return
        PageChangeActual(Val(sender.Tag))
    End Sub
    ''' <summary>
    ''' 通过点击返回按钮或手动触发返回来改变页面。
    ''' </summary>
    Public Sub PageBack() Handles BtnTitleInner.Click
        If PageStack.Any() Then
            PageChangeActual(PageStack(0))
        Else
            PageChange(PageType.Launch)
        End If
    End Sub

    '实际处理页面切换
    ''' <summary>
    ''' 切换现有页面的实际方法。
    ''' </summary>
    Private Sub PageChangeActual(Stack As PageStackData, Optional SubType As PageSubType = -1)
        If PageCurrent = Stack AndAlso (PageCurrentSub = SubType OrElse SubType = -1) Then Return
        AniControlEnabled += 1
        Try

#Region "子页面处理"
            Dim PageName As String = PageNameGet(Stack)
            If PageName = "" Then
                '即将切换到一个顶级页面
                PageChangeExit()
            Else
                '即将切换到一个子页面
                If PageStack.Any Then
                    '子页面 → 另一个子页面，更新
                    AniStart({
                        AaOpacity(LabTitleInner, -LabTitleInner.Opacity, 130),
                        AaCode(Sub() LabTitleInner.Text = PageName,, True),
                        AaOpacity(LabTitleInner, 1, 150, 30)
                    }, "FrmMain Titlebar SubLayer")
                    If PageStack.Contains(Stack) Then
                        '返回到更上层的子页面
                        Do While PageStack.Contains(Stack)
                            PageStack.RemoveAt(0)
                        Loop
                    Else
                        '进入更深层的子页面
                        PageStack.Insert(0, PageCurrent)
                    End If
                Else
                    '主页面 → 子页面，进入
                    PanTitleInner.Visibility = Visibility.Visible
                    PanTitleMain.IsHitTestVisible = False
                    PanTitleInner.IsHitTestVisible = True
                    PageNameRefresh(Stack)
                    AniStart({
                        AaOpacity(PanTitleMain, -PanTitleMain.Opacity, 150),
                        AaX(PanTitleMain, 12 - PanTitleMain.Margin.Left, 150,, New AniEaseInFluent(AniEasePower.Weak)),
                        AaOpacity(PanTitleInner, 1 - PanTitleInner.Opacity, 150, 200),
                        AaX(PanTitleInner, -PanTitleInner.Margin.Left, 350, 200, New AniEaseOutBack),
                        AaCode(Sub() PanTitleMain.Visibility = Visibility.Collapsed,, True)
                    }, "FrmMain Titlebar FirstLayer")
                    PageStack.Insert(0, PageCurrent)
                End If
            End If
#End Region

#Region "实际更改页面框架 UI"
            PageLast = PageCurrent
            PageCurrent = Stack
            Dim pageTargets = ModMainWindowPageFrameShell.ResolvePageTargets(Stack, SubType)
            PageChangeAnim(pageTargets.Left, pageTargets.Right)
#End Region

#Region "设置为最新状态"
            BtnExtraDownload.ShowRefresh()
            BtnExtraApril.ShowRefresh()
#End Region

            Log("[Control] 切换主要页面：" & GetStringFromEnum(Stack) & ", " & SubType)
        Catch ex As Exception
            Log(ex, "切换主要页面失败（ID " & PageCurrent.Page & "）", LogLevel.Feedback)
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Sub PageChangeAnim(TargetLeft As FrameworkElement, TargetRight As FrameworkElement)
        ModMainWindowPageAnimationShell.AnimatePageChange(
            Me,
            CType(TargetLeft, MyPageLeft),
            CType(TargetRight, MyPageRight),
            Sub() RunInUi(Sub() PanMainLeft_Resize(PanMainLeft.ActualWidth), True),
            Sub() RunInUi(Sub() BtnExtraBack.ShowRefresh(), True))
    End Sub
    ''' <summary>
    ''' 退出子界面。
    ''' </summary>
    Private Sub PageChangeExit()
        ModMainWindowPageAnimationShell.ExitSubPage(Me, PageStack)
    End Sub

    '左边栏改变
    Private Sub PanMainLeft_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles PanMainLeft.SizeChanged
        If Not e.WidthChanged Then Return
        PanMainLeft_Resize(e.NewSize.Width)
    End Sub
    Private Sub PanMainLeft_Resize(NewWidth As Double)
        Dim Delta As Double = NewWidth - RectLeftBackground.Width
        If Math.Abs(Delta) > 0.1 AndAlso AniControlEnabled = 0 Then
            If PanMain.Opacity < 0.1 Then PanMainLeft.IsHitTestVisible = False '避免左边栏指向背景未能完美覆盖左边栏
            If NewWidth > 0 Then
                '宽度足够，显示
                AniStart({
                    AaWidth(RectLeftBackground, NewWidth - RectLeftBackground.Width, 180,, New AniEaseOutFluent(AniEasePower.ExtraStrong)),
                    AaOpacity(RectLeftShadow, 1 - RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            Else
                '宽度不足，隐藏
                AniStart({
                    AaWidth(RectLeftBackground, -RectLeftBackground.Width, 180,, New AniEaseOutFluent),
                    AaOpacity(RectLeftShadow, -RectLeftShadow.Opacity, 180),
                    AaCode(Sub() PanMainLeft.IsHitTestVisible = True, 150)
                }, "FrmMain LeftChange", True)
            End If
        Else
            RectLeftBackground.Width = NewWidth
            PanMainLeft.IsHitTestVisible = True
            AniStop("FrmMain LeftChange")
        End If
    End Sub

#End Region

#Region "控件拖动"

    '在时钟中调用，使得即使鼠标在窗口外松开，也可以释放控件
    Public Sub DragTick()
        If DragControl Is Nothing Then Return
        If Not Mouse.LeftButton = MouseButtonState.Pressed Then
            DragStop()
        End If
    End Sub
    '在鼠标移动时调用，以改变 Slider 位置
    Public Sub DragDoing() Handles PanBack.MouseMove
        If DragControl Is Nothing Then Return
        If Mouse.LeftButton = MouseButtonState.Pressed Then
            DragControl.DragDoing()
        Else
            DragStop()
        End If
    End Sub
    Public Sub DragStop()
        '存在其他线程调用的可能性，因此需要确保在 UI 线程运行
        RunInUi(Sub()
                    If DragControl Is Nothing Then Return
                    Dim Control = DragControl
                    DragControl = Nothing
                    Control.DragStop() '控件会在该事件中判断 DragControl，所以得放在后面
                End Sub)
    End Sub

#End Region

#Region "附加按钮"

    '更新重启
    Private Sub BtnExtraUpdateRestart_Click() Handles BtnExtraUpdateRestart.Click
        UpdateRestart(True, True)
    End Sub
    Private Function BtnExtraUpdateRestart_ShowCheck() As Boolean
        Return IsUpdateWaitingRestart
    End Function
    
    '音乐
    Private Sub BtnExtraMusic_Click(sender As Object, e As EventArgs) Handles BtnExtraMusic.Click
        MusicControlPause()
    End Sub
    Private Sub BtnExtraMusic_RightClick(sender As Object, e As EventArgs) Handles BtnExtraMusic.RightClick
        MusicControlNext()
    End Sub

    '任务管理
    Private Sub BtnExtraDownload_Click(sender As Object, e As EventArgs) Handles BtnExtraDownload.Click
        PageChange(PageType.TaskManager)
    End Sub
    Private Function BtnExtraDownload_ShowCheck() As Boolean
        Return HasDownloadingTask() AndAlso Not PageCurrent = PageType.TaskManager
    End Function

    '投降
    Public Sub AprilGiveup() Handles BtnExtraApril.Click
        If IsAprilEnabled AndAlso Not IsAprilGiveup Then
            Hint("=D", HintType.Finish)
            IsAprilGiveup = True
            FrmLaunchLeft.AprilScaleTrans.ScaleX = 1
            FrmLaunchLeft.AprilScaleTrans.ScaleY = 1
            BtnExtraApril.ShowRefresh()
        End If
    End Sub
    Public Function BtnExtraApril_ShowCheck() As Boolean
        Return IsAprilEnabled AndAlso Not IsAprilGiveup AndAlso PageCurrent = PageType.Launch
    End Function

    '关闭 Minecraft
    Public Sub BtnExtraShutdown_Click() Handles BtnExtraShutdown.Click
        Try
            If McLaunchLoaderReal IsNot Nothing Then McLaunchLoaderReal.Abort()
            For Each Watcher In McWatcherList
                Watcher.Kill()
            Next
            Hint("已关闭运行中的 Minecraft！", HintType.Finish)
        Catch ex As Exception
            Log(ex, "强制关闭所有 Minecraft 失败", LogLevel.Feedback)
        End Try
    End Sub
    Public Function BtnExtraShutdown_ShowCheck() As Boolean
        Return HasRunningMinecraft
    End Function

    '游戏日志
    Public Sub BtnExtraLog_Click() Handles BtnExtraLog.Click
        PageChange(PageType.GameLog)
    End Sub
    Public Function BtnExtraLog_ShowCheck() As Boolean
        If FrmLogLeft Is Nothing OrElse FrmLogRight Is Nothing OrElse PageCurrent = PageType.GameLog Then Return False
        Return FrmLogLeft.ShownLogs.Count > 0
    End Function

    ''' <summary>
    ''' 返回顶部。
    ''' </summary>
    Public Sub BackToTop() Handles BtnExtraBack.Click
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        If RealScroll IsNot Nothing Then
            RealScroll.PerformVerticalOffsetDelta(-RealScroll.VerticalOffset)
        Else
            Log("[UI] 无法返回顶部，未找到合适的 RealScroll", LogLevel.Hint)
        End If
    End Sub
    Private Function BtnExtraBack_ShowCheck() As Boolean
        Dim RealScroll As MyScrollViewer = BtnExtraBack_GetRealChild()
        Return RealScroll IsNot Nothing AndAlso RealScroll.Visibility = Visibility.Visible AndAlso RealScroll.VerticalOffset > Height + If(BtnExtraBack.Show, 0, 700)
    End Function
    Private Function BtnExtraBack_GetRealChild() As MyScrollViewer
        If PanMainRight.Child Is Nothing OrElse TypeOf PanMainRight.Child IsNot MyPageRight Then Return Nothing
        Return CType(PanMainRight.Child, MyPageRight).PanScroll
    End Function

#End Region

    '愚人节鼠标位置
    Public lastMouseArg As MouseEventArgs = Nothing
    Private Sub FormMain_MouseMove(sender As Object, e As MouseEventArgs) Handles Me.MouseMove
        lastMouseArg = e
    End Sub

End Class
