Imports System.IO.Compression
Imports System.Net.Http
Imports System.Text.Json.Nodes
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils
Imports PCL.Core.Utils.OS
Imports PCL.Core.App
Imports PCL.Core.Minecraft.Launch.Utils
Imports PCL.Core.Utils.Secret
Imports PCL.Core.Utils.Exts
Imports PCL.Core.IO.Net.Http.Client.Request

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
                    ShowLaunchCompletionNotification(MinecraftLaunchOutcome.Succeeded)
                Case LoadState.Aborted
                    ShowLaunchCompletionNotification(MinecraftLaunchOutcome.Aborted)
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
                If Not CurrentEx.Message = "$$" Then MyMsgBox(CurrentEx.Message.TrimStart("$"), GetLaunchFailureDisplay().DialogTitle)
                Throw
            ElseIf CurrentEx.InnerException IsNot Nothing Then
                '检查下一级错误
                CurrentEx = CurrentEx.InnerException
                GoTo NextInner
            Else
                '没有特殊处理过的错误信息
                McLaunchLog("错误：" & ex.ToString())
                Dim failureDisplay = GetLaunchFailureDisplay()
                Log(ex,
                    failureDisplay.LogTitle, LogLevel.Msgbox,
                    failureDisplay.DialogTitle)
                Throw
            End If
        End Try
    End Sub

    Private Sub ShowLaunchCompletionNotification(outcome As MinecraftLaunchOutcome)
        Dim notification = MinecraftLaunchShellService.GetCompletionNotification(
            New MinecraftLaunchCompletionRequest(
                If(McInstanceSelected?.Name, ""),
                outcome,
                CurrentLaunchOptions?.SaveBatch IsNot Nothing,
                AbortHint))
        Select Case notification.Kind
            Case MinecraftLaunchNotificationKind.Info
                Hint(notification.Message, HintType.Info)
            Case MinecraftLaunchNotificationKind.Finish
                Hint(notification.Message, HintType.Finish)
        End Select
    End Sub

    Private Function GetLaunchFailureDisplay() As MinecraftLaunchFailureDisplay
        Return MinecraftLaunchShellService.GetFailureDisplay(CurrentLaunchOptions?.SaveBatch IsNot Nothing)
    End Function

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
        Dim precheckResult = MinecraftLaunchPrecheckService.Evaluate(New MinecraftLaunchPrecheckRequest(
            If(McInstanceSelected?.Name, ""),
            If(McInstanceSelected?.PathIndie, ""),
            If(McInstanceSelected?.PathInstance, ""),
            McInstanceSelected IsNot Nothing,
            McInstanceSelected?.State = McInstanceState.Error,
            If(McInstanceSelected?.Desc, ""),
            IsUtf8CodePage(),
            Setup.Get("HintDisableGamePathCheckTip"),
            If(McInstanceSelected Is Nothing, True, McInstanceSelected.PathInstance.IsASCII()),
            CheckResult,
            GetCurrentProfileKind(),
            McInstanceSelected IsNot Nothing AndAlso McInstanceSelected.Info.HasLabyMod,
            If(McInstanceSelected Is Nothing, MinecraftLaunchLoginRequirement.None, CType(Setup.Get("VersionServerLoginRequire", McInstanceSelected), MinecraftLaunchLoginRequirement)),
            If(McInstanceSelected Is Nothing, Nothing, Setup.Get("VersionServerAuthServer", McInstanceSelected)),
            GetSelectedAuthServerBase(),
            ProfileList.Any(Function(x) x.Type = McLoginType.Ms),
            RegionUtils.IsRestrictedFeatAllowed))
        If Not precheckResult.IsSuccess Then Throw New ArgumentException(precheckResult.FailureMessage)
        If precheckResult.Prompts.Count > 0 Then
            ModLaunchPromptShell.RunLaunchPrompt(precheckResult.Prompts(0), CurrentLaunchOptions)
        End If
#If BETA Then
        '求赞助
        If CurrentLaunchOptions?.SaveBatch Is Nothing Then '保存脚本时不提示
            RunInNewThread(
            Sub()
                Dim supportPrompt = MinecraftLaunchShellService.GetSupportPrompt(Setup.Get("SystemLaunchCount"))
                If supportPrompt IsNot Nothing Then ModLaunchPromptShell.RunLaunchPrompt(supportPrompt, CurrentLaunchOptions)
            End Sub, "Donate")
        End If
#End If
        For index = 1 To precheckResult.Prompts.Count - 1
            ModLaunchPromptShell.RunLaunchPrompt(precheckResult.Prompts(index), CurrentLaunchOptions)
        Next
    End Sub

    Private Function GetCurrentProfileKind() As MinecraftLaunchProfileKind
        If SelectedProfile Is Nothing Then Return MinecraftLaunchProfileKind.None
        Select Case SelectedProfile.Type
            Case McLoginType.Legacy
                Return MinecraftLaunchProfileKind.Legacy
            Case McLoginType.Auth
                Return MinecraftLaunchProfileKind.Auth
            Case McLoginType.Ms
                Return MinecraftLaunchProfileKind.Microsoft
            Case Else
                Return MinecraftLaunchProfileKind.None
        End Select
    End Function

    Private Function GetSelectedAuthServerBase() As String
        If SelectedProfile Is Nothing OrElse SelectedProfile.Type <> McLoginType.Auth Then Return Nothing
        Return SelectedProfile.Server.BeforeLast("/authserver")
    End Function

    Private Function GetSelectedProfileIndex() As Integer?
        If SelectedProfile Is Nothing Then Return Nothing
        Dim index = ProfileList.IndexOf(SelectedProfile)
        Return If(index >= 0, index, Nothing)
    End Function

    Private Function GetStoredProfiles() As List(Of MinecraftLaunchStoredProfile)
        Return ProfileList.Select(AddressOf ConvertToStoredProfile).ToList()
    End Function

    Private Function ConvertToStoredProfile(profile As McProfile) As MinecraftLaunchStoredProfile
        Dim kind = MinecraftLaunchStoredProfileKind.Offline
        Select Case profile.Type
            Case McLoginType.Auth
                kind = MinecraftLaunchStoredProfileKind.Authlib
            Case McLoginType.Ms
                kind = MinecraftLaunchStoredProfileKind.Microsoft
        End Select

        Return New MinecraftLaunchStoredProfile(
            kind,
            profile.Uuid,
            profile.Username,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.Name,
            profile.Password,
            profile.ClientToken,
            profile.RawJson)
    End Function

    Private Function CreateMicrosoftLoginResult(accessToken As String, userName As String, uuid As String, profileJson As String) As McLoginResult
        Return New McLoginResult With {
            .AccessToken = accessToken,
            .Name = userName,
            .Uuid = uuid,
            .Type = "Microsoft",
            .ClientToken = uuid,
            .ProfileJson = profileJson}
    End Function

    Private Function CreateMicrosoftLoginResultFromStored(profile As MinecraftLaunchStoredProfile) As McLoginResult
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
        Return CreateMicrosoftLoginResult(
            If(profile.AccessToken, ""),
            profile.Username,
            profile.Uuid,
            If(profile.RawJson, ""))
    End Function

    Private Function CreateCurrentMicrosoftLoginResult(input As McLoginMs) As McLoginResult
        If SelectedProfile IsNot Nothing AndAlso SelectedProfile.Type = McLoginType.Ms Then
            Return CreateMicrosoftLoginResultFromStored(ConvertToStoredProfile(SelectedProfile))
        End If

        If String.IsNullOrWhiteSpace(input?.AccessToken) OrElse
           String.IsNullOrWhiteSpace(input?.UserName) OrElse
           String.IsNullOrWhiteSpace(input?.Uuid) Then
            Throw New InvalidOperationException("当前没有可继续使用的正版登录结果。")
        End If

        Return CreateMicrosoftLoginResult(
            If(input?.AccessToken, ""),
            If(input?.UserName, ""),
            If(input?.Uuid, ""),
            If(input?.ProfileJson, ""))
    End Function

    Private Sub ApplyProfileMutationPlan(plan As MinecraftLaunchProfileMutationPlan)
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        If Not String.IsNullOrWhiteSpace(plan.NoticeMessage) Then Hint(plan.NoticeMessage, HintType.Critical)

        Select Case plan.Kind
            Case MinecraftLaunchProfileMutationKind.CreateNew
                Dim createdProfile = CreateProfileFromStored(plan.CreateProfile)
                ProfileList.Add(createdProfile)
                If plan.ShouldSelectCreatedProfile Then SelectedProfile = createdProfile
            Case MinecraftLaunchProfileMutationKind.UpdateSelected, MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate
                If Not plan.TargetProfileIndex.HasValue OrElse plan.TargetProfileIndex.Value < 0 OrElse plan.TargetProfileIndex.Value >= ProfileList.Count Then
                    Throw New InvalidOperationException("无法应用档案变更：目标档案不存在。")
                End If
                UpdateProfileFromStored(ProfileList(plan.TargetProfileIndex.Value), plan.UpdateProfile)
            Case Else
                Throw New InvalidOperationException("未知的档案变更类型。")
        End Select

        If plan.ShouldClearCreatingProfile Then IsCreatingProfile = False
    End Sub

    Private Function CreateProfileFromStored(profile As MinecraftLaunchStoredProfile) As McProfile
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))

        Select Case profile.Kind
            Case MinecraftLaunchStoredProfileKind.Microsoft
                Return New McProfile With {
                    .Type = McLoginType.Ms,
                    .Uuid = profile.Uuid,
                    .Username = profile.Username,
                    .AccessToken = profile.AccessToken,
                    .RefreshToken = profile.RefreshToken,
                    .Expires = 1743779140286,
                    .Desc = "",
                    .RawJson = profile.RawJson
                }
            Case MinecraftLaunchStoredProfileKind.Authlib
                Return New McProfile With {
                    .Type = McLoginType.Auth,
                    .Uuid = profile.Uuid,
                    .Username = profile.Username,
                    .Server = profile.Server,
                    .ServerName = profile.ServerName,
                    .Name = profile.LoginName,
                    .Password = profile.Password,
                    .AccessToken = profile.AccessToken,
                    .ClientToken = profile.ClientToken,
                    .Expires = 1743779140286,
                    .Desc = ""
                }
            Case Else
                Throw New InvalidOperationException("不支持从该档案类型创建变更。")
        End Select
    End Function

    Private Sub UpdateProfileFromStored(targetProfile As McProfile, profile As MinecraftLaunchStoredProfile)
        If targetProfile Is Nothing Then Throw New ArgumentNullException(NameOf(targetProfile))
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))

        targetProfile.Uuid = profile.Uuid
        targetProfile.Username = profile.Username
        If profile.Kind = MinecraftLaunchStoredProfileKind.Microsoft Then
            targetProfile.AccessToken = profile.AccessToken
            targetProfile.RefreshToken = profile.RefreshToken
            If profile.RawJson IsNot Nothing Then targetProfile.RawJson = profile.RawJson
        ElseIf profile.Kind = MinecraftLaunchStoredProfileKind.Authlib Then
            targetProfile.Server = profile.Server
            targetProfile.ServerName = profile.ServerName
            targetProfile.AccessToken = profile.AccessToken
            targetProfile.ClientToken = profile.ClientToken
            targetProfile.Name = profile.LoginName
            targetProfile.Password = profile.Password
        End If
    End Sub

#End Region

#Region "档案验证"

#Region "主模块"

    '登录方式
    Public Enum McLoginType
        Legacy = 1
        Auth = 2
        Ms = 3
    End Enum

    '各个登录方式的对应数据
    Public MustInherit Class McLoginData
        ''' <summary>
        ''' 登录方式。
        ''' </summary>
        Public Type As McLoginType
        Public Overrides Function Equals(obj As Object) As Boolean
            Return obj IsNot Nothing AndAlso obj.GetHashCode() = GetHashCode()
        End Function
    End Class

#Region "第三方验证类型"
    Public Class McLoginServer
        Inherits McLoginData

        ''' <summary>
        ''' 登录用户名。
        ''' </summary>
        Public UserName As String
        ''' <summary>
        ''' 登录密码。
        ''' </summary>
        Public Password As String
        ''' <summary>
        ''' 登录服务器基础地址。
        ''' </summary>
        Public BaseUrl As String
        ''' <summary>
        ''' 登录方式的描述字符串，如 “正版”、“统一通行证”。
        ''' </summary>
        Public Description As String
        ''' <summary>
        ''' 是否在本次登录中强制要求玩家重新选择角色，目前仅对 Authlib-Injector 生效。
        ''' </summary>
        Public ForceReselectProfile As Boolean = False
        ''' <summary>
        ''' 是否已经存在该验证信息，用于判断是否为新增档案。
        ''' </summary>
        Public IsExist As Boolean = False

        Public Sub New(Type As McLoginType)
            Me.Type = Type
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(UserName & Password & BaseUrl & Type) Mod Integer.MaxValue
        End Function

    End Class
#End Region

#Region "正版验证类型"
    Public Class McLoginMs
        Inherits McLoginData

        ''' <summary>
        ''' 缓存的 OAuth RefreshToken。若没有则为空字符串。
        ''' </summary>
        Public OAuthRefreshToken As String = ""
        Public AccessToken As String = ""
        Public Uuid As String = ""
        Public UserName As String = ""
        Public ProfileJson As String = ""

        Public Sub New()
            Type = McLoginType.Ms
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(OAuthRefreshToken & AccessToken & Uuid & UserName & ProfileJson) Mod Integer.MaxValue
        End Function
    End Class
#End Region

#Region "离线验证类型"
    Public Class McLoginLegacy
        Inherits McLoginData
        ''' <summary>
        ''' 登录用户名。
        ''' </summary>
        Public UserName As String
        ''' <summary>
        ''' 皮肤种类。
        ''' </summary>
        Public SkinType As Integer
        ''' <summary>
        ''' 若采用正版皮肤，则为该皮肤名。
        ''' </summary>
        Public SkinName As String
        ''' <summary>
        ''' UUID。
        ''' </summary>
        Public Uuid As String

        Public Sub New()
            Type = McLoginType.Legacy
        End Sub
        Public Overrides Function GetHashCode() As Integer
            Return GetHash(UserName & SkinType & SkinName & Type) Mod Integer.MaxValue
        End Function
    End Class
#End Region

    '登录返回结果
    Public Structure McLoginResult
        Public Name As String
        Public Uuid As String
        Public AccessToken As String
        Public Type As String
        Public ClientToken As String
        ''' <summary>
        ''' 进行微软登录时返回的 profile 信息。
        ''' </summary>
        Public ProfileJson As String
    End Structure

    '登录主模块加载器
    Public McLoginLoader As New LoaderTask(Of McLoginData, McLoginResult)("登录", AddressOf McLoginStart, AddressOf McLoginInput, ThreadPriority.BelowNormal) With {.ReloadTimeout = 1, .ProgressWeight = 15, .Block = False}
    Public Function McLoginInput() As McLoginData
        Dim LoginData As McLoginData = Nothing
        Try
            LoginData = GetLoginData()
        Catch ex As Exception
            Log(ex, "获取登录输入信息失败", LogLevel.Feedback)
        End Try
        Return LoginData
    End Function
    Private Sub McLoginStart(Data As LoaderTask(Of McLoginData, McLoginResult))
        Log("[Profile] 开始加载选定档案")
        '校验登录信息
        Dim CheckResult As String = IsProfileValid()
        If Not CheckResult = "" Then Throw New ArgumentException(CheckResult)
        '获取对应加载器
        Dim Loader As LoaderBase = Nothing
        Select Case Data.Input.Type
            Case McLoginType.Ms
                Loader = McLoginMsLoader
            Case McLoginType.Legacy
                Loader = McLoginLegacyLoader
            Case McLoginType.Auth
                Loader = McLoginAuthLoader
        End Select
        '尝试加载
        Loader.WaitForExit(Data.Input, McLoginLoader, Data.IsForceRestarting)
        Data.Output = CType(Loader, Object).Output
        RunInUi(Sub() FrmLaunchLeft.RefreshPage(False)) '刷新自动填充列表
        Log("[Profile] 选定档案加载完成")
    End Sub

#End Region

    '各个登录方式的主对象与输入构造
    Public McLoginMsLoader As New LoaderTask(Of McLoginMs, McLoginResult)("Loader Login Ms", AddressOf McLoginMsStart) With {.ReloadTimeout = 1}
    Public McLoginLegacyLoader As New LoaderTask(Of McLoginLegacy, McLoginResult)("Loader Login Legacy", AddressOf McLoginLegacyStart)
    Public McLoginAuthLoader As New LoaderTask(Of McLoginServer, McLoginResult)("Loader Login Auth", AddressOf McLoginServerStart) With {.ReloadTimeout = 1000 * 60 * 10}

    '主加载函数，返回所有需要的登录信息
    Private McLoginMsRefreshTime As Long = 0 '上次刷新登录的时间

#Region "正版验证"
    Private Structure MicrosoftOAuthStepResult
        Public Outcome As MinecraftLaunchMicrosoftOAuthRefreshOutcome
        Public AccessToken As String
        Public RefreshToken As String
    End Structure

    Private Structure MicrosoftStringStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public Value As String
    End Structure

    Private Structure MicrosoftXstsStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public XstsToken As String
        Public UserHash As String
    End Structure

    Private Structure MicrosoftProfileStepResult
        Public Outcome As MinecraftLaunchMicrosoftStepOutcome
        Public Uuid As String
        Public UserName As String
        Public ProfileJson As String
    End Structure

    Private Function ShouldIgnoreMicrosoftRefreshFailure(Optional stepLabel As String = Nothing) As Boolean
        Dim isIgnore As Boolean = False
        RunInUiWait(Sub()
                        If Not IsLaunching Then Exit Sub
                        Dim decision = RunAccountDecisionPrompt(MinecraftLaunchAccountWorkflowService.GetMicrosoftRefreshNetworkErrorPrompt(stepLabel))
                        If decision.Decision = MinecraftLaunchAccountDecisionKind.IgnoreAndContinue Then isIgnore = True
                    End Sub)
        Return isIgnore
    End Function

    Private Sub McLoginMsStart(Data As LoaderTask(Of McLoginMs, McLoginResult))
        Dim Input As McLoginMs = Data.Input
        Dim LogUsername As String = Input.UserName
        ProfileLog("验证方式：正版（" & If(String.IsNullOrEmpty(LogUsername), "尚未登录", LogUsername) & "）")
        Dim currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetInitialStep(
            New MinecraftLaunchMicrosoftLoginExecutionRequest(
                MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(
                    New MinecraftLaunchMicrosoftSessionReuseRequest(
                        Data.IsForceRestarting,
                        Input.AccessToken,
                        McLoginMsRefreshTime,
                        TimeUtils.GetTimeTick())),
                Not String.IsNullOrEmpty(Input.OAuthRefreshToken)))
        Dim oAuthAccessToken As String = Nothing
        Dim oAuthRefreshToken As String = Nothing
        Dim xblToken As New MicrosoftStringStepResult
        Dim xstsTokens As New MicrosoftXstsStepResult
        Dim accessToken As New MicrosoftStringStepResult
        Dim profileResult As New MicrosoftProfileStepResult

        Do
            Data.Progress = currentStep.Progress
            If Data.IsAborted Then Throw New ThreadInterruptedException

            Select Case currentStep.Kind
                Case MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession
                    Data.Output = CreateCurrentMicrosoftLoginResult(Input)
                    Exit Do
                Case MinecraftLaunchMicrosoftLoginStepKind.RequestDeviceCodeOAuthTokens
                    Dim oAuthTokens = MsLoginStep1New(Data)
                    oAuthAccessToken = oAuthTokens.AccessToken
                    oAuthRefreshToken = oAuthTokens.RefreshToken
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterDeviceCodeOAuthSuccess()
                Case MinecraftLaunchMicrosoftLoginStepKind.RefreshOAuthTokens
                    Dim oAuthTokens = MsLoginStep1Refresh(Input.OAuthRefreshToken)
                    Dim refreshOutcome = oAuthTokens.Outcome
                    If refreshOutcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded Then
                        oAuthAccessToken = oAuthTokens.AccessToken
                        oAuthRefreshToken = oAuthTokens.RefreshToken
                    End If
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(refreshOutcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetXboxLiveToken
                    xblToken = MsLoginStep2(oAuthAccessToken)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxLiveToken(xblToken.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetXboxSecurityToken
                    xstsTokens = MsLoginStep3(xblToken)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxSecurityToken(xstsTokens.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftAccessToken
                    accessToken = MsLoginStep4(xstsTokens)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftAccessToken(accessToken.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.VerifyOwnership
                    MsLoginStep5(accessToken.Value)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterOwnershipVerification()
                Case MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftProfile
                    profileResult = MsLoginStep6(accessToken.Value)
                    currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftProfile(profileResult.Outcome)
                Case MinecraftLaunchMicrosoftLoginStepKind.ApplyProfileMutation
                    Dim microsoftMutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
                        New MinecraftLaunchMicrosoftProfileMutationRequest(
                            IsCreatingProfile,
                            GetSelectedProfileIndex(),
                            GetStoredProfiles(),
                            profileResult.Uuid,
                            profileResult.UserName,
                            accessToken.Value,
                            oAuthRefreshToken,
                            profileResult.ProfileJson))
                    ApplyProfileMutationPlan(microsoftMutationPlan)
                    If microsoftMutationPlan.Kind <> MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate Then
                        SaveProfile()
                        Data.Output = CreateMicrosoftLoginResult(accessToken.Value, profileResult.UserName, profileResult.Uuid, profileResult.ProfileJson)
                    Else
                        Data.Output = CreateMicrosoftLoginResultFromStored(microsoftMutationPlan.UpdateProfile)
                    End If
                    Exit Do
                Case Else
                    Throw New InvalidOperationException("未知的微软登录执行步骤。")
            End Select
        Loop

        McLoginMsRefreshTime = TimeUtils.GetTimeTick()
        ProfileLog("正版验证完成")
    End Sub
    ''' <summary>
    ''' 正版验证步骤 1：通过设备代码流获取账号信息
    ''' </summary>
    ''' <returns>OAuth 验证完成的返回结果</returns>
    Private Function MsLoginStep1New(Data As LoaderTask(Of McLoginMs, McLoginResult)) As MicrosoftOAuthStepResult
        '参考：https://learn.microsoft.com/zh-cn/entra/identity-platform/v2-oauth2-device-code

        '初始请求
Retry:
        McLaunchLog("开始正版验证 Step 1/6（原始登录）")
        Dim PrepareJson As JObject
        Dim parameters As New Dictionary(Of String, String) From {
            {"client_id", OAuthClientId},
            {"tenant", "/consumers"},
            {"scope", "XboxLive.signin offline_access"}
        }
        Using response = HttpRequest.
            CreatePost("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode").
            WithFormContent(parameters).
            SendAsync().
            GetAwaiter().
            GetResult()

            response.EnsureSuccessStatusCode()
            PrepareJson = GetJson(response.AsString())
        End Using

        McLaunchLog("网页登录地址：" & PrepareJson("verification_uri").ToString)

        Dim promptResult = ModLaunchPromptShell.RunMicrosoftDeviceCodeLoginPrompt(PrepareJson)
        If promptResult.Kind = MicrosoftDeviceCodePromptResultKind.PasswordLoginRequired Then
            Dim decision = RunAccountDecisionPrompt(MinecraftLaunchAccountWorkflowService.GetPasswordLoginPrompt())
            If decision.Decision = MinecraftLaunchAccountDecisionKind.Retry Then
                GoTo Retry
            Else
                Throw New Exception("$$")
            End If
        ElseIf promptResult.Kind = MicrosoftDeviceCodePromptResultKind.Failed Then
            Throw promptResult.Error
        Else
            Return New MicrosoftOAuthStepResult With {
                .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded,
                .AccessToken = promptResult.AccessToken,
                .RefreshToken = promptResult.RefreshToken}
        End If
    End Function
    ''' <summary>
    ''' 正版验证步骤 1，刷新登录：从 OAuth Code 或 OAuth RefreshToken 获取 {OAuth accessToken, OAuth RefreshToken}
    ''' </summary>
    ''' <param name="Code"></param>
    ''' <returns></returns>
    Private Function MsLoginStep1Refresh(Code As String) As MicrosoftOAuthStepResult
        McLaunchLog("开始正版验证 Step 1/6（刷新登录）")
        If String.IsNullOrEmpty(Code) Then Throw New ArgumentException("传入的 Code 为空", NameOf(Code))
        Dim Result As String = Nothing
        Try
            Dim parameters As New Dictionary(Of String, String) From {
                {"client_id", OAuthClientId},
                {"refresh_token", Code},
                {"grant_type", "refresh_token"},
                {"scope", "XboxLive.signin offline_access"}
            }
            Using response = HttpRequest.
                CreatePost("https://login.live.com/oauth20_token.srf").
                WithFormContent(parameters).
                SendAsync().
                GetAwaiter().
                GetResult()

                response.EnsureSuccessStatusCode()
                Result = response.AsString()
            End Using
        Catch ex As ThreadInterruptedException
            Log(ex, "加载线程已终止")
        Catch ex As Exception
            If ex.Message.ContainsF("must sign in again", True) OrElse ex.Message.ContainsF("password expired", True) OrElse
               (ex.Message.Contains("refresh_token") AndAlso ex.Message.Contains("is not valid")) Then '#269
                Return New MicrosoftOAuthStepResult With {
                    .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.RequireRelogin}
            Else
                ProfileLog("正版验证 Step 1/6 获取 OAuth Token 失败：" & ex.ToString())
                If ShouldIgnoreMicrosoftRefreshFailure() Then
                    Return New MicrosoftOAuthStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.IgnoreAndContinue}
                End If
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim AccessToken As String = ResultJson("access_token").ToString
        Dim RefreshToken As String = ResultJson("refresh_token").ToString
        Return New MicrosoftOAuthStepResult With {
            .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded,
            .AccessToken = AccessToken,
            .RefreshToken = RefreshToken}
    End Function


    Private Class XBLTokenRequestData
        Public Class PropertiesData
            Public Property AuthMethod As String
            Public Property SiteName As String
            Public Property RpsTicket As String
        End Class
        Public Property Properties As PropertiesData
        Public Property RelyingParty As String
        Public Property TokenType As String
    End Class

    ''' <summary>
    ''' 正版验证步骤 2：从 OAuth accessToken 获取 XBLToken
    ''' </summary>
    ''' <param name="accessToken">OAuth accessToken</param>
    ''' <returns>XBLToken</returns>
    Private Function MsLoginStep2(accessToken As String) As MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 2/6: 获取 XBLToken")
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))
        Dim requestData As New XBLTokenRequestData With {
            .Properties = New XBLTokenRequestData.PropertiesData With {
                .AuthMethod = "RPS",
                .SiteName = "user.auth.xboxlive.com",
                .RpsTicket = $"d={accessToken}"
            },
            .RelyingParty = "http://auth.xboxlive.com",
            .TokenType = "JWT"
        }
        Dim Result As String = Nothing
        Try
            Using response = HttpRequest.
                CreatePost("https://user.auth.xboxlive.com/user/authenticate").
                WithJsonContent(requestData).
                SendAsync().
                GetAwaiter().
                GetResult()

                response.EnsureSuccessStatusCode()
                Result = response.AsString()
            End Using
        Catch ex As Exception
            ProfileLog("正版验证 Step 2/6 获取 XBLToken 失败：" & ex.ToString())
            If ShouldIgnoreMicrosoftRefreshFailure("Step 2") Then
                Return New MicrosoftStringStepResult With {
                    .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim XBLToken As String = ResultJson("Token").ToString
        Return New MicrosoftStringStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Value = XBLToken}
    End Function


    Private Class XSTSTokenRequestData
        Public Class PropertiesData
            Public Property SandboxId As String
            Public Property UserTokens As List(Of String)
        End Class
        Public Property Properties As PropertiesData
        Public Property RelyingParty As String
        Public Property TokenType As String
    End Class
    ''' <summary>
    ''' 正版验证步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
    ''' </summary>
    ''' <returns>包含 XSTSToken 与 UHS 的字符串组</returns>
    Private Function MsLoginStep3(xblTokenResult As MicrosoftStringStepResult) As MicrosoftXstsStepResult
        ProfileLog("开始正版验证 Step 3/6: 获取 XSTSToken")
        If String.IsNullOrEmpty(xblTokenResult.Value) Then Throw New ArgumentException("XBLToken 为空，无法获取数据", NameOf(xblTokenResult))
        Dim requestData As New XSTSTokenRequestData With {
            .Properties = New XSTSTokenRequestData.PropertiesData With {
                .SandboxId = "RETAIL",
                .UserTokens = {xblTokenResult.Value}.ToList()
            },
            .RelyingParty = "rp://api.minecraftservices.com/",
            .TokenType = "JWT"
        }
        Dim result As String
        Using response = HttpRequest.CreatePost("https://xsts.auth.xboxlive.com/xsts/authorize").
                WithJsonContent(requestData).
                SendAsync().
                GetAwaiter().
                GetResult()

            result = response.AsString()

            If Not response.IsSuccessStatusCode Then
                Dim xstsPrompt = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(result)
                If xstsPrompt IsNot Nothing Then
                    RunAccountDecisionPrompt(xstsPrompt)
                    Throw New Exception("$$")
                Else
                    ProfileLog("正版验证 Step 3/6 获取 XSTSToken 失败：" & response.StatusCode)
                    If ShouldIgnoreMicrosoftRefreshFailure("Step 3") Then
                        Return New MicrosoftXstsStepResult With {
                            .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                    End If
                    response.EnsureSuccessStatusCode()
                End If
            End If
        End Using

        Dim ResultJson As JObject = GetJson(result)
        Dim XSTSToken As String = ResultJson("Token").ToString
        Dim UHS As String = ResultJson("DisplayClaims")("xui")(0)("uhs").ToString
        Return New MicrosoftXstsStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .XstsToken = XSTSToken,
            .UserHash = UHS}
    End Function
    ''' <summary>
    ''' 正版验证步骤 4：从 {XSTSToken, UHS} 获取 Minecraft accessToken
    ''' </summary>
    ''' <param name="Tokens">包含 XSTSToken 与 UHS 的字符串组</param>
    ''' <returns>Minecraft accessToken</returns>
    Private Function MsLoginStep4(tokens As MicrosoftXstsStepResult) As MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 4/6: 获取 Minecraft AccessToken")
        If String.IsNullOrEmpty(tokens.XstsToken) OrElse String.IsNullOrEmpty(tokens.UserHash) Then Throw New ArgumentException("传入的 XSTSToken 或者 UHS 错误", NameOf(tokens))
        Dim requestData As New Dictionary(Of String, String) From {
            {"identityToken", $"XBL3.0 x={tokens.UserHash};{tokens.XstsToken}"}
        }
        Dim Result As String
        Try
            Using response = HttpRequest.
                CreatePost("https://api.minecraftservices.com/authentication/login_with_xbox").
                WithJsonContent(requestData).
                SendAsync().
                GetAwaiter().
                GetResult()

                response.EnsureSuccessStatusCode()
                Result = response.AsString()
            End Using
        Catch ex As HttpRequestException
            Dim Message As String = ex.Message
            If ex.StatusCode.Equals(HttpStatusCode.TooManyRequests) Then
                Log(ex, "正版验证 Step 4 汇报 429")
                Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.Forbidden Then
                Log(ex, "正版验证 Step 4 汇报 403")
                Throw New Exception("$当前 IP 的登录尝试异常。" & vbCrLf & "如果你使用了 VPN 或加速器，请把它们关掉或更换节点后再试！")
            Else
                ProfileLog("正版验证 Step 4/6 获取 MC AccessToken 失败：" & ex.ToString())
                If ShouldIgnoreMicrosoftRefreshFailure("Step 4") Then
                    Return New MicrosoftStringStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                End If
                Throw
            End If
        End Try

        Dim ResultJson As JObject = GetJson(Result)
        Dim AccessToken As String = ResultJson("access_token").ToString()
        If String.IsNullOrWhiteSpace(AccessToken) Then Throw New Exception("获取到的 Minecraft AccessToken 为空，登录流程异常！")
        Return New MicrosoftStringStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Value = AccessToken}
    End Function
    ''' <summary>
    ''' 正版验证步骤 5：验证微软账号是否持有 MC，这也会刷新 XGP
    ''' </summary>
    ''' <param name="accessToken">Minecraft accessToken</param>
    Private Sub MsLoginStep5(accessToken As String)
        ProfileLog("开始正版验证 Step 5/6: 验证账户是否持有 MC")
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))
        Dim result As String = ""
        Try
            Using response = HttpRequest.Create("https://api.minecraftservices.com/entitlements/mcstore").
                WithBearerToken(accessToken).
                SendAsync().
                GetAwaiter().
                GetResult()

                response.EnsureSuccessStatusCode()
                result = response.AsString()
            End Using
            Dim ResultJson As JObject = GetJson(result)
            If Not (ResultJson.ContainsKey("items") AndAlso
                ResultJson("items").Any(Function(x)
                                            Return x("name")?.ToString() = "product_minecraft" OrElse
                                            x("name")?.ToString() = "game_minecraft"
                                        End Function)) Then
                RunAccountDecisionPrompt(MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt())
                Throw New Exception("$$")
            End If
        Catch ex As Exception
            Log(ex, "正版验证 Step 5 异常：" & result)
            Throw
        End Try
    End Sub
    ''' <summary>
    ''' 正版验证步骤 6：从 Minecraft accessToken 获取 {UUID, UserName, ProfileJson}
    ''' </summary>
    ''' <param name="AccessToken">Minecraft accessToken</param>
    ''' <returns>包含 UUID, UserName 和 ProfileJson 的字符串组</returns>
    Private Function MsLoginStep6(AccessToken As String) As MicrosoftProfileStepResult
        ProfileLog("开始正版验证 Step 6/6: 获取玩家 ID 与 UUID 等相关信息")
        If String.IsNullOrEmpty(AccessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(AccessToken))
        Dim Result As String
        Try
            Using response = HttpRequest.
                Create("https://api.minecraftservices.com/minecraft/profile").
                WithBearerToken(AccessToken).
                SendAsync().
                GetAwaiter().
                GetResult()

                response.EnsureSuccessStatusCode()
                Result = response.AsString()
            End Using
        Catch ex As HttpRequestException
            Dim Message As String = ex.Message
            If ex.StatusCode.Equals(HttpStatusCode.TooManyRequests) Then
                Log(ex, "正版验证 Step 6 汇报 429")
                Throw New Exception("$登录尝试太过频繁，请等待几分钟后再试！")
            ElseIf ex.StatusCode = HttpStatusCode.NotFound Then
                Log(ex, "正版验证 Step 6 汇报 404")
                RunInNewThread(
                Sub()
                    RunAccountDecisionPrompt(MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt())
                End Sub, "Login Failed: Create Profile")
                Throw New Exception("$$")
            Else
                ProfileLog("正版验证 Step 6/6 获取玩家档案信息失败：" & ex.ToString())
                If ShouldIgnoreMicrosoftRefreshFailure("Step 6") Then
                    Return New MicrosoftProfileStepResult With {
                        .Outcome = MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue}
                End If
                Throw
            End If
        End Try
        Dim ResultJson As JObject = GetJson(Result)
        Dim UUID As String = ResultJson("id").ToString
        Dim UserName As String = ResultJson("name").ToString
        Return New MicrosoftProfileStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Uuid = UUID,
            .UserName = UserName,
            .ProfileJson = Result}
    End Function
#End Region

#Region "第三方验证"
    Private Structure AuthlibLoginStepResult
        Public LoginResult As McLoginResult
        Public NeedsRefresh As Boolean
    End Structure

    Private Sub McLoginServerStart(Data As LoaderTask(Of McLoginServer, McLoginResult))
        Dim Input As McLoginServer = Data.Input
        ProfileLog("验证方式：" & Input.Description)
        Dim currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetInitialStep(
            New MinecraftLaunchThirdPartyLoginExecutionRequest(
                Data.Input.ForceReselectProfile OrElse IsCreatingProfile))
        Do
            Data.Progress = currentStep.Progress
            Select Case currentStep.Kind
                Case MinecraftLaunchThirdPartyLoginStepKind.ValidateCachedSession
                    Try
                        If Data.IsAborted Then Throw New ThreadInterruptedException
                        Data.Output = McLoginRequestValidate(Data.Input)
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterValidateSuccess()
                    Catch ex As HttpWebException
                        Dim AllMessage = ex.ToString()
                        ProfileLog("验证登录失败：" & AllMessage)
                        If (AllMessage.Contains("超时") OrElse AllMessage.Contains("imeout")) AndAlso Not AllMessage.Contains("403") Then
                            ProfileLog("已触发超时登录失败")
                            Dim failure = MinecraftLaunchThirdPartyLoginWorkflowService.GetValidationTimeoutFailure(ex.InnerHttpException.WebResponse)
                            ModLaunchPromptShell.ShowThirdPartyLoginFailure(failure)
                            Throw New Exception(failure.WrappedExceptionMessage)
                        End If
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterValidateFailure()
                    Catch ex As Exception
                        Dim AllMessage = ex.ToString()
                        ProfileLog("验证登录失败：" & AllMessage)
                        Dim failure = MinecraftLaunchThirdPartyLoginWorkflowService.GetValidationFailure(AllMessage)
                        ModLaunchPromptShell.ShowThirdPartyLoginFailure(failure)
                        Throw
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession
                    Try
                        If Data.IsAborted Then Throw New ThreadInterruptedException
                        Data.Output = McLoginRequestRefresh(Data.Input)
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshSuccess(currentStep.HasRetriedRefresh)
                    Catch ex As Exception
                        ProfileLog("刷新登录失败：" & ex.ToString())
                        Dim failure = MinecraftLaunchThirdPartyLoginWorkflowService.GetRefreshFailure(ex.ToString())
                        ModLaunchPromptShell.ShowThirdPartyLoginFailure(failure)
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshFailure(currentStep.HasRetriedRefresh)
                        If currentStep.Kind = MinecraftLaunchThirdPartyLoginStepKind.Fail Then
                            Throw New Exception(currentStep.FailureMessage, ex)
                        End If
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.Authenticate
                    Try
                        If Data.IsAborted Then Throw New ThreadInterruptedException
                        Dim loginStepResult = McLoginRequestLogin(Data.Input)
                        Data.Output = loginStepResult.LoginResult
                        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterLoginSuccess(loginStepResult.NeedsRefresh)
                        If currentStep.Kind = MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession AndAlso currentStep.HasRetriedRefresh Then
                            ProfileLog("重新进行刷新登录")
                        End If
                    Catch ex As HttpWebException
                        ProfileLog("验证失败：" & ex.ToString())
                        Dim responseText = ex.InnerHttpException.WebResponse
                        Dim failure = MinecraftLaunchThirdPartyLoginWorkflowService.GetLoginHttpFailure(ex.ToString(), responseText)
                        ModLaunchPromptShell.ShowThirdPartyLoginFailure(failure)
                        Throw New Exception(failure.WrappedExceptionMessage)
                    Catch ex As Exception
                        ProfileLog("验证失败：" & ex.ToString())
                        Dim failure = MinecraftLaunchThirdPartyLoginWorkflowService.GetLoginFailure(ex.ToString())
                        ModLaunchPromptShell.ShowThirdPartyLoginFailure(failure)
                        Throw New Exception(failure.WrappedExceptionMessage)
                    End Try
                Case MinecraftLaunchThirdPartyLoginStepKind.Finish
                    Exit Do
                Case Else
                    Throw New InvalidOperationException("未知的第三方登录执行步骤。")
            End Select
        Loop
    End Sub
    'Server 登录：三种验证方式的请求
    Private Function McLoginRequestValidate(input As McLoginServer) As McLoginResult
        ProfileLog("验证登录开始（Validate, Authlib")
        '提前缓存信息，否则如果在登录请求过程中退出登录，设置项目会被清空，导致输出存在空值
        Dim AccessToken As String = ""
        Dim ClientToken As String = ""
        Dim Uuid As String = ""
        Dim Name As String = ""
        If SelectedProfile IsNot Nothing Then
            AccessToken = SelectedProfile.AccessToken
            ClientToken = SelectedProfile.ClientToken
            Uuid = SelectedProfile.Uuid
            Name = SelectedProfile.Username
        End If
        '发送登录请求
        Dim RequestData As New JObject(
            New JProperty("accessToken", AccessToken), New JProperty("clientToken", ClientToken))
        NetRequestRetry(
            Url:=input.BaseUrl & "/validate",
            Method:="POST",
            Data:=RequestData.ToString(0),
            Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
            ContentType:="application/json") '没有返回值的
        '不更改缓存，直接结束
        ProfileLog("验证登录成功（Validate, Authlib")
        Return New McLoginResult With {
            .AccessToken = AccessToken,
            .ClientToken = ClientToken,
            .Uuid = Uuid,
            .Name = Name,
            .Type = "Auth"}
    End Function
    Private Function McLoginRequestRefresh(input As McLoginServer) As McLoginResult
        Dim RefreshInfo As New JObject
        Dim SelectProfile As New JObject From {
            {"name", SelectedProfile.Username},
            {"id", SelectedProfile.Uuid}
        }
        RefreshInfo.Add("selectedProfile", SelectProfile)
        RefreshInfo.Add(New JProperty("accessToken", SelectedProfile.AccessToken))
        RefreshInfo.Add(New JProperty("requestUser", True))
        ProfileLog("刷新登录开始（Refresh, Authlib")
        Dim LoginJson As JObject = GetJson(NetRequestRetry(
               Url:=input.BaseUrl & "/refresh",
               Method:="POST",
               Data:=RefreshInfo.ToString(0),
               Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
               ContentType:="application/json"))
        If LoginJson("selectedProfile") Is Nothing Then Throw New Exception("选择的角色 " & SelectedProfile.Username & " 无效！")

        Dim loginResult = New McLoginResult With {
            .AccessToken = LoginJson("accessToken").ToString,
            .ClientToken = LoginJson("clientToken").ToString,
            .Uuid = LoginJson("selectedProfile")("id").ToString,
            .Name = LoginJson("selectedProfile")("name").ToString,
            .Type = "Auth"}
        '保存缓存
        Dim authRefreshMutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            New MinecraftLaunchAuthProfileMutationRequest(
                True,
                GetSelectedProfileIndex(),
                SelectedProfile.Server,
                SelectedProfile.ServerName,
                loginResult.Uuid,
                loginResult.Name,
                loginResult.AccessToken,
                loginResult.ClientToken,
                input.UserName,
                input.Password))
        ApplyProfileMutationPlan(authRefreshMutationPlan)
        ProfileLog("刷新登录成功（Refresh, Authlib）")
        Return loginResult
    End Function
    Private Function McLoginRequestLogin(input As McLoginServer) As AuthlibLoginStepResult
        Try
            Dim NeedRefresh As Boolean = False
            ProfileLog("登录开始（Login, Authlib）")
            Dim RequestData As New JObject(
                New JProperty("agent", New JObject(New JProperty("name", "Minecraft"), New JProperty("version", 1))),
                New JProperty("username", input.UserName),
                New JProperty("password", input.Password),
                New JProperty("requestUser", True))
            Dim LoginJson As JObject = GetJson(NetRequestRetry(
                Url:=input.BaseUrl & "/authenticate",
                Method:="POST",
                Data:=RequestData.ToString(0),
                Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
                ContentType:="application/json"))
            '检查登录结果
            If LoginJson("availableProfiles").Count = 0 Then
                ' handled below through the core workflow result
            End If
            Dim availableProfiles = LoginJson("availableProfiles").
                Select(Function(profile) New MinecraftLaunchAuthProfileOption(profile("id").ToString, profile("name").ToString)).
                ToList()
            Dim selectionResult = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
                New MinecraftLaunchAuthProfileSelectionRequest(
                    input.ForceReselectProfile,
                    If(SelectedProfile IsNot Nothing, SelectedProfile.Uuid, Nothing),
                    If(LoginJson("selectedProfile") Is Nothing, Nothing, LoginJson("selectedProfile")("id").ToString),
                    availableProfiles))
            If Not String.IsNullOrWhiteSpace(selectionResult.NoticeMessage) Then Hint(selectionResult.NoticeMessage, HintType.Critical)
            If selectionResult.Kind = MinecraftLaunchAuthProfileSelectionKind.Fail Then Throw New Exception(selectionResult.FailureMessage)

            Dim SelectedName As String = selectionResult.SelectedProfileName
            Dim SelectedId As String = selectionResult.SelectedProfileId
            NeedRefresh = selectionResult.NeedsRefresh
            If selectionResult.Kind = MinecraftLaunchAuthProfileSelectionKind.PromptForSelection Then
                ProfileLog("要求玩家选择角色")
                RunInUiWait(
                    Sub()
                        Dim selectedProfile = ModLaunchPromptShell.RunAuthProfileSelectionPrompt(selectionResult.PromptTitle, selectionResult.PromptOptions)
                        SelectedName = selectedProfile.Name
                        SelectedId = selectedProfile.Id
                    End Sub)
                ProfileLog("玩家选择的角色：" & SelectedName)
            ElseIf selectionResult.NeedsRefresh Then
                ProfileLog("根据缓存选择的角色：" & SelectedName)
            End If

            Dim loginResult = New McLoginResult With {
                .AccessToken = LoginJson("accessToken").ToString,
                .ClientToken = LoginJson("clientToken").ToString,
                .Name = SelectedName,
                .Uuid = SelectedId,
                .Type = "Auth"}
            '获取服务器信息
            Dim Response As String = NetGetCodeByRequestRetry(input.BaseUrl.Replace("/authserver", ""), Encoding.UTF8)
            Dim ServerName As String = JObject.Parse(Response)("meta")("serverName").ToString()
            Dim authMutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
                New MinecraftLaunchAuthProfileMutationRequest(
                    input.IsExist,
                    GetSelectedProfileIndex(),
                    input.BaseUrl,
                    ServerName,
                    loginResult.Uuid,
                    loginResult.Name,
                    loginResult.AccessToken,
                    loginResult.ClientToken,
                    input.UserName,
                    input.Password))
            ApplyProfileMutationPlan(authMutationPlan)
            SaveProfile()
            ProfileLog("登录成功（Login, Authlib）")
            Return New AuthlibLoginStepResult With {
                .LoginResult = loginResult,
                .NeedsRefresh = NeedRefresh}
        Catch ex As HttpWebException
            Throw
        Catch ex As Exception
            Dim AllMessage As String = ex.ToString()
            ProfileLog("第三方验证失败: " & ex.ToString())
            If ex.Message.StartsWithF("$") Then
                Throw
            Else
                Throw New Exception("登录失败：" & ex.Message, ex)
            End If
        End Try
    End Function
#End Region

#Region "离线验证"
    Private Sub McLoginLegacyStart(Data As LoaderTask(Of McLoginLegacy, McLoginResult))
        Dim Input As McLoginLegacy = Data.Input
        ProfileLog($"验证方式：离线（{Input.UserName}, {Input.Uuid}）")
        Data.Progress = 0.1
        With Data.Output
            .Name = Input.UserName
            .Uuid = SelectedProfile.Uuid
            .Type = "Legacy"
        End With
        '将结果扩展到所有项目中
        Data.Output.AccessToken = Data.Output.Uuid
        Data.Output.ClientToken = Data.Output.Uuid
    End Sub
#End Region

#End Region

#Region "Java 处理"

    Public McLaunchJavaSelected As JavaEntry = Nothing
    Private Sub McLaunchJava(task As LoaderTask(Of Integer, Integer))
        Dim recommendedCode As Integer =
                If(McInstanceSelected.JsonObject?("javaVersion")?("majorVersion")?.ToObject(Of Integer),
                   If(McInstanceSelected.JsonVersion?("java_version")?.ToObject(Of Integer), 0))
        Dim recommendedComponent As String =
                If(McInstanceSelected.JsonObject?("javaVersion")?("component")?.ToString,
                   McInstanceSelected.JsonVersion?("java_component")?.ToString)
        If recommendedComponent = "" Then recommendedComponent = Nothing
        Dim jsonRequiredMajorVersion As Integer? = Nothing
        If McInstanceSelected.JsonObject("javaVersion") IsNot Nothing Then jsonRequiredMajorVersion = CInt(Val(McInstanceSelected.JsonObject("javaVersion")("majorVersion")))

        Dim javaRequirement = MinecraftLaunchJavaRequirementService.Evaluate(
            New MinecraftLaunchJavaRequirementRequest(
                McInstanceSelected.Info.Valid,
                McInstanceSelected.ReleaseTime,
                If(McInstanceSelected.Info.Valid, McInstanceSelected.Info.Vanilla, Nothing),
                McInstanceSelected.Info.HasOptiFine,
                McInstanceSelected.Info.HasForge,
                If(McInstanceSelected.Info.HasForge, McInstanceSelected.Info.Forge, Nothing),
                McInstanceSelected.Info.HasCleanroom,
                McInstanceSelected.Info.HasFabric,
                McInstanceSelected.Info.HasLiteLoader,
                McInstanceSelected.Info.HasLabyMod,
                jsonRequiredMajorVersion,
                recommendedCode,
                recommendedComponent))
        Dim minVer = javaRequirement.MinimumVersion
        Dim maxVer = javaRequirement.MaximumVersion
        If javaRequirement.RecommendedMajorVersion >= 22 Then McLaunchLog("Mojang 要求至少使用 Java " & javaRequirement.RecommendedMajorVersion)

        SyncLock JavaLock

            '选择 Java
            McLaunchLog("Java 版本需求：最低 " & minVer.ToString & "，最高 " & maxVer.ToString)
            McLaunchJavaSelected = JavaSelect("$$", minVer, maxVer, McInstanceSelected)
            If task.IsAborted Then Return
            If McLaunchJavaSelected IsNot Nothing Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString)
                Return
            End If

            '无合适的 Java
            If task.IsAborted Then Return '中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            McLaunchLog("无合适的 Java，需要确认是否自动下载")
            Dim javaPrompt = MinecraftLaunchJavaPromptService.BuildMissingJavaPrompt(
                New MinecraftLaunchJavaPromptRequest(
                    minVer,
                    maxVer,
                    McInstanceSelected.Info.HasForge,
                    javaRequirement.RecommendedComponent))
            Dim javaDecision = RunJavaPrompt(javaPrompt)
            If javaDecision.Decision <> MinecraftLaunchJavaPromptDecision.Download Then Throw New Exception("$$")
            '开始自动下载
            Dim javaLoader = GetJavaDownloadLoader()
            Try
                javaLoader.Start(javaPrompt.DownloadTarget, IsForceRestart:=True)
                Do While javaLoader.State = LoadState.Loading AndAlso Not task.IsAborted
                    task.Progress = javaLoader.Progress
                    Thread.Sleep(10)
                Loop
            Finally
                javaLoader.Abort() '确保取消时中止 Java 下载
            End Try

            '检查下载结果
            McLaunchJavaSelected = JavaSelect("$$", minVer, maxVer, McInstanceSelected)
            If task.IsAborted Then Return
            If McLaunchJavaSelected IsNot Nothing Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString())
            Else
                Hint("没有可用的 Java，已取消启动！", HintType.Critical)
                Throw New Exception("$$")
            End If

        End SyncLock
    End Sub

#End Region

#Region "启动参数"

    Public Class LaunchArgument
        Private _features As New List(Of String)
        Public Sub New(Minecraft As McInstance)
            Dim curArgu As String = String.Empty
            If Minecraft.IsOldJson Then
                _features = Minecraft.JsonObject("minecraftArguments").ToString.Split(" "c).ToList()
            Else
                For Each item In Minecraft.JsonObject("arguments")("game")
                    If item.Type = JTokenType.String Then
                        _features.Add(item.ToString)
                    ElseIf item.Type = JTokenType.Object Then
                        _features.AddRange(item("value").Select(Function(x) x.ToString))
                    End If
                Next
            End If
        End Sub

        Public Function HasArguments(key As String)
            Return _features.Contains(key)
        End Function
    End Class

    Private McLaunchArgument As String

    ''' <summary>
    ''' 释放 Java Wrapper 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractJavaWrapper() As String
        Dim WrapperPath As String = PathPure & "JavaWrapper.jar"
        Log("[Java] 选定的 Java Wrapper 路径：" & WrapperPath)
        SyncLock ExtractJavaWrapperLock '避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
            Try
                WriteJavaWrapper(WrapperPath)
            Catch ex As Exception
                If File.Exists(WrapperPath) Then
                    '因为未知原因 Java Wrapper 可能变为只读文件（#4243）
                    Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(WrapperPath)
                        WriteJavaWrapper(WrapperPath)
                    Catch ex2 As Exception
                        Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", LogLevel.Developer)
                        WrapperPath = PathPure & "JavaWrapper2.jar"
                        Try
                            WriteJavaWrapper(WrapperPath)
                        Catch ex3 As Exception
                            Throw New FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3)
                        End Try
                    End Try
                Else
                    Throw New FileNotFoundException("释放 Java Wrapper 失败", ex)
                End If
            End Try
        End SyncLock
        Return WrapperPath
    End Function
    Private ExtractJavaWrapperLock As New Object
    Private Sub WriteJavaWrapper(Path As String)
        WriteFile(Path, GetResourceStream("Resources/java-wrapper.jar"))
    End Sub

    ''' <summary>
    ''' 释放 linkd 并返回完整文件路径。
    ''' </summary>
    Public Function ExtractLinkD() As String
        Dim LinkDPath As String = PathPure & "linkd.exe"
        SyncLock ExtractLinkDLock '避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
            Try
                WriteLinkD(LinkDPath)
            Catch ex As Exception
                If File.Exists(LinkDPath) Then
                    Log(ex, "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(LinkDPath)
                        WriteLinkD(LinkDPath)
                    Catch ex2 As Exception
                        Throw New FileNotFoundException("释放 linkd 失败", ex2)
                    End Try
                Else
                    Throw New FileNotFoundException("释放 linkd 失败", ex)
                End If
            End Try
        End SyncLock
        Return LinkDPath
    End Function
    Private ExtractLinkDLock As New Object
    Private Sub WriteLinkD(Path As String)
        WriteFile(Path, GetResourceStream("Resources/linkd.exe"))
    End Sub

    ''' <summary>
    ''' 判断是否使用 RetroWrapper。
    ''' TODO: 在更换为 Drop 比较版本号后可能不准确，需要测试确认。
    ''' </summary>
    Private Function McLaunchNeedsRetroWrapper(Mc As McInstance) As Boolean
        Return (Mc.ReleaseTime >= New Date(2013, 6, 25) AndAlso Mc.Info.Drop = 99) OrElse
            (Mc.Info.Drop < 60 AndAlso Mc.Info.Drop <> 99) AndAlso
            Not Setup.Get("LaunchAdvanceDisableRW") AndAlso
            Not Setup.Get("VersionAdvanceDisableRW", Mc) '<1.6
    End Function


    '主方法，合并 Jvm、Game、Replace 三部分的参数数据
    Private Sub McLaunchArgumentMain(Loader As LoaderTask(Of String, List(Of McLibToken)))
        McLaunchLog("开始获取 Minecraft 启动参数")
        '获取基准字符串与参数信息
        Dim Arguments As String
        If McInstanceSelected.JsonObject("arguments") IsNot Nothing AndAlso McInstanceSelected.JsonObject("arguments")("jvm") IsNot Nothing Then
            McLaunchLog("获取新版 JVM 参数")
            Arguments = McLaunchArgumentsJvmNew(McInstanceSelected)
            McLaunchLog("新版 JVM 参数获取成功：")
            McLaunchLog(Arguments)
        Else
            McLaunchLog("获取旧版 JVM 参数")
            Arguments = McLaunchArgumentsJvmOld(McInstanceSelected)
            McLaunchLog("旧版 JVM 参数获取成功：")
            McLaunchLog(Arguments)
        End If
        If Not String.IsNullOrEmpty(McInstanceSelected.JsonObject("minecraftArguments")) Then '有的实例 JSON 中是空字符串
            McLaunchLog("获取旧版 Game 参数")
            Arguments += " " & McLaunchArgumentsGameOld(McInstanceSelected)
            McLaunchLog("旧版 Game 参数获取成功")
        End If
        If McInstanceSelected.JsonObject("arguments") IsNot Nothing AndAlso McInstanceSelected.JsonObject("arguments")("game") IsNot Nothing Then
            McLaunchLog("获取新版 Game 参数")
            Arguments += " " & McLaunchArgumentsGameNew(McInstanceSelected)
            McLaunchLog("新版 Game 参数获取成功")
        End If
        '编码参数（#4700、#5892、#5909）
        If McLaunchJavaSelected.Installation.MajorVersion > 8 Then
            If Not Arguments.Contains("-Dstdout.encoding=") Then Arguments = "-Dstdout.encoding=UTF-8 " & Arguments
            If Not Arguments.Contains("-Dstderr.encoding=") Then Arguments = "-Dstderr.encoding=UTF-8 " & Arguments
        End If
        If McLaunchJavaSelected.Installation.MajorVersion >= 18 Then
            If Not Arguments.Contains("-Dfile.encoding=") Then Arguments = "-Dfile.encoding=COMPAT " & Arguments
        End If
        'MJSB
        Arguments = Arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=""Windows 10""")
        '全屏
        If Setup.Get("LaunchArgumentWindowType") = 0 Then Arguments += " --fullscreen"
        '由 Option 传入的额外参数
        For Each Arg In CurrentLaunchOptions.ExtraArgs
            Arguments += " " & Arg.Trim
        Next
        '自定义参数
        Dim ArgumentGame As String = Setup.Get("VersionAdvanceGame", instance:=McInstanceSelected)
        Arguments += " " & If(ArgumentGame = "", Setup.Get("LaunchAdvanceGame"), ArgumentGame)
        '替换参数
        Dim ReplaceArguments = McLaunchArgumentsReplace(McInstanceSelected, Loader)
        If String.IsNullOrWhiteSpace(ReplaceArguments("${version_type}")) Then
            '若自定义信息为空，则去掉该部分
            Arguments = Arguments.Replace(" --versionType ${version_type}", "")
            ReplaceArguments("${version_type}") = """"""
        End If
        Dim FinalArguments As String = ""
        For Each Argument In Arguments.Split(" ")
            For Each Entry As KeyValuePair(Of String, String) In ReplaceArguments
                Argument = Argument.Replace(Entry.Key, Entry.Value)
            Next
            If (Argument.Contains(" ") OrElse Argument.Contains(":\")) AndAlso Not Argument.EndsWithF("""") Then Argument = $"""{Argument}"""
            FinalArguments += Argument & " "
        Next
        FinalArguments = FinalArguments.TrimEnd()
        '进存档
        Dim WorldName As String = CurrentLaunchOptions.WorldName
        If WorldName IsNot Nothing Then
            FinalArguments += $" --quickPlaySingleplayer ""{WorldName}"""
        End If
        '进服
        Dim Server As String = If(String.IsNullOrEmpty(CurrentLaunchOptions.ServerIp), Setup.Get("VersionServerEnter", McInstanceSelected), CurrentLaunchOptions.ServerIp)
        If String.IsNullOrWhiteSpace(WorldName) AndAlso Not String.IsNullOrWhiteSpace(Server) Then
            If McInstanceSelected.ReleaseTime > New Date(2023, 4, 4) Then
                'QuickPlay
                FinalArguments += $" --quickPlayMultiplayer ""{Server}"""
            Else
                '老版本
                If Server.Contains(":") Then
                    '包含端口号
                    FinalArguments += " --server " & Server.Split(":")(0) & " --port " & Server.Split(":")(1)
                Else
                    '不包含端口号
                    FinalArguments += " --server " & Server & " --port 25565"
                End If
                If McInstanceSelected.Info.HasOptiFine Then Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintType.Critical)
            End If
        End If
        '输出
        McLaunchLog("Minecraft 启动参数：")
        McLaunchLog(FinalArguments)
        McLaunchArgument = FinalArguments
    End Sub

    'Jvm 部分（第一段）
    Private Function McLaunchArgumentsJvmOld(instance As McInstance) As String
        '存储以空格为间隔的启动参数列表
        Dim DataList As New List(Of String)

        '输出固定参数
        DataList.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump")
        Dim ArgumentJvm As String = Setup.Get("VersionAdvanceJvm", instance:=McInstanceSelected)
        If ArgumentJvm = "" Then ArgumentJvm = Setup.Get("LaunchAdvanceJvm")
        If Not ArgumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true") Then ArgumentJvm += " -Dlog4j2.formatMsgNoLookups=true"
        ArgumentJvm = ArgumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", "") '#3511 的清理
        DataList.Insert(0, ArgumentJvm) '可变 JVM 参数
        DataList.Add("-Xmn" & Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) * 1024 * 0.15) & "m")
        DataList.Add("-Xmx" & Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) * 1024) & "m")
        DataList.Add("""-Djava.library.path=" & GetNativesFolder() & """")
        DataList.Add("-cp ${classpath}") '把支持库添加进启动参数表

        'Authlib-Injector
        If McLoginLoader.Output.Type = "Auth" Then
            If McLaunchJavaSelected.Installation.MajorVersion >= 6 Then DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT") '信任系统根证书（Meloong-Git/#5252）
            Dim Server As String = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "")
            Try
                Dim Response As String = NetGetCodeByRequestRetry(Server, Encoding.UTF8)
                DataList.Insert(0, "-javaagent:""" & PathPure & "authlib-injector.jar""=" & Server &
                              " -Dauthlibinjector.side=client" &
                              " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)))
            Catch ex As HttpWebException
                Throw New Exception($"无法连接到第三方登录服务器（{If(Server, Nothing)}）{vbCrLf}详细信息：" & ex.InnerHttpException.WebResponse, ex)
            Catch ex As Exception
                Throw New Exception($"无法连接到第三方登录服务器（{If(Server, Nothing)}）", ex)
            End Try
        End If
        
        If Config.Instance.UseDebugLof4j2Config.Item(instance.PathIndie) Then
            If McInstanceSelected.ReleaseTime.Year >= 2017 Then
                DataList.Insert(0, "-Dlog4j.configurationFile=""" & LaunchEnvUtils.ExtractDebugLog4j2Config() & """")
            Else 
                DataList.Insert(0, "-Dlog4j.configurationFile=""" & LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() & """")
            End If
        End If
        
        '渲染器
        Dim Renderer = 0
        If Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) <> 0 Then
            Renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) - 1
        Else
            Renderer = Setup.Get("LaunchAdvanceRenderer")
        End If
        Dim MesaLoaderWindowsVersion = "25.3.5"
        Dim MesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & MesaLoaderWindowsVersion & "\Loader.jar"

        If Renderer <> 0 Then
            DataList.Insert(0, "-javaagent:""" & MesaLoaderWindowsTargetFile & """=" & If(Renderer = 1, "llvmpipe", If(Renderer = 2, "d3d12", "zink")))
        End If

        '设置代理
        If Config.Instance.UseProxy.Item(instance.PathIndie) AndAlso Config.Network.HttpProxy.Type.Equals(2) AndAlso Not String.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress) Then
            Try
                Dim ProxyAddress As New Uri(Setup.Get("SystemHttpProxy"))
                DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyHost={ProxyAddress.AbsoluteUri}")
                DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyPort={ProxyAddress.Port}")
            Catch ex As Exception
                Log(ex, "添加代理信息到游戏失败，放弃加入", LogLevel.Hint)
            End Try
        End If
        
        '添加 Java Wrapper 作为主 Jar
        If IsUtf8CodePage() AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso Not Setup.Get("VersionAdvanceDisableJLW", McInstanceSelected) Then
            If McLaunchJavaSelected.Installation.MajorVersion >= 9 Then DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED")
            DataList.Add("-Doolloo.jlw.tmpdir=""" & PathPure.TrimEnd("\") & """")
            DataList.Add("-jar """ & ExtractJavaWrapper() & """")
        End If

        '添加 MainClass
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        Else
            DataList.Add(instance.JsonObject("mainClass"))
        End If

        Return Join(DataList, " ")
    End Function
    Private Function McLaunchArgumentsJvmNew(instance As McInstance) As String
        Dim DataList As New List(Of String)

        '获取 Json 中的 DataList
        Dim currentInstance As McInstance = instance
NextInstance:
        If currentInstance.JsonObject("arguments") IsNot Nothing AndAlso currentInstance.JsonObject("arguments")("jvm") IsNot Nothing Then
            For Each SubJson As JToken In currentInstance.JsonObject("arguments")("jvm")
                If SubJson.Type = JTokenType.String Then
                    '字符串类型
                    DataList.Add(SubJson.ToString)
                Else
                    '非字符串类型
                    If McJsonRuleCheck(SubJson("rules")) Then
                        '满足准则
                        If SubJson("value").Type = JTokenType.String Then
                            DataList.Add(SubJson("value").ToString)
                        Else
                            For Each value As JToken In SubJson("value")
                                DataList.Add(value.ToString)
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If currentInstance.InheritInstanceName <> "" Then
            currentInstance = New McInstance(currentInstance.InheritInstanceName)
            GoTo NextInstance
        End If

        '内存、Log4j 防御参数等
        SecretLaunchJvmArgs(DataList)

        'Authlib-Injector
        If McLoginLoader.Output.Type = "Auth" Then
            If McLaunchJavaSelected.Installation.MajorVersion >= 6 Then DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT") '信任系统根证书（Meloong-Git/#5252）
            Dim Server As String = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "")
            Try
                Dim Response As String = NetGetCodeByRequestRetry(Server, Encoding.UTF8)
                DataList.Insert(0, "-javaagent:""" & PathPure & "authlib-injector.jar""=" & Server &
                              " -Dauthlibinjector.side=client" &
                              " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)))
            Catch ex As Exception
                Throw New Exception("无法连接到第三方登录服务器（" & If(Server, Nothing) & "）", ex)
            End Try
        End If

        If Config.Instance.UseDebugLof4j2Config.Item(instance.PathIndie) Then
            If McInstanceSelected.ReleaseTime.Year >= 2017 Then
                DataList.Insert(0, "-Dlog4j.configurationFile=""" & LaunchEnvUtils.ExtractDebugLog4j2Config() & """")
            Else
                DataList.Insert(0, "-Dlog4j.configurationFile=""" & LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() & """")
            End If
        End If

        '渲染器
        Dim Renderer = 0
        If Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) <> 0 Then
            Renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) - 1
        Else
            Renderer = Setup.Get("LaunchAdvanceRenderer")
        End If
        Dim MesaLoaderWindowsVersion = "25.3.5"
        Dim MesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & MesaLoaderWindowsVersion & "\Loader.jar"

        If Renderer <> 0 Then
            DataList.Insert(0, "-javaagent:""" & MesaLoaderWindowsTargetFile & """=" & If(Renderer = 1, "llvmpipe", If(Renderer = 2, "d3d12", "zink")))
        End If

        '设置代理
        If Config.Instance.UseProxy.Item(instance.PathIndie) AndAlso Config.Network.HttpProxy.Type.Equals(2) AndAlso Not String.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress) Then
            Try
                Dim ProxyAddress As New Uri(Setup.Get("SystemHttpProxy"))
                DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyHost={ProxyAddress.AbsoluteUri}")
                DataList.Add($"-D{If(ProxyAddress.Scheme.ToString.StartsWithF("https:"), "https", "http")}.proxyPort={ProxyAddress.Port}")
            Catch ex As Exception
                Log(ex, "添加代理信息到游戏失败，放弃加入", LogLevel.Hint)
            End Try
        End If
        '添加 RetroWrapper 相关参数
        If McLaunchNeedsRetroWrapper(instance) Then
            'https://github.com/NeRdTheNed/RetroWrapper/wiki/RetroWrapper-flags
            DataList.Add("-Dretrowrapper.doUpdateCheck=false")
        End If
        '添加 Java Wrapper 作为主 Jar
        If IsUtf8CodePage() AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso Not Setup.Get("VersionAdvanceDisableJLW", McInstanceSelected) Then
            If McLaunchJavaSelected.Installation.MajorVersion >= 9 Then DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED")
            DataList.Add("-Doolloo.jlw.tmpdir=""" & PathPure.TrimEnd("\") & """")
            DataList.Add("-jar """ & ExtractJavaWrapper() & """")
        End If


        '将 "-XXX" 与后面 "XXX" 合并到一起
        '如果不合并，会导致 Forge 1.17 启动无效，它有两个 --add-exports，进一步导致其中一个在后面被去重
        Dim DeDuplicateDataList As New List(Of String)
        For i = 0 To DataList.Count - 1
            Dim CurrentEntry As String = DataList(i)
            If DataList(i).StartsWithF("-") Then
                Do While i < DataList.Count - 1
                    If DataList(i + 1).StartsWithF("-") Then
                        Exit Do
                    Else
                        i += 1
                        CurrentEntry += " " + DataList(i)
                    End If
                Loop
            End If
            DeDuplicateDataList.Add(CurrentEntry.Trim.Replace("McEmu= ", "McEmu="))
        Next

        '#3511 的清理
        DeDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M")

        '去重
        Dim Result As String = Join(DeDuplicateDataList.Distinct.ToList, " ")

        '添加 MainClass
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        Else
            Result += " " & instance.JsonObject("mainClass").ToString
        End If

        Return Result
    End Function

    'Game 部分（第二段）
    Private Function McLaunchArgumentsGameOld(Version As McInstance) As String
        Dim DataList As New List(Of String)

        '添加 RetroWrapper 相关参数
        If McLaunchNeedsRetroWrapper(Version) Then
            DataList.Add("--tweakClass com.zero.retrowrapper.RetroTweaker")
        End If

        '本地化 Minecraft 启动信息
        Dim BasicString As String = Version.JsonObject("minecraftArguments").ToString
        If Not BasicString.Contains("--height") Then BasicString += " --height ${resolution_height} --width ${resolution_width}"
        DataList.Add(BasicString)

        Dim Result As String = Join(DataList, " ")

        '特别改变 OptiFineTweaker
        If (Version.Info.HasForge OrElse Version.Info.HasLiteLoader) AndAlso Version.Info.HasOptiFine Then
            '把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            If Result.Contains("--tweakClass optifine.OptiFineForgeTweaker") Then
                Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" & Result)
                Result = Result.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
            End If
            If Result.Contains("--tweakClass optifine.OptiFineTweaker") Then
                Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" & Result)
                Result = Result.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
                Try
                    WriteFile(Version.PathInstance & Version.Name & ".json", ReadFile(Version.PathInstance & Version.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
                Catch ex As Exception
                    Log(ex, "替换 OptiFineForge TweakClass 失败")
                End Try
            End If
        End If

        Return Result
    End Function
    Private Function McLaunchArgumentsGameNew(instance As McInstance) As String
        Dim dataList As New List(Of String)

        '获取 Json 中的 DataList
        Dim currentInstance As McInstance = instance
NextInstance:
        If currentInstance.JsonObject("arguments") IsNot Nothing AndAlso currentInstance.JsonObject("arguments")("game") IsNot Nothing Then
            For Each SubJson As JToken In currentInstance.JsonObject("arguments")("game")
                If SubJson.Type = JTokenType.String Then
                    '字符串类型
                    dataList.Add(SubJson.ToString)
                Else
                    '非字符串类型
                    If McJsonRuleCheck(SubJson("rules")) Then
                        '满足准则
                        If SubJson("value").Type = JTokenType.String Then
                            dataList.Add(SubJson("value").ToString)
                        Else
                            For Each value As JToken In SubJson("value")
                                dataList.Add(value.ToString)
                            Next
                        End If
                    End If
                End If
            Next
        End If
        If currentInstance.InheritInstanceName <> "" Then
            currentInstance = New McInstance(currentInstance.InheritInstanceName)
            GoTo NextInstance
        End If

        '将 "-XXX" 与后面 "XXX" 合并到一起
        '如果不进行合并 Impact 会启动无效，它有两个 --tweakclass
        Dim DeDuplicateDataList As New List(Of String)
        For i = 0 To dataList.Count - 1
            Dim CurrentEntry As String = dataList(i)
            If dataList(i).StartsWithF("-") Then
                Do While i < dataList.Count - 1
                    If dataList(i + 1).StartsWithF("-") Then
                        Exit Do
                    Else
                        i += 1
                        CurrentEntry += " " + dataList(i)
                    End If
                Loop
            End If
            DeDuplicateDataList.Add(CurrentEntry)
        Next
        '去重
        McLaunchArgumentsGameNew = Join(DeDuplicateDataList.Distinct.ToList, " ")

        '特别改变 OptiFineTweaker
        If (instance.Info.HasForge OrElse instance.Info.HasLiteLoader) AndAlso instance.Info.HasOptiFine Then
            '把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            If McLaunchArgumentsGameNew.Contains("--tweakClass optifine.OptiFineForgeTweaker") Then
                Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" & McLaunchArgumentsGameNew)
                McLaunchArgumentsGameNew = McLaunchArgumentsGameNew.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "").Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
            End If
            If McLaunchArgumentsGameNew.Contains("--tweakClass optifine.OptiFineTweaker") Then
                Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" & McLaunchArgumentsGameNew)
                McLaunchArgumentsGameNew = McLaunchArgumentsGameNew.Replace(" --tweakClass optifine.OptiFineTweaker", "").Replace("--tweakClass optifine.OptiFineTweaker ", "") & " --tweakClass optifine.OptiFineForgeTweaker"
                Try
                    WriteFile(instance.PathInstance & instance.Name & ".json", ReadFile(instance.PathInstance & instance.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
                Catch ex As Exception
                    Log(ex, "替换 OptiFineForge TweakClass 失败")
                End Try
            End If
        End If
    End Function

    '替换 Arguments
    Private Function McLaunchArgumentsReplace(instance As McInstance, ByRef loader As LoaderTask(Of String, List(Of McLibToken))) As Dictionary(Of String, String)
        Dim GameArguments As New Dictionary(Of String, String)

        '基础参数
        GameArguments.Add("${classpath_separator}", ";")
        GameArguments.Add("${natives_directory}", ShortenPath(GetNativesFolder()))
        GameArguments.Add("${library_directory}", ShortenPath(McFolderSelected & "libraries"))
        GameArguments.Add("${libraries_directory}", ShortenPath(McFolderSelected & "libraries"))
        GameArguments.Add("${launcher_name}", "PCLCE")
        GameArguments.Add("${launcher_version}", VersionCode)
        GameArguments.Add("${version_name}", instance.Name)
        Dim ArgumentInfo As String = Setup.Get("VersionArgumentInfo", instance:=McInstanceSelected)
        GameArguments.Add("${version_type}", If(ArgumentInfo = "", Setup.Get("LaunchArgumentInfo"), ArgumentInfo))
        GameArguments.Add("${game_directory}", ShortenPath(Left(McInstanceSelected.PathIndie, McInstanceSelected.PathIndie.Count - 1)))
        GameArguments.Add("${assets_root}", ShortenPath(McFolderSelected & "assets"))
        GameArguments.Add("${user_properties}", "{}")
        GameArguments.Add("${auth_player_name}", McLoginLoader.Output.Name)
        GameArguments.Add("${auth_uuid}", McLoginLoader.Output.Uuid)
        GameArguments.Add("${auth_access_token}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${access_token}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${auth_session}", McLoginLoader.Output.AccessToken)
        GameArguments.Add("${user_type}", "msa") '#1221

        '窗口尺寸参数
        Dim GameSize As Size
        Select Case Setup.Get("LaunchArgumentWindowType")
            Case 2 '与启动器尺寸一致
                Dim Result As Size
                RunInUiWait(Sub() Result = New Size(GetPixelSize(FrmMain.PanForm.ActualWidth), GetPixelSize(FrmMain.PanForm.ActualHeight)))
                GameSize = Result
                GameSize.Height -= 29.5 * DPI / 96 '标题栏高度
            Case 3 '自定义
                GameSize = New Size(Math.Max(100, Setup.Get("LaunchArgumentWindowWidth")), Math.Max(100, Setup.Get("LaunchArgumentWindowHeight")))
            Case Else
                GameSize = New Size(854, 480)
        End Select
        If McInstanceSelected.Info.Drop <= 120 AndAlso
            McLaunchJavaSelected.Installation.MajorVersion <= 8 AndAlso McLaunchJavaSelected.Installation.Version.Revision >= 200 AndAlso McLaunchJavaSelected.Installation.Version.Revision <= 321 AndAlso
            Not McInstanceSelected.Info.HasOptiFine AndAlso Not McInstanceSelected.Info.HasForge Then
            '修复 #3463：1.12.2-，JRE 8u200~321 下窗口大小为设置大小的 DPI% 倍
            McLaunchLog($"已应用窗口大小过大修复（{McLaunchJavaSelected.Installation.Version.Revision}）")
            GameSize.Width /= DPI / 96
            GameSize.Height /= DPI / 96
        End If
        GameArguments.Add("${resolution_width}", Math.Round(GameSize.Width))
        GameArguments.Add("${resolution_height}", Math.Round(GameSize.Height))

        'Assets 相关参数
        GameArguments.Add("${game_assets}", ShortenPath(McFolderSelected & "assets\virtual\legacy")) '1.5.2 的 pre-1.6 资源索引应与 legacy 合并
        GameArguments.Add("${assets_index_name}", McAssetsGetIndexName(instance))

        '支持库参数
        Dim LibList As List(Of McLibToken) = McLibListGet(instance, True)
        loader.Output = LibList
        Dim CpStrings As New List(Of String)
        Dim OptiFineCp As String = Nothing

        'RetroWrapper 释放
        If McLaunchNeedsRetroWrapper(instance) Then
            Dim WrapperPath As String = McFolderSelected & "libraries\retrowrapper\RetroWrapper.jar"
            Try
                WriteFile(WrapperPath, GetResourceStream("Resources/retro-wrapper.jar"))
                CpStrings.Add(WrapperPath)
            Catch ex As Exception
                Log(ex, "RetroWrapper 释放失败")
            End Try
        End If

        For Each Library As McLibToken In LibList
            If Library.IsNatives Then Continue For
            If Library.Name IsNot Nothing AndAlso Library.Name.Contains("com.cleanroommc:cleanroom:0.2") Then 'Cleanroom 的主 Jar 必须放在 ClassPath 第一位
                CpStrings.Insert(0, Library.LocalPath)
            End If
            If Library.Name IsNot Nothing AndAlso Library.Name = "optifine:OptiFine" Then
                OptiFineCp = Library.LocalPath
            Else
                CpStrings.Add(Library.LocalPath)
            End If
        Next
        For Each library As String In Config.Instance.ClasspathHead(instance.PathInstance).Split(";") '自定义 Classpath 头部
            If String.IsNullOrWhiteSpace(library) Then Continue For
            CpStrings.Insert(0, library)
        Next
        If OptiFineCp IsNot Nothing Then CpStrings.Insert(CpStrings.Count - 2, OptiFineCp) 'OptiFine 的总是需要放到倒数第二位
        GameArguments.Add("${classpath}", Join(CpStrings.Select(Function(c) ShortenPath(c)), ";"))

        Return GameArguments
    End Function

#End Region

#Region "解压 Natives"

    Private Sub McLaunchNatives(Loader As LoaderTask(Of List(Of McLibToken), Integer))

        '创建文件夹
        Dim Target As String = GetNativesFolder() & "\"
        Directory.CreateDirectory(Target)

        '解压文件
        McLaunchLog("正在解压 Natives 文件")
        Dim ExistFiles As New List(Of String)
        For Each Native As McLibToken In Loader.Input
            If Not Native.IsNatives Then Continue For
            Dim Zip As ZipArchive
            Try
                Zip = New ZipArchive(New FileStream(Native.LocalPath, FileMode.Open))
            Catch ex As InvalidDataException
                Log(ex, "打开 Natives 文件失败（" & Native.LocalPath & "）")
                File.Delete(Native.LocalPath)
                Throw New Exception("无法打开 Natives 文件（" & Native.LocalPath & "），该文件可能已损坏，请重新尝试启动游戏")
            End Try
            For Each Entry In Zip.Entries
                Dim FileName As String = Entry.FullName
                If FileName.EndsWithF(".dll", True) Then
                    '实际解压文件的步骤
                    Dim FilePath As String = Target & FileName
                    ExistFiles.Add(FilePath)
                    Dim OriginalFile As New FileInfo(FilePath)
                    If OriginalFile.Exists Then
                        If OriginalFile.Length = Entry.Length Then
                            If ModeDebug Then McLaunchLog("无需解压：" & FilePath)
                            Continue For
                        End If
                        '删除原文件
                        Try
                            File.Delete(FilePath)
                        Catch ex As UnauthorizedAccessException
                            McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" & FilePath)
                            McLaunchLog("实际的错误信息：" & ex.ToString())
                            Exit For
                        End Try
                    End If
                    '解压新文件
                    WriteFile(FilePath, Entry.Open)
                    McLaunchLog("已解压：" & FilePath)
                End If
            Next
            If Zip IsNot Nothing Then Zip.Dispose()
        Next

        '删除多余文件
        For Each FileName As String In Directory.GetFiles(Target)
            If ExistFiles.Contains(FileName) Then Continue For
            Try
                McLaunchLog("删除：" & FileName)
                File.Delete(FileName)
            Catch ex As UnauthorizedAccessException
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤")
                McLaunchLog("实际的错误信息：" & ex.ToString())
                Return
            End Try
        Next

    End Sub
    ''' <summary>
    ''' 获取 Natives 文件夹路径，不以 \ 结尾。
    ''' </summary>
    Private Function GetNativesFolder() As String
        Dim Result As String = McInstanceSelected.PathInstance & McInstanceSelected.Name & "-natives"
        If IsGBKEncoding OrElse Result.IsASCII() Then Return Result
        Result = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\bin\natives"
        If Result.IsASCII() Then Return Result
        Return OsDrive & "ProgramData\PCL\natives"
    End Function

#End Region

#Region "启动与前后处理"

    Private Sub McLaunchPrerun()

        '要求 Java 使用高性能显卡
        Dim javaExePath = If(McLaunchJavaSelected.Installation.JavawExePath, McLaunchJavaSelected.Installation.JavaExePath)
        Try
            ProcessInterop.SetGpuPreference(javaExePath, Config.Launch.SetGpuPreference)
        Catch ex As Exception
            Dim failurePlan = MinecraftLaunchGpuPreferenceWorkflowService.BuildFailurePlan(
                New MinecraftLaunchGpuPreferenceFailureRequest(
                    javaExePath,
                    Config.Launch.SetGpuPreference,
                    ProcessInterop.IsAdmin()))
            If failurePlan.ActionKind = MinecraftLaunchGpuPreferenceFailureActionKind.LogDirectFailure Then
                Log(ex, "直接调整显卡设置失败")
            Else
                Log(ex, failurePlan.RetryLogMessage)
                Try
                    If ProcessInterop.StartAsAdmin(failurePlan.AdminRetryArguments).ExitCode = ProcessReturnValues.TaskDone Then
                        McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功")
                    Else
                        Throw New Exception("调整过程中出现异常")
                    End If
                Catch exx As Exception
                    Log(exx, failurePlan.RetryFailureHintMessage, LogLevel.Hint)
                End Try
            End If
        End Try

        '更新 launcher_profiles.json
        Try
            '确保可用
            If Not McLoginLoader.Output.Type = "Microsoft" Then Exit Try
            McFolderLauncherProfilesJsonCreate(McFolderSelected)
            '构建需要替换的 Json 对象
            Dim ReplaceJsonString As String = "
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ & McLoginLoader.Output.Name & """
                    }
                  }
                }
              },
              ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }"
            Dim ReplaceJson As JObject = GetJson(ReplaceJsonString)
            '更新文件
            Dim Profiles As JObject = GetJson(ReadFile(McFolderSelected & "launcher_profiles.json"))
            Profiles.Merge(ReplaceJson)
            WriteFile(McFolderSelected & "launcher_profiles.json", Profiles.ToString, Encoding:=Encoding.GetEncoding("GB18030"))
            McLaunchLog("已更新 launcher_profiles.json")
        Catch ex As Exception
            Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试")
            Try
                File.Delete(McFolderSelected & "launcher_profiles.json")
                McFolderLauncherProfilesJsonCreate(McFolderSelected)
                '构建需要替换的 Json 对象
                Dim ReplaceJsonString As String = "
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ & McLoginLoader.Output.Name.Replace("""", "-") & """,
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ & McLoginLoader.Output.Name & """
                            }
                          }
                        }
                      },
                      ""clientToken"": """ & McLoginLoader.Output.ClientToken & """,
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }"
                Dim ReplaceJson As JObject = GetJson(ReplaceJsonString)
                '更新文件
                Dim Profiles As JObject = GetJson(ReadFile(McFolderSelected & "launcher_profiles.json"))
                Profiles.Merge(ReplaceJson)
                WriteFile(McFolderSelected & "launcher_profiles.json", Profiles.ToString, Encoding:=Encoding.GetEncoding("GB18030"))
                McLaunchLog("已在删除后更新 launcher_profiles.json")
            Catch exx As Exception
                Log(exx, "更新 launcher_profiles.json 失败", LogLevel.Feedback)
            End Try
        End Try

        '更新 options.txt
        Dim primaryOptionsFileAddress As String = McInstanceSelected.PathIndie & "options.txt"
        Dim yosbrOptionsFileAddress As String = McInstanceSelected.PathIndie & "config\yosbr\options.txt"
        Dim optionsPlan = MinecraftLaunchOptionsFileService.BuildPlan(
            New MinecraftLaunchOptionsSyncRequest(
                Config.Tool.AutoChangeLanguage,
                File.Exists(primaryOptionsFileAddress),
                ReadIni(primaryOptionsFileAddress, "lang", "none"),
                File.Exists(yosbrOptionsFileAddress),
                Directory.Exists(McInstanceSelected.PathIndie & "saves"),
                McInstanceSelected.ReleaseTime,
                Setup.Get("LaunchArgumentWindowType")))
        Dim setupFileAddress As String = If(
            optionsPlan.TargetKind = MinecraftLaunchOptionsFileTargetKind.Yosbr,
            yosbrOptionsFileAddress,
            primaryOptionsFileAddress)
        Try
            If optionsPlan.TargetSelectionLogMessage IsNot Nothing Then McLaunchLog(optionsPlan.TargetSelectionLogMessage)
            For Each write In optionsPlan.Writes
                WriteIni(setupFileAddress, write.Key, write.Value)
            Next
            For Each logMessage In optionsPlan.LogMessages
                McLaunchLog(logMessage)
            Next
        Catch ex As Exception
            Log(ex, "更新 options.txt 失败", LogLevel.Hint)
        End Try

    End Sub
    Private Sub McLaunchCustom(Loader As LoaderTask(Of Integer, Integer))

        '获取自定义命令
        Dim CustomCommandGlobal As String = Setup.Get("LaunchAdvanceRun")
        If CustomCommandGlobal <> "" Then CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, True)
        Dim CustomCommandVersion As String = Setup.Get("VersionAdvanceRun", instance:=McInstanceSelected)
        If CustomCommandVersion <> "" Then CustomCommandVersion = ArgumentReplace(CustomCommandVersion, True)

        '输出 bat
        Try
            Dim CmdString As String =
                $"{If(McLaunchJavaSelected.Installation.MajorVersion > 8, "chcp 65001>nul" & vbCrLf, "")}" &
                "@echo off" & vbCrLf &
                $"title 启动 - {McInstanceSelected.Name}" & vbCrLf &
                "echo 游戏正在启动，请稍候。" & vbCrLf &
                $"cd /D ""{ShortenPath(McInstanceSelected.PathIndie)}""" & vbCrLf &
                CustomCommandGlobal & vbCrLf &
                CustomCommandVersion & vbCrLf &
                $"""{McLaunchJavaSelected.Installation.JavaExePath}"" {McLaunchArgument}" & vbCrLf &
                "echo 游戏已退出。" & vbCrLf &
                "pause"
            WriteFile(If(CurrentLaunchOptions.SaveBatch, ExePath & "PCL\LatestLaunch.bat"), FilterAccessToken(CmdString, "F"),
                      Encoding:=If(McLaunchJavaSelected.Installation.MajorVersion > 8, Encoding.UTF8, Encoding.Default))
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then
                McLaunchLog("导出启动脚本完成，强制结束启动过程")
                AbortHint = "导出启动脚本成功！"
                OpenExplorer(CurrentLaunchOptions.SaveBatch)
                Loader.Parent.Abort()
                Return '导出脚本完成
            End If
        Catch ex As Exception
            Log(ex, "输出启动脚本失败")
            If CurrentLaunchOptions.SaveBatch IsNot Nothing Then Throw '直接触发启动失败
        End Try

        '执行自定义命令
        If CustomCommandGlobal <> "" Then
            McLaunchLog("正在执行全局自定义命令：" & CustomCommandGlobal)
            Dim CustomProcess As New Process
            Try
                CustomProcess.StartInfo.FileName = "cmd.exe"
                CustomProcess.StartInfo.Arguments = "/c """ & CustomCommandGlobal & """"
                CustomProcess.StartInfo.WorkingDirectory = ShortenPath(McFolderSelected)
                CustomProcess.StartInfo.UseShellExecute = False
                CustomProcess.StartInfo.CreateNoWindow = True
                CustomProcess.Start()
                If Setup.Get("LaunchAdvanceRunWait") Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsAborted
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Log(ex, "执行全局自定义命令失败", LogLevel.Hint)
            Finally
                If Not CustomProcess.HasExited AndAlso Loader.IsAborted Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
            End Try
        End If
        If CustomCommandVersion <> "" Then
            McLaunchLog("正在执行实例自定义命令：" & CustomCommandVersion)
            Dim CustomProcess As New Process
            Try
                CustomProcess.StartInfo.FileName = "cmd.exe"
                CustomProcess.StartInfo.Arguments = "/c """ & CustomCommandVersion & """"
                CustomProcess.StartInfo.WorkingDirectory = ShortenPath(McFolderSelected)
                CustomProcess.StartInfo.UseShellExecute = False
                CustomProcess.StartInfo.CreateNoWindow = True
                CustomProcess.Start()
                If Setup.Get("VersionAdvanceRunWait", instance:=McInstanceSelected) Then
                    Do Until CustomProcess.HasExited OrElse Loader.IsAborted
                        Thread.Sleep(10)
                    Loop
                End If
            Catch ex As Exception
                Log(ex, "执行实例自定义命令失败", LogLevel.Hint)
            Finally
                If Not CustomProcess.HasExited AndAlso Loader.IsAborted Then
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程") '#1183
                    CustomProcess.Kill()
                End If
            End Try
        End If

    End Sub
    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))
        Dim noJavaw As Boolean = Setup.Get("LaunchAdvanceNoJavaw") AndAlso McLaunchJavaSelected.Installation.JavawExePath IsNot Nothing

        '启动信息
        Dim GameProcess = New Process()
        Dim StartInfo As New ProcessStartInfo(If(noJavaw, McLaunchJavaSelected.Installation.JavaExePath, McLaunchJavaSelected.Installation.JavawExePath))

        '设置环境变量
        Dim Paths As New List(Of String)(StartInfo.EnvironmentVariables("Path").Split(";"))
        Paths.Add(ShortenPath(McLaunchJavaSelected.Installation.JavaFolder))
        StartInfo.EnvironmentVariables("Path") = Join(Paths.Distinct.ToList, ";")
        StartInfo.EnvironmentVariables("appdata") = ShortenPath(McFolderSelected)

        '设置其他参数
        StartInfo.WorkingDirectory = ShortenPath(McInstanceSelected.PathIndie)
        StartInfo.UseShellExecute = False
        StartInfo.RedirectStandardOutput = True
        StartInfo.RedirectStandardError = True
        StartInfo.CreateNoWindow = noJavaw
        StartInfo.Arguments = McLaunchArgument
        GameProcess.StartInfo = StartInfo

        '开始进程
        GameProcess.Start()
        McLaunchLog("已启动游戏进程：" & StartInfo.FileName)
        If Loader.IsAborted Then
            McLaunchLog("由于取消启动，已强制结束游戏进程") '#1631
            GameProcess.Kill()
            Return
        End If
        Loader.Output = GameProcess
        McLaunchProcess = GameProcess
        '进程优先级处理
        Try
            GameProcess.PriorityBoostEnabled = True
            Select Case Setup.Get("LaunchArgumentPriority")
                Case 0 '高
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal
                Case 2 '低
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal
                Case Else '中
            End Select
        Catch ex As Exception
            Log(ex, "设置进程优先级失败", LogLevel.Feedback)
        End Try

    End Sub
    Private Sub McLaunchWait(Loader As LoaderTask(Of Process, Integer))

        '输出信息
        McLaunchLog("")
        McLaunchLog("~ 基础参数 ~")
        McLaunchLog("PCL 版本：" & VersionBaseName & " (" & VersionCode & ")")
        McLaunchLog($"游戏版本：{McInstanceSelected.Info.VanillaName}（{McInstanceSelected.Info.Vanilla}，Drop {McInstanceSelected.Info.Drop}{If(McInstanceSelected.Info.Reliable, "", "，无法完全确定")}）")
        McLaunchLog("资源版本：" & McAssetsGetIndexName(McInstanceSelected))
        McLaunchLog("实例继承：" & If(McInstanceSelected.InheritInstanceName = "", "无", McInstanceSelected.InheritInstanceName))
        McLaunchLog("分配的内存：" & PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) & " GB（" & Math.Round(PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) * 1024) & " MB）")
        McLaunchLog("MC 文件夹：" & McFolderSelected)
        McLaunchLog("实例文件夹：" & McInstanceSelected.PathInstance)
        McLaunchLog("版本隔离：" & (McInstanceSelected.PathIndie = McInstanceSelected.PathInstance))
        McLaunchLog("HMCL 格式：" & McInstanceSelected.IsHmclFormatJson)
        McLaunchLog("Java 信息：" & If(McLaunchJavaSelected IsNot Nothing, McLaunchJavaSelected.ToString, "无可用 Java"))
        'McLaunchLog("环境变量：" & If(McLaunchJavaSelected IsNot Nothing, If(McLaunchJavaSelected.HasEnvironment, "已设置", "未设置"), "未设置"))
        McLaunchLog("Natives 文件夹：" & GetNativesFolder())
        McLaunchLog("")
        McLaunchLog("~ 档案参数 ~")
        McLaunchLog("玩家用户名：" & McLoginLoader.Output.Name)
        McLaunchLog("AccessToken：" & McLoginLoader.Output.AccessToken)
        McLaunchLog("ClientToken：" & McLoginLoader.Output.ClientToken)
        McLaunchLog("UUID：" & McLoginLoader.Output.Uuid)
        McLaunchLog("验证方式：" & McLoginLoader.Output.Type)
        McLaunchLog("")

        '获取窗口标题
        Dim WindowTitle As String = Setup.Get("VersionArgumentTitle", instance:=McInstanceSelected)
        If WindowTitle.IsNullOrEmpty() AndAlso Not Setup.Get("VersionArgumentTitleEmpty", instance:=McInstanceSelected) Then WindowTitle = Setup.Get("LaunchArgumentTitle")
        WindowTitle = ArgumentReplace(WindowTitle, False)

        'JStack 路径
        Dim JStackPath As String = McLaunchJavaSelected.Installation.JavaFolder & "\jstack.exe"

        '初始化等待
        Dim Watcher As New Watcher(Loader, McInstanceSelected, WindowTitle, If(File.Exists(JStackPath), JStackPath, ""), CurrentLaunchOptions.IsTest)
        McLaunchWatcher = Watcher

        '显示实时日志
        If CurrentLaunchOptions.IsTest Then
            If FrmLogLeft Is Nothing Then RunInUiWait(Sub() FrmLogLeft = New PageLogLeft)
            If FrmLogRight Is Nothing Then RunInUiWait(Sub()
                                                           AniControlEnabled += 1
                                                           FrmLogRight = New PageLogRight
                                                           AniControlEnabled -= 1
                                                       End Sub)
            FrmLogLeft.Add(Watcher)
            McLaunchLog("已显示游戏实时日志")
        End If

        '等待
        Do While Watcher.State = Watcher.MinecraftState.Loading
            Thread.Sleep(100)
        Loop
        If Watcher.State = Watcher.MinecraftState.Crashed Then
            Throw New Exception("$$")
        End If

    End Sub
    Private Sub McLaunchEnd()
        McLaunchLog("开始启动结束处理")
        Dim shellPlan = MinecraftLaunchShellService.GetPostLaunchShellPlan(
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

    ''' <summary>
    ''' 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    ''' </summary>
    Private Function ArgumentReplace(text As String, replaceTime As Boolean, Optional escapeHandler As Func(Of String, String) = Nothing) As String
        '预处理
        If text Is Nothing Then Return Nothing
        Dim replacer =
        Function(s As String) As String
            If s Is Nothing Then Return ""
            If EscapeHandler Is Nothing Then Return s
            If s.Contains(":\") Then s = ShortenPath(s)
            Return EscapeHandler(s)
        End Function
        '基础
        text = text.Replace("{pcl_version}", replacer(VersionBaseName))
        text = text.Replace("{pcl_version_code}", replacer(VersionCode))
        text = text.Replace("{pcl_version_branch}", replacer(VersionBranchName))
        text = text.Replace("{identify}", replacer(Identify.LauncherId))
        text = text.Replace("{path}", replacer(Basics.CurrentDirectory))
        text = text.Replace("{path_with_name}", replacer(Basics.ExecutablePath))
        text = text.Replace("{path_temp}", replacer(PathTemp))
        '时间
        If ReplaceTime Then '在窗口标题中，时间会被后续动态替换，所以此时不应该替换
            text = text.Replace("{date}", replacer(Date.Now.ToString("yyyy'/'M'/'d")))
            text = text.Replace("{time}", replacer(Date.Now.ToString("HH':'mm':'ss")))
        End If
        'Minecraft
        text = text.Replace("{java}", replacer(McLaunchJavaSelected?.Installation.JavaFolder))
        text = text.Replace("{minecraft}", replacer(McFolderSelected))
        If McInstanceSelected?.IsLoaded Then
            text = text.Replace("{version_path}", replacer(McInstanceSelected.PathInstance)) : text = text.Replace("{verpath}", replacer(McInstanceSelected.PathInstance))
            text = text.Replace("{version_indie}", replacer(McInstanceSelected.PathIndie)) : text = text.Replace("{verindie}", replacer(McInstanceSelected.PathIndie))
            text = text.Replace("{name}", replacer(McInstanceSelected.Name))
            If {"unknown", "old", "pending"}.Contains(McInstanceSelected.Info.VanillaName.ToLower) Then
                text = text.Replace("{version}", replacer(McInstanceSelected.Name))
            Else
                text = text.Replace("{version}", replacer(McInstanceSelected.Info.VanillaName))
            End If
        Else
            text = text.Replace("{version_path}", replacer(Nothing)) : text = text.Replace("{verpath}", replacer(Nothing))
            text = text.Replace("{version_indie}", replacer(Nothing)) : text = text.Replace("{verindie}", replacer(Nothing))
            text = text.Replace("{name}", replacer(Nothing))
            text = text.Replace("{version}", replacer(Nothing))
        End If
        '登录信息
        If McLoginLoader.State = LoadState.Finished Then
            text = text.Replace("{user}", replacer(McLoginLoader.Output.Name))
            text = text.Replace("{uuid}", replacer(McLoginLoader.Output.Uuid?.ToLower))
            Select Case McLoginLoader.Input.Type
                Case McLoginType.Legacy
                    text = text.Replace("{login}", replacer("离线"))
                Case McLoginType.Ms
                    text = text.Replace("{login}", replacer("正版"))
                Case McLoginType.Auth
                    text = text.Replace("{login}", replacer("Authlib-Injector"))
            End Select
        Else
            text = text.Replace("{user}", replacer(Nothing))
            text = text.Replace("{uuid}", replacer(Nothing))
            text = text.Replace("{login}", replacer(Nothing))
        End If
        Return text
    End Function

#End Region

End Module
