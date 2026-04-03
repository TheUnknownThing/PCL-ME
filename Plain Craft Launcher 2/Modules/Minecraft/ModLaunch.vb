Imports System.IO.Compression
Imports System.Net.Http
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils
Imports PCL.Core.Utils.OS
Imports PCL.Core.Utils.Processes
Imports PCL.Core.App
Imports PCL.Core.Minecraft.Launch.Utils
Imports PCL.Core.Utils.Exts

Public Module ModLaunch

#Region "开始"

    Public IsLaunching As Boolean = False
    Public CurrentLaunchOptions As McLaunchOptions = Nothing
    Public Class McLaunchOptions
        ''' <summary>
        ''' 强制指定在启动后进入的服务器 IP。
        ''' 默认值：Nothing。使用实例设置的值。
        ''' </summary>
        Public ServerIp As String = Nothing
        ''' <summary>
        ''' 指定在启动之后进入的存档名称。
        ''' 默认值：Nothing。使用实例设置的值。
        ''' </summary>
        Public WorldName As String = Nothing
        ''' <summary>
        ''' 将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        ''' 默认值：Nothing。不保存。
        ''' </summary>
        Public SaveBatch As String = Nothing
        ''' <summary>
        ''' 强行指定启动的 MC 实例。
        ''' 默认值：Nothing。使用 McInstanceCurrent。
        ''' </summary>
        Public Instance As McInstance = Nothing
        ''' <summary>
        ''' 额外的启动参数。
        ''' </summary>
        Public ExtraArgs As New List(Of String)
        ''' <summary>
        ''' 是否为 “测试游戏” 按钮启动的游戏。
        ''' 如果是，则显示游戏实时日志。
        ''' </summary>
        Public IsTest As Boolean = False
    End Class
    ''' <summary>
    ''' 尝试启动 Minecraft。必须在 UI 线程调用。
    ''' 返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    ''' </summary>
    Public Function McLaunchStart(Optional Options As McLaunchOptions = Nothing) As Boolean
        IsLaunching = True
        CurrentLaunchOptions = If(Options, New McLaunchOptions)
        '预检查
        If Not RunInUi() Then Throw New Exception("McLaunchStart 必须在 UI 线程调用！")
        If McLaunchLoader.State = LoadState.Loading Then
            Hint("已有游戏正在启动中！", HintType.Critical)
            IsLaunching = False
            Return False
        End If
        '强制切换需要启动的实例
        If CurrentLaunchOptions.Instance IsNot Nothing AndAlso McInstanceSelected <> CurrentLaunchOptions.Instance Then
            McLaunchLog("在启动前切换到实例 " & CurrentLaunchOptions.Instance.Name)
            '检查实例
            CurrentLaunchOptions.Instance.Load()
            If CurrentLaunchOptions.Instance.State = McInstanceState.Error Then
                Hint("无法启动 Minecraft：" & CurrentLaunchOptions.Instance.Desc, HintType.Critical)
                IsLaunching = False
                Return False
            End If
            '切换实例
            McInstanceSelected = CurrentLaunchOptions.Instance
            Setup.Set("LaunchInstanceSelect", McInstanceSelected.Name)
            FrmLaunchLeft.RefreshButtonsUI()
            FrmLaunchLeft.RefreshPage(False)
        End If
        FrmMain.AprilGiveup()
        '禁止进入实例选择页面（否则就可以在启动中切换 McInstanceCurrent 了）
        FrmMain.PageStack = FrmMain.PageStack.Where(Function(p) p.Page <> FormMain.PageType.InstanceSelect).ToList
        '实际启动加载器
        McLaunchLoader.Start(Options, IsForceRestart:=True)
        Return True
    End Function

    ''' <summary>
    ''' 记录启动日志。
    ''' </summary>
    Public Sub McLaunchLog(Text As String)
        Text = FilterUserName(FilterAccessToken(Text, "*"), "*")
        RunInUi(Sub() FrmLaunchRight.LabLog.Text += vbCrLf & "[" & TimeUtils.GetTimeNow() & "] " & Text)
        Log("[Launch] " & Text)
    End Sub

    '启动状态切换
    Public McLaunchLoader As New LoaderTask(Of McLaunchOptions, Object)("Loader Launch", AddressOf McLaunchStart) With {.OnStateChanged = AddressOf McLaunchState}
    Public McLaunchLoaderReal As LoaderCombo(Of Object)
    Public McLaunchProcess As Process
    Public McLaunchWatcher As Watcher
    Private McLaunchSessionPlan As MinecraftLaunchSessionStartWorkflowPlan = Nothing
    Private McLaunchPrerunPlan As MinecraftLaunchPrerunWorkflowPlan = Nothing
    Private Sub McLaunchState(Loader As LoaderTask(Of McLaunchOptions, Object))
        Select Case McLaunchLoader.State
            Case LoadState.Finished, LoadState.Failed, LoadState.Waiting, LoadState.Aborted
                FrmLaunchLeft.PageChangeToLogin()
            Case LoadState.Loading
                '在预检测结束后再触发动画
                FrmLaunchRight.LabLog.Text = ""
        End Select
    End Sub
    ''' <summary>
    ''' 指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    ''' </summary>
    Private AbortHint As String = Nothing

    '实际的启动方法
    Private Sub McLaunchStart(Loader As LoaderTask(Of McLaunchOptions, Object))
        '开始动画
        RunInUiWait(AddressOf FrmLaunchLeft.PageChangeToLaunching)
        McLaunchSessionPlan = Nothing
        McLaunchPrerunPlan = Nothing
        '预检测（预检测的错误将直接抛出）
        Try
            McLaunchPrecheck()
            McLaunchLog("预检测已通过")
        Catch ex As Exception
            If Not ex.Message.StartsWithF("$$") Then Hint(ex.Message, HintType.Critical)
            Throw
        End Try
        '正式加载
        Try
            '构造主加载器
            Dim Loaders As New List(Of LoaderBase) From {
                New LoaderTask(Of Integer, Integer)("获取 Java", AddressOf McLaunchJava) With {.ProgressWeight = 4, .Block = False},
                McLoginLoader, '.ProgressWeight = 15, .Block = False
                New LoaderCombo(Of String)("补全文件", DlClientFix(McInstanceSelected, False, AssetsIndexExistsBehaviour.DownloadInBackground)) With {.ProgressWeight = 15, .Show = False},
                New LoaderTask(Of String, List(Of McLibToken))("获取启动参数", AddressOf McLaunchArgumentMain) With {.ProgressWeight = 2},
                New LoaderTask(Of List(Of McLibToken), Integer)("解压文件", AddressOf McLaunchNatives) With {.ProgressWeight = 2},
                New LoaderTask(Of Integer, Integer)("预启动处理", AddressOf McLaunchPrerun) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("执行自定义命令", AddressOf McLaunchCustom) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Process)("启动进程", AddressOf McLaunchRun) With {.ProgressWeight = 2},
                New LoaderTask(Of Process, Integer)("等待游戏窗口出现", AddressOf McLaunchWait) With {.ProgressWeight = 1},
                New LoaderTask(Of Integer, Integer)("结束处理", AddressOf McLaunchEnd) With {.ProgressWeight = 1}
            }
            '内存优化
            Select Case Setup.Get("VersionRamOptimize", instance:=McInstanceSelected)
                Case 0 '全局
                    If Setup.Get("LaunchArgumentRam") Then '使用全局设置
                        CType(Loaders(2), LoaderCombo(Of String)).Block = False
                        Loaders.Insert(3, New LoaderTask(Of Integer, Integer)("内存优化", AddressOf McLaunchMemoryOptimize) With {.ProgressWeight = 30})
                    End If
                Case 1 '开启
                    CType(Loaders(2), LoaderCombo(Of String)).Block = False
                    Loaders.Insert(3, New LoaderTask(Of Integer, Integer)("内存优化", AddressOf McLaunchMemoryOptimize) With {.ProgressWeight = 30})
                Case 2 '关闭
            End Select
            Dim LaunchLoader As New LoaderCombo(Of Object)("Minecraft 启动", Loaders) With {.Show = False}
            If McLoginLoader.State = LoadState.Finished Then McLoginLoader.State = LoadState.Waiting '要求重启登录主加载器，它会自行决定是否启动副加载器
            '等待加载器执行并更新 UI
            McLaunchLoaderReal = LaunchLoader
            AbortHint = Nothing
            LaunchLoader.Start()
            '任务栏进度条
            LoaderTaskbarAdd(LaunchLoader)
            Do While LaunchLoader.State = LoadState.Loading
                FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
                Thread.Sleep(100)
            Loop
            FrmLaunchLeft.Dispatcher.Invoke(AddressOf FrmLaunchLeft.LaunchingRefresh)
            '成功与失败处理
            Select Case LaunchLoader.State
                Case LoadState.Finished
                    ModLaunchResultShell.ShowCompletionNotification(
                        If(McInstanceSelected?.Name, ""),
                        MinecraftLaunchOutcome.Succeeded,
                        CurrentLaunchOptions?.SaveBatch IsNot Nothing,
                        AbortHint)
                Case LoadState.Aborted
                    ModLaunchResultShell.ShowCompletionNotification(
                        If(McInstanceSelected?.Name, ""),
                        MinecraftLaunchOutcome.Aborted,
                        CurrentLaunchOptions?.SaveBatch IsNot Nothing,
                        AbortHint)
                Case LoadState.Failed
                    Throw LaunchLoader.Error
                Case Else
                    Throw New Exception("错误的状态改变：" & GetStringFromEnum(CType(LaunchLoader.State, [Enum])))
            End Select
            IsLaunching = False
        Catch ex As Exception
            Dim CurrentEx = ex
NextInner:
            If CurrentEx.Message.StartsWithF("$") Then
                '若有以 $ 开头的错误信息，则以此为准显示提示
                '若错误信息为 $$，则不提示
                If Not CurrentEx.Message = "$$" Then
                    ModLaunchResultShell.ShowFailureMessage(
                        CurrentEx.Message.TrimStart("$"),
                        CurrentLaunchOptions?.SaveBatch IsNot Nothing)
                End If
                Throw
            ElseIf CurrentEx.InnerException IsNot Nothing Then
                '检查下一级错误
                CurrentEx = CurrentEx.InnerException
                GoTo NextInner
            Else
                '没有特殊处理过的错误信息
                ModLaunchResultShell.LogUnhandledFailure(ex, CurrentLaunchOptions?.SaveBatch IsNot Nothing, AddressOf McLaunchLog)
                Throw
            End If
        End Try
    End Sub

#End Region

#Region "内存优化"

    Private Sub McLaunchMemoryOptimize(Loader As LoaderTask(Of Integer, Integer))
        McLaunchLog("内存优化开始")
        Dim Finished As Boolean = False
        RunInNewThread(
        Sub()
            PageToolsTest.MemoryOptimize(False)
            Finished = True
        End Sub, "Launch Memory Optimize")
        Do While Not Finished AndAlso Not Loader.IsAborted
            If Loader.Progress < 0.7 Then
                Loader.Progress += 0.007 '10s
            Else
                Loader.Progress += (0.95 - Loader.Progress) * 0.02 '最快 += 0.005
            End If
            Thread.Sleep(100)
        Loop
    End Sub

#End Region

#Region "预检测"

    Private Sub McLaunchPrecheck()
        If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomUtils.NextInt(100, 2000))
        '检查实例
        If McInstanceSelected IsNot Nothing Then McInstanceSelected.Load()
        '检查输入信息
        Dim CheckResult As String = ""
        RunInUiWait(Sub() CheckResult = If(SelectedProfile Is Nothing, "", IsProfileValid()))
        Dim precheckResult = ModLaunchPrecheckShell.EvaluatePrecheck(
            McInstanceSelected,
            SelectedProfile,
            ProfileList,
            CheckResult)
        If Not precheckResult.IsSuccess Then Throw New ArgumentException(precheckResult.FailureMessage)
        ModLaunchInteractionShell.RunPrecheckPrompts(precheckResult, CurrentLaunchOptions)
    End Sub

#End Region

#Region "Java 处理"

    Public McLaunchJavaSelected As JavaEntry = Nothing
    Private Sub McLaunchJava(task As LoaderTask(Of Integer, Integer))
        McLaunchJavaSelected = ModLaunchJavaWorkflowShell.ResolveLaunchJava(
            McInstanceSelected,
            task,
            AddressOf ResolveLaunchJavaSelection,
            AddressOf McLaunchLog)
        If task.IsAborted Then Return
    End Sub

#End Region

#Region "启动参数"

    Private McLaunchArgument As String

    ''' <summary>
    ''' 释放 Java Wrapper 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractJavaWrapper() As String
        Return ModLaunchArgumentShell.ExtractJavaWrapperShell(PathPure)
    End Function

    ''' <summary>
    ''' 释放 linkd 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractLinkD() As String
        Return ModLaunchArgumentShell.ExtractLinkDShell(PathPure)
    End Function

    ''' <summary>
    ''' 判断是否使用 RetroWrapper。
    ''' TODO: 在更换为 Drop 比较版本号后可能不准确，需要测试确认。
    ''' </summary>
    Private Function McLaunchNeedsRetroWrapper(Mc As McInstance) As Boolean
        Return ModLaunchArgumentShell.ShouldUseRetroWrapper(Mc)
    End Function


    '主方法，合并 Jvm、Game、Replace 三部分的参数数据
    Private Sub McLaunchArgumentMain(Loader As LoaderTask(Of String, List(Of McLibToken)))
        McLaunchArgument = ModLaunchArgumentWorkflowShell.BuildLaunchArguments(
            McInstanceSelected,
            CurrentLaunchOptions,
            McLaunchJavaSelected,
            McLoginLoader.Output,
            If(McLoginAuthLoader.Input?.BaseUrl, Nothing),
            AddressOf GetNativesFolder,
            Loader,
            AddressOf McLaunchLog)
    End Sub

#End Region

#Region "解压 Natives"

    Private Sub McLaunchNatives(Loader As LoaderTask(Of List(Of McLibToken), Integer))
        ModLaunchNativesShell.SyncNatives(GetNativesFolder(), Loader.Input, ModeDebug, AddressOf McLaunchLog)
    End Sub
    ''' <summary>
    ''' 获取 Natives 文件夹路径，不以 \ 结尾。
    ''' </summary>
    Private Function GetNativesFolder() As String
        Return ModLaunchNativesShell.GetNativesFolder(McInstanceSelected)
    End Function

#End Region

#Region "启动与前后处理"

    Private Sub McLaunchPrerun()
        Dim launcherProfilesPath = McFolderSelected & "launcher_profiles.json"
        McLaunchPrerunPlan = MinecraftLaunchPrerunWorkflowService.BuildPlan(
            New MinecraftLaunchPrerunWorkflowRequest(
                launcherProfilesPath,
                McLoginLoader.Output.Type = "Microsoft",
                If(McLoginLoader.Output.Type = "Microsoft" AndAlso File.Exists(launcherProfilesPath), ReadFile(launcherProfilesPath), Nothing),
                McLoginLoader.Output.Name,
                McLoginLoader.Output.ClientToken,
                Date.Now,
                McInstanceSelected.PathIndie & "options.txt",
                File.Exists(McInstanceSelected.PathIndie & "options.txt"),
                ReadIni(McInstanceSelected.PathIndie & "options.txt", "lang", "none"),
                McInstanceSelected.PathIndie & "config\yosbr\options.txt",
                File.Exists(McInstanceSelected.PathIndie & "config\yosbr\options.txt"),
                Directory.Exists(McInstanceSelected.PathIndie & "saves"),
                McInstanceSelected.ReleaseTime,
                Setup.Get("LaunchArgumentWindowType"),
                Config.Tool.AutoChangeLanguage))

        '要求 Java 使用高性能显卡
        Dim javaExePath = If(McLaunchJavaSelected.Installation.JavawExePath, McLaunchJavaSelected.Installation.JavaExePath)
        ModLaunchPrerunShell.ApplyGpuPreference(javaExePath, Config.Launch.SetGpuPreference, AddressOf McLaunchLog)

        '更新 launcher_profiles.json
        ModLaunchPrerunShell.UpdateLauncherProfilesJson(McLaunchPrerunPlan.LauncherProfiles, McFolderSelected, AddressOf McLaunchLog)

        '更新 options.txt
        ModLaunchPrerunShell.ApplyOptionsSync(McLaunchPrerunPlan.Options, AddressOf McLaunchLog)

    End Sub

    Private Sub McLaunchCustom(Loader As LoaderTask(Of Integer, Integer))
        McLaunchSessionPlan = ModLaunchSessionPlanShell.BuildSessionStartPlan(
            McInstanceSelected,
            McLaunchJavaSelected,
            McLaunchArgument,
            BuildWatcherWorkflowRequest(),
            Function(text, replaceTime) ArgumentReplace(text, replaceTime))

        '输出 bat
        Try
            If ModLaunchSessionPlanShell.TryWriteLaunchScript(
                McLaunchSessionPlan,
                CurrentLaunchOptions.SaveBatch,
                Sub(hint) AbortHint = hint,
                Sub() Loader.Parent.Abort()) Then Return
        Catch ex As Exception
            Log(ex, "输出启动脚本失败")
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then Throw '直接触发启动失败
        End Try

        '执行自定义命令
        For Each shellPlan In McLaunchSessionPlan.CustomCommandShellPlans
            ModLaunchExecutionShell.ExecuteCustomCommand(shellPlan, Loader, AddressOf McLaunchLog)
        Next

    End Sub

    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))
        If McLaunchSessionPlan Is Nothing Then Throw New InvalidOperationException("缺少启动会话计划。")
        McLaunchProcess = ModLaunchExecutionShell.StartGameProcess(
            McLaunchSessionPlan.ProcessShellPlan,
            Loader,
            AddressOf McLaunchLog)
    End Sub
    Private Sub McLaunchWait(Loader As LoaderTask(Of Process, Integer))

        If McLaunchSessionPlan Is Nothing Then Throw New InvalidOperationException("缺少启动会话计划。")
        McLaunchWatcher = ModLaunchExecutionShell.WaitForGameWindow(
            McLaunchSessionPlan,
            Loader,
            McInstanceSelected,
            Function(rawWindowTitle) ArgumentReplace(rawWindowTitle, False),
            AddressOf McLaunchLog)
    End Sub
    Private Sub McLaunchEnd()
        McLaunchLog("开始启动结束处理")
        Dim shellPlan = MinecraftLaunchSessionWorkflowService.BuildPostLaunchPlan(
            New MinecraftLaunchPostLaunchShellRequest(
                Config.Launch.LauncherVisibility,
                Setup.Get("UiMusicStop"),
                Setup.Get("UiMusicStart")))

        '暂停或开始音乐播放
        ModLaunchSessionShell.ApplyMusicAction(shellPlan.MusicAction)
        '暂停视频背景播放
        ModLaunchSessionShell.ApplyVideoBackgroundAction(shellPlan.VideoBackgroundAction)
        '启动器可见性
        McLaunchLog("启动器可见性：" & Setup.Get("LaunchArgumentVisible"))
        If shellPlan.LauncherAction.LogMessage <> "" Then McLaunchLog(shellPlan.LauncherAction.LogMessage)
        ModLaunchSessionShell.ApplyLauncherAction(shellPlan.LauncherAction)

        '启动计数
        Setup.Set("SystemLaunchCount", Setup.Get("SystemLaunchCount") + shellPlan.GlobalLaunchCountIncrement)

        Setup.Set("VersionLaunchCount", Setup.Get("VersionLaunchCount", McInstanceSelected) + shellPlan.InstanceLaunchCountIncrement, instance:=McInstanceSelected)

    End Sub

    Private Function BuildWatcherWorkflowRequest() As MinecraftLaunchWatcherWorkflowRequest
        Return ModLaunchSessionArgumentShell.BuildWatcherWorkflowRequest(
            McInstanceSelected,
            McLaunchJavaSelected,
            McLoginLoader.Output,
            CurrentLaunchOptions.IsTest,
            AddressOf GetNativesFolder)
    End Function

    ''' <summary>
    ''' 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    ''' </summary>
    Private Function ArgumentReplace(text As String, replaceTime As Boolean, Optional escapeHandler As Func(Of String, String) = Nothing) As String
        Return ModLaunchSessionArgumentShell.ReplaceArgumentTokens(
            text,
            replaceTime,
            McLaunchJavaSelected,
            McInstanceSelected,
            McLoginLoader.State,
            McLoginLoader.Input,
            McLoginLoader.Output,
            escapeHandler)
    End Function

#End Region

End Module
