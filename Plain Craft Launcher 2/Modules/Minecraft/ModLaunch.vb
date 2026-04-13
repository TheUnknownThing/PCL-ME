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

        Dim refreshResult = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(Result)
        Return New MicrosoftOAuthStepResult With {
            .Outcome = MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded,
            .AccessToken = refreshResult.AccessToken,
            .RefreshToken = refreshResult.RefreshToken}
    End Function
    ''' <summary>
    ''' 正版验证步骤 2：从 OAuth accessToken 获取 XBLToken
    ''' </summary>
    ''' <param name="accessToken">OAuth accessToken</param>
    ''' <returns>XBLToken</returns>
    Private Function MsLoginStep2(accessToken As String) As MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 2/6: 获取 XBLToken")
        If String.IsNullOrEmpty(accessToken) Then Throw New ArgumentException("传入的 AccessToken 为空", NameOf(accessToken))
        Dim requestData = MinecraftLaunchMicrosoftProtocolService.BuildXboxLiveTokenRequest(accessToken)
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

        Return New MicrosoftStringStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Value = MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(Result)}
    End Function
    ''' <summary>
    ''' 正版验证步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
    ''' </summary>
    ''' <returns>包含 XSTSToken 与 UHS 的字符串组</returns>
    Private Function MsLoginStep3(xblTokenResult As MicrosoftStringStepResult) As MicrosoftXstsStepResult
        ProfileLog("开始正版验证 Step 3/6: 获取 XSTSToken")
        If String.IsNullOrEmpty(xblTokenResult.Value) Then Throw New ArgumentException("XBLToken 为空，无法获取数据", NameOf(xblTokenResult))
        Dim requestData = MinecraftLaunchMicrosoftProtocolService.BuildXstsTokenRequest(xblTokenResult.Value)
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

        Dim xstsResult = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(result)
        Return New MicrosoftXstsStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .XstsToken = xstsResult.Token,
            .UserHash = xstsResult.UserHash}
    End Function
    ''' <summary>
    ''' 正版验证步骤 4：从 {XSTSToken, UHS} 获取 Minecraft accessToken
    ''' </summary>
    ''' <param name="Tokens">包含 XSTSToken 与 UHS 的字符串组</param>
    ''' <returns>Minecraft accessToken</returns>
    Private Function MsLoginStep4(tokens As MicrosoftXstsStepResult) As MicrosoftStringStepResult
        ProfileLog("开始正版验证 Step 4/6: 获取 Minecraft AccessToken")
        If String.IsNullOrEmpty(tokens.XstsToken) OrElse String.IsNullOrEmpty(tokens.UserHash) Then Throw New ArgumentException("传入的 XSTSToken 或者 UHS 错误", NameOf(tokens))
        Dim requestData = MinecraftLaunchMicrosoftProtocolService.BuildMinecraftAccessTokenRequest(tokens.UserHash, tokens.XstsToken)
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

        Dim AccessToken As String = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(Result)
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
            If Not MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(result) Then
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
        Dim profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(Result)
        Return New MicrosoftProfileStepResult With {
            .Outcome = MinecraftLaunchMicrosoftStepOutcome.Succeeded,
            .Uuid = profileResponse.Uuid,
            .UserName = profileResponse.UserName,
            .ProfileJson = profileResponse.ProfileJson}
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
        NetRequestRetry(
            Url:=input.BaseUrl & "/validate",
            Method:="POST",
            Data:=MinecraftLaunchAuthlibProtocolService.BuildValidateRequestJson(AccessToken, ClientToken),
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
        ProfileLog("刷新登录开始（Refresh, Authlib")
        Dim refreshResponse = MinecraftLaunchAuthlibProtocolService.ParseRefreshResponseJson(NetRequestRetry(
               Url:=input.BaseUrl & "/refresh",
               Method:="POST",
               Data:=MinecraftLaunchAuthlibProtocolService.BuildRefreshRequestJson(
                   SelectedProfile.Username,
                   SelectedProfile.Uuid,
                   SelectedProfile.AccessToken),
               Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
               ContentType:="application/json"))

        Dim loginResult = New McLoginResult With {
            .AccessToken = refreshResponse.AccessToken,
            .ClientToken = refreshResponse.ClientToken,
            .Uuid = refreshResponse.SelectedProfileId,
            .Name = refreshResponse.SelectedProfileName,
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
            Dim authResponse = MinecraftLaunchAuthlibProtocolService.ParseAuthenticateResponseJson(NetRequestRetry(
                Url:=input.BaseUrl & "/authenticate",
                Method:="POST",
                Data:=MinecraftLaunchAuthlibProtocolService.BuildAuthenticateRequestJson(input.UserName, input.Password),
                Headers:=New Dictionary(Of String, String) From {{"Accept-Language", "zh-CN"}},
                ContentType:="application/json"))
            '检查登录结果
            If authResponse.AvailableProfiles.Count = 0 Then
                ' handled below through the core workflow result
            End If
            Dim selectionResult = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
                New MinecraftLaunchAuthProfileSelectionRequest(
                    input.ForceReselectProfile,
                    If(SelectedProfile IsNot Nothing, SelectedProfile.Uuid, Nothing),
                    authResponse.SelectedProfileId,
                    authResponse.AvailableProfiles))
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
                .AccessToken = authResponse.AccessToken,
                .ClientToken = authResponse.ClientToken,
                .Name = SelectedName,
                .Uuid = SelectedId,
                .Type = "Auth"}
            '获取服务器信息
            Dim Response As String = NetGetCodeByRequestRetry(input.BaseUrl.Replace("/authserver", ""), Encoding.UTF8)
            Dim ServerName As String = MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(Response)
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

        Dim javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(
            New MinecraftLaunchJavaWorkflowRequest(
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
        Dim minVer = javaWorkflow.MinimumVersion
        Dim maxVer = javaWorkflow.MaximumVersion
        If javaWorkflow.RecommendedVersionLogMessage IsNot Nothing Then McLaunchLog(javaWorkflow.RecommendedVersionLogMessage)

        SyncLock JavaLock

            '选择 Java
            McLaunchLog(javaWorkflow.RequirementLogMessage)
            McLaunchJavaSelected = JavaSelect("$$", minVer, maxVer, McInstanceSelected)
            If task.IsAborted Then Return
            Dim initialSelection = MinecraftLaunchJavaWorkflowService.ResolveInitialSelection(javaWorkflow, McLaunchJavaSelected IsNot Nothing)
            If initialSelection.ActionKind = MinecraftLaunchJavaSelectionActionKind.UseSelectedJava Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString)
                Return
            End If

            '无合适的 Java
            If task.IsAborted Then Return '中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            McLaunchLog(initialSelection.LogMessage)
            Dim javaPrompt = initialSelection.Prompt
            Dim javaDecision = RunJavaPrompt(javaPrompt)
            Dim promptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(javaPrompt, javaDecision.Decision)
            If promptOutcome.ActionKind <> MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection Then Throw New Exception("$$")
            '开始自动下载
            Dim javaLoader = GetJavaDownloadLoader()
            Try
                javaLoader.Start(promptOutcome.DownloadTarget, IsForceRestart:=True)
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
            Dim postDownloadSelection = MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(javaWorkflow, McLaunchJavaSelected IsNot Nothing)
            If postDownloadSelection.ActionKind = MinecraftLaunchJavaPostDownloadActionKind.UseSelectedJava Then
                McLaunchLog("选择的 Java：" & McLaunchJavaSelected.ToString())
            Else
                Hint(postDownloadSelection.HintMessage, HintType.Critical)
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
        Return MinecraftLaunchRetroWrapperService.ShouldUse(
            New MinecraftLaunchRetroWrapperRequest(
                Mc.ReleaseTime,
                Mc.Info.Drop,
                Setup.Get("LaunchAdvanceDisableRW"),
                Setup.Get("VersionAdvanceDisableRW", Mc)))
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
        Dim ArgumentGame As String = Setup.Get("VersionAdvanceGame", instance:=McInstanceSelected)
        Dim ReplaceArguments = McLaunchArgumentsReplace(McInstanceSelected, Loader)
        Dim WorldName As String = CurrentLaunchOptions.WorldName
        Dim Server As String = If(String.IsNullOrEmpty(CurrentLaunchOptions.ServerIp), Setup.Get("VersionServerEnter", McInstanceSelected), CurrentLaunchOptions.ServerIp)
        Dim argumentPlan = MinecraftLaunchArgumentWorkflowService.BuildPlan(
            New MinecraftLaunchArgumentPlanRequest(
                Arguments,
                McLaunchJavaSelected.Installation.MajorVersion,
                Setup.Get("LaunchArgumentWindowType") = 0,
                CurrentLaunchOptions.ExtraArgs,
                If(ArgumentGame = "", Setup.Get("LaunchAdvanceGame"), ArgumentGame),
                ReplaceArguments,
                WorldName,
                Server,
                McInstanceSelected.ReleaseTime,
                McInstanceSelected.Info.HasOptiFine))
        If argumentPlan.ShouldWarnAboutLegacyServerWithOptiFine Then
            Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", HintType.Critical)
        End If
        '输出
        McLaunchLog("Minecraft 启动参数：")
        McLaunchLog(argumentPlan.FinalArguments)
        McLaunchArgument = argumentPlan.FinalArguments
    End Sub

    'Jvm 部分（第一段）
    Private Function McLaunchArgumentsJvmOld(instance As McInstance) As String
        Dim totalMemory = Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) * 1024)
        Dim youngMemory = Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit) * 1024 * 0.15)
        Dim proxyAddress = TryGetLaunchProxyAddress(instance)

        Return MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
            New MinecraftLaunchLegacyJvmArgumentRequest(
                GetSelectedJvmArgumentOverrides(),
                youngMemory,
                totalMemory,
                GetNativesFolder(),
                McLaunchJavaSelected.Installation.MajorVersion,
                BuildAuthlibInjectorArgument(includeDetailedHttpError:=True),
                GetDebugLog4jConfigurationPath(instance),
                GetRendererAgentArgument(instance),
                If(proxyAddress Is Nothing, Nothing, GetLaunchProxyScheme(proxyAddress)),
                If(proxyAddress Is Nothing, Nothing, proxyAddress.AbsoluteUri),
                If(proxyAddress Is Nothing, CType(Nothing, Integer?), proxyAddress.Port),
                ShouldUseJavaWrapper(),
                PathPure.TrimEnd("\"),
                If(ShouldUseJavaWrapper(), ExtractJavaWrapper(), Nothing),
                GetMainClassOrThrow(instance)))
    End Function
    Private Function McLaunchArgumentsJvmNew(instance As McInstance) As String
        Dim totalMemory = Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected) * 1024)
        Dim youngMemory = Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected) * 1024 * 0.15)
        Dim proxyAddress = TryGetLaunchProxyAddress(instance)

        Return MinecraftLaunchJvmArgumentService.BuildModernArguments(
            New MinecraftLaunchModernJvmArgumentRequest(
                MinecraftLaunchJsonArgumentService.ExtractValues(
                    New MinecraftLaunchJsonArgumentRequest(
                        CollectArgumentSectionJsons(instance, "jvm"),
                        Environment.OSVersion.Version.ToString(),
                        Is32BitSystem)).
                    ToList(),
                GetSelectedJvmArgumentOverrides(),
                CType(Setup.Get("LaunchPreferredIpStack"), JvmPreferredIpStack),
                youngMemory,
                totalMemory,
                McLaunchNeedsRetroWrapper(instance),
                McLaunchJavaSelected.Installation.MajorVersion,
                BuildAuthlibInjectorArgument(includeDetailedHttpError:=False),
                GetDebugLog4jConfigurationPath(instance),
                GetRendererAgentArgument(instance),
                If(proxyAddress Is Nothing, Nothing, GetLaunchProxyScheme(proxyAddress)),
                If(proxyAddress Is Nothing, Nothing, proxyAddress.AbsoluteUri),
                If(proxyAddress Is Nothing, CType(Nothing, Integer?), proxyAddress.Port),
                ShouldUseJavaWrapper(),
                PathPure.TrimEnd("\"),
                If(ShouldUseJavaWrapper(), ExtractJavaWrapper(), Nothing),
                GetMainClassOrThrow(instance)))
    End Function

    'Game 部分（第二段）
    Private Function McLaunchArgumentsGameOld(Version As McInstance) As String
        Dim plan = MinecraftLaunchGameArgumentService.BuildLegacyPlan(
            New MinecraftLaunchLegacyGameArgumentRequest(
                Version.JsonObject("minecraftArguments").ToString(),
                McLaunchNeedsRetroWrapper(Version),
                Version.Info.HasForge OrElse Version.Info.HasLiteLoader,
                Version.Info.HasOptiFine))
        ApplyGameArgumentPlan(plan, Version)
        Return plan.Arguments
    End Function
    Private Function McLaunchArgumentsGameNew(instance As McInstance) As String
        Dim plan = MinecraftLaunchGameArgumentService.BuildModernPlan(
            New MinecraftLaunchModernGameArgumentRequest(
                MinecraftLaunchJsonArgumentService.ExtractValues(
                    New MinecraftLaunchJsonArgumentRequest(
                        CollectArgumentSectionJsons(instance, "game"),
                        Environment.OSVersion.Version.ToString(),
                        Is32BitSystem)).
                    ToList(),
                instance.Info.HasForge OrElse instance.Info.HasLiteLoader,
                instance.Info.HasOptiFine))
        ApplyGameArgumentPlan(plan, instance)
        Return plan.Arguments
    End Function

    '替换 Arguments
    Private Function McLaunchArgumentsReplace(instance As McInstance, ByRef loader As LoaderTask(Of String, List(Of McLibToken))) As Dictionary(Of String, String)
        Dim ArgumentInfo As String = Setup.Get("VersionArgumentInfo", instance:=McInstanceSelected)

        '窗口尺寸参数
        Dim launcherWindowWidth As Double? = Nothing
        Dim launcherWindowHeight As Double? = Nothing
        If Setup.Get("LaunchArgumentWindowType") = 2 Then
            RunInUiWait(
                Sub()
                    launcherWindowWidth = GetPixelSize(FrmMain.PanForm.ActualWidth)
                    launcherWindowHeight = GetPixelSize(FrmMain.PanForm.ActualHeight)
                End Sub)
        End If
        Dim resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(
            New MinecraftLaunchResolutionRequest(
                CInt(Setup.Get("LaunchArgumentWindowType")),
                launcherWindowWidth,
                launcherWindowHeight,
                29.5 * DPI / 96,
                CInt(Setup.Get("LaunchArgumentWindowWidth")),
                CInt(Setup.Get("LaunchArgumentWindowHeight")),
                McInstanceSelected.Info.Drop,
                McLaunchJavaSelected.Installation.MajorVersion,
                McLaunchJavaSelected.Installation.Version.Revision,
                McInstanceSelected.Info.HasOptiFine,
                McInstanceSelected.Info.HasForge,
                DPI / 96))
        If resolutionPlan.LogMessage IsNot Nothing Then McLaunchLog(resolutionPlan.LogMessage)

        '支持库参数
        Dim LibList As List(Of McLibToken) = McLibListGet(instance, True)
        loader.Output = LibList
        Dim retroWrapperPath As String = Nothing

        'RetroWrapper 释放
        If McLaunchNeedsRetroWrapper(instance) Then
            Dim WrapperPath As String = McFolderSelected & "libraries\retrowrapper\RetroWrapper.jar"
            Try
                WriteFile(WrapperPath, GetResourceStream("Resources/retro-wrapper.jar"))
                retroWrapperPath = ShortenPath(WrapperPath)
            Catch ex As Exception
                Log(ex, "RetroWrapper 释放失败")
            End Try
        End If

        Dim classpathPlan = MinecraftLaunchClasspathService.BuildPlan(
            New MinecraftLaunchClasspathRequest(
                LibList.Select(Function(library) New MinecraftLaunchClasspathLibrary(
                    library.Name,
                    ShortenPath(library.LocalPath),
                    library.IsNatives)).ToList(),
                Config.Instance.ClasspathHead(instance.PathInstance).
                    Split(";"c).
                    Where(Function(library) Not String.IsNullOrWhiteSpace(library)).
                    Select(Function(library) ShortenPath(library)).
                    ToList(),
                retroWrapperPath,
                ";"))

        Dim replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(
            New MinecraftLaunchReplacementValueRequest(
                ";",
                ShortenPath(GetNativesFolder()),
                ShortenPath(McFolderSelected & "libraries"),
                ShortenPath(McFolderSelected & "libraries"),
                "PCLCE",
                VersionCode.ToString(),
                instance.Name,
                If(ArgumentInfo = "", Setup.Get("LaunchArgumentInfo"), ArgumentInfo),
                ShortenPath(Left(McInstanceSelected.PathIndie, McInstanceSelected.PathIndie.Count - 1)),
                ShortenPath(McFolderSelected & "assets"),
                "{}",
                If(McLoginLoader.Output.Name, ""),
                If(McLoginLoader.Output.Uuid, ""),
                If(McLoginLoader.Output.AccessToken, ""),
                "msa",
                resolutionPlan.Width,
                resolutionPlan.Height,
                ShortenPath(McFolderSelected & "assets\virtual\legacy"),
                McAssetsGetIndexName(instance),
                classpathPlan.JoinedClasspath))

        Dim GameArguments As New Dictionary(Of String, String)
        For Each Entry In replacementPlan.Values
            GameArguments.Add(Entry.Key, Entry.Value)
        Next

        Return GameArguments
    End Function

    Private Function CollectArgumentSectionJsons(instance As McInstance, sectionName As String) As List(Of String)
        Dim sections As New List(Of String)
        Dim currentInstance As McInstance = instance
        Do
            Dim argumentsToken = currentInstance.JsonObject("arguments")
            Dim sectionToken = If(argumentsToken Is Nothing, Nothing, argumentsToken(sectionName))
            If sectionToken IsNot Nothing Then sections.Add(sectionToken.ToString())
            If currentInstance.InheritInstanceName = "" Then Exit Do
            currentInstance = New McInstance(currentInstance.InheritInstanceName)
        Loop
        Return sections
    End Function

    Private Function GetSelectedJvmArgumentOverrides() As String
        Dim argumentJvm As String = Setup.Get("VersionAdvanceJvm", instance:=McInstanceSelected)
        If argumentJvm = "" Then argumentJvm = Setup.Get("LaunchAdvanceJvm")
        Return argumentJvm
    End Function

    Private Function BuildAuthlibInjectorArgument(includeDetailedHttpError As Boolean) As String
        If McLoginLoader.Output.Type <> "Auth" Then Return Nothing

        Dim server As String = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "")
        Try
            Dim response As String = NetGetCodeByRequestRetry(server, Encoding.UTF8)
            Return "-javaagent:""" & PathPure & "authlib-injector.jar""=" & server &
                   " -Dauthlibinjector.side=client" &
                   " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(response))
        Catch ex As HttpWebException When includeDetailedHttpError
            Throw New Exception($"无法连接到第三方登录服务器（{If(server, Nothing)}）{vbCrLf}详细信息：" & ex.InnerHttpException.WebResponse, ex)
        Catch ex As Exception
            Throw New Exception($"无法连接到第三方登录服务器（{If(server, Nothing)}）", ex)
        End Try
    End Function

    Private Function GetDebugLog4jConfigurationPath(instance As McInstance) As String
        If Not Config.Instance.UseDebugLof4j2Config.Item(instance.PathIndie) Then Return Nothing
        If McInstanceSelected.ReleaseTime.Year >= 2017 Then
            Return LaunchEnvUtils.ExtractDebugLog4j2Config()
        Else
            Return LaunchEnvUtils.ExtractLegacyDebugLog4j2Config()
        End If
    End Function

    Private Function GetRendererAgentArgument(instance As McInstance) As String
        Dim renderer As Integer
        If Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) <> 0 Then
            renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) - 1
        Else
            renderer = Setup.Get("LaunchAdvanceRenderer")
        End If
        If renderer = 0 Then Return Nothing

        Dim mesaLoaderWindowsVersion = "25.3.5"
        Dim mesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & mesaLoaderWindowsVersion & "\Loader.jar"
        Return "-javaagent:""" & mesaLoaderWindowsTargetFile & """=" & If(renderer = 1, "llvmpipe", If(renderer = 2, "d3d12", "zink"))
    End Function

    Private Function TryGetLaunchProxyAddress(instance As McInstance) As Uri
        If Not Config.Instance.UseProxy.Item(instance.PathIndie) OrElse
           Not Config.Network.HttpProxy.Type.Equals(2) OrElse
           String.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress) Then
            Return Nothing
        End If

        Try
            Return New Uri(Setup.Get("SystemHttpProxy"))
        Catch ex As Exception
            Log(ex, "添加代理信息到游戏失败，放弃加入", LogLevel.Hint)
            Return Nothing
        End Try
    End Function

    Private Function GetLaunchProxyScheme(proxyAddress As Uri) As String
        If proxyAddress Is Nothing Then Return Nothing
        Return If(proxyAddress.Scheme.StartsWith("https", StringComparison.OrdinalIgnoreCase), "https", "http")
    End Function

    Private Function ShouldUseJavaWrapper() As Boolean
        Return IsUtf8CodePage() AndAlso
               Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso
               Not Setup.Get("VersionAdvanceDisableJLW", McInstanceSelected)
    End Function

    Private Function GetMainClassOrThrow(instance As McInstance) As String
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        End If
        Return instance.JsonObject("mainClass").ToString()
    End Function

    Private Sub ApplyGameArgumentPlan(plan As MinecraftLaunchGameArgumentPlan, instance As McInstance)
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        For Each logMessage In plan.LogMessages
            Log(logMessage)
        Next
        If plan.ShouldRewriteOptiFineTweakerInJson Then
            Try
                WriteFile(instance.PathInstance & instance.Name & ".json", ReadFile(instance.PathInstance & instance.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
            Catch ex As Exception
                Log(ex, "替换 OptiFineForge TweakClass 失败")
            End Try
        End If
    End Sub

#End Region

#Region "解压 Natives"

    Private Sub McLaunchNatives(Loader As LoaderTask(Of List(Of McLibToken), Integer))
        Dim nativeSyncResult = MinecraftLaunchNativesSyncService.Sync(
            New MinecraftLaunchNativesSyncRequest(
                GetNativesFolder(),
                Loader.Input.
                    Where(Function(native) native.IsNatives).
                    Select(Function(native) native.LocalPath).
                    ToList(),
                ModeDebug))
        For Each logMessage In nativeSyncResult.LogMessages
            McLaunchLog(logMessage)
        Next
    End Sub
    ''' <summary>
    ''' 获取 Natives 文件夹路径，不以 \ 结尾。
    ''' </summary>
    Private Function GetNativesFolder() As String
        Return MinecraftLaunchNativesDirectoryService.ResolvePath(
            New MinecraftLaunchNativesDirectoryRequest(
                McInstanceSelected.PathInstance & McInstanceSelected.Name & "-natives",
                IsGBKEncoding,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\bin\natives",
                OsDrive & "ProgramData\PCL\natives"))
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
        Try
            ProcessInterop.SetGpuPreference(javaExePath, Config.Launch.SetGpuPreference)
        Catch ex As Exception
            Dim failurePlan = MinecraftLaunchPrerunWorkflowService.BuildGpuPreferenceFailurePlan(
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
        UpdateLauncherProfilesJson(McLaunchPrerunPlan.LauncherProfiles)

        '更新 options.txt
        Try
            If McLaunchPrerunPlan.Options.SyncPlan.TargetSelectionLogMessage IsNot Nothing Then McLaunchLog(McLaunchPrerunPlan.Options.SyncPlan.TargetSelectionLogMessage)
            For Each optionWrite In McLaunchPrerunPlan.Options.SyncPlan.Writes
                WriteIni(McLaunchPrerunPlan.Options.TargetFilePath, optionWrite.Key, optionWrite.Value)
            Next
            For Each logMessage In McLaunchPrerunPlan.Options.SyncPlan.LogMessages
                McLaunchLog(logMessage)
            Next
        Catch ex As Exception
            Log(ex, "更新 options.txt 失败", LogLevel.Hint)
        End Try

    End Sub

    Private Sub UpdateLauncherProfilesJson(plan As MinecraftLaunchLauncherProfilesPrerunPlan)
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        If Not plan.ShouldEnsureFileExists OrElse String.IsNullOrWhiteSpace(plan.Path) Then Exit Sub
        If Not plan.Workflow.ShouldWrite Then Exit Sub

        Try
            McFolderLauncherProfilesJsonCreate(McFolderSelected)
            WriteLauncherProfilesJson(plan.Path, plan.Workflow.InitialAttempt)
        Catch ex As Exception
            Log(ex, plan.Workflow.RetryLogMessage)
            Try
                File.Delete(plan.Path)
                McFolderLauncherProfilesJsonCreate(McFolderSelected)
                WriteLauncherProfilesJson(plan.Path, plan.Workflow.RetryAttempt)
            Catch retryEx As Exception
                Log(retryEx, plan.Workflow.FailureLogMessage, LogLevel.Feedback)
            End Try
        End Try
    End Sub

    Private Sub WriteLauncherProfilesJson(launcherProfilesPath As String, attempt As MinecraftLaunchLauncherProfilesWriteAttempt)
        If attempt Is Nothing Then Throw New ArgumentNullException(NameOf(attempt))
        WriteFile(launcherProfilesPath, attempt.UpdatedProfilesJson, Encoding:=Encoding.GetEncoding("GB18030"))
        McLaunchLog(attempt.SuccessLogMessage)
    End Sub

    Private Sub McLaunchCustom(Loader As LoaderTask(Of Integer, Integer))

        '获取自定义命令
        Dim CustomCommandGlobal As String = Setup.Get("LaunchAdvanceRun")
        If CustomCommandGlobal <> "" Then CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, True)
        Dim CustomCommandVersion As String = Setup.Get("VersionAdvanceRun", instance:=McInstanceSelected)
        If CustomCommandVersion <> "" Then CustomCommandVersion = ArgumentReplace(CustomCommandVersion, True)
        McLaunchSessionPlan = MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            New MinecraftLaunchSessionStartWorkflowRequest(
                New MinecraftLaunchCustomCommandWorkflowRequest(
                    New MinecraftLaunchCustomCommandRequest(
                        McLaunchJavaSelected.Installation.MajorVersion,
                        McInstanceSelected.Name,
                        ShortenPath(McInstanceSelected.PathIndie),
                        McLaunchJavaSelected.Installation.JavaExePath,
                        McLaunchArgument,
                        CustomCommandGlobal,
                        Setup.Get("LaunchAdvanceRunWait"),
                        CustomCommandVersion,
                        Setup.Get("VersionAdvanceRunWait", instance:=McInstanceSelected)),
                    ShortenPath(McFolderSelected)),
                New MinecraftLaunchProcessRequest(
                    Setup.Get("LaunchAdvanceNoJavaw"),
                    McLaunchJavaSelected.Installation.JavaExePath,
                    McLaunchJavaSelected.Installation.JavawExePath,
                    ShortenPath(McLaunchJavaSelected.Installation.JavaFolder),
                    Environment.GetEnvironmentVariable("Path"),
                    ShortenPath(McFolderSelected),
                    ShortenPath(McInstanceSelected.PathIndie),
                    McLaunchArgument,
                    Setup.Get("LaunchArgumentPriority")),
                BuildWatcherWorkflowRequest()))

        '输出 bat
        Try
            WriteFile(If(CurrentLaunchOptions.SaveBatch, ExePath & "PCL\LatestLaunch.bat"), FilterAccessToken(McLaunchSessionPlan.CustomCommandPlan.BatchScriptContent, "F"),
                      Encoding:=If(McLaunchSessionPlan.CustomCommandPlan.UseUtf8Encoding, Encoding.UTF8, Encoding.Default))
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
        For Each shellPlan In McLaunchSessionPlan.CustomCommandShellPlans
            ExecuteCustomCommand(shellPlan, Loader)
        Next

    End Sub

    Private Sub ExecuteCustomCommand(shellPlan As MinecraftLaunchCustomCommandShellPlan, Loader As LoaderTask(Of Integer, Integer))
        McLaunchLog(shellPlan.StartLogMessage)
        Dim customProcess As New Process
        Try
            customProcess.StartInfo.FileName = shellPlan.FileName
            customProcess.StartInfo.Arguments = shellPlan.Arguments
            customProcess.StartInfo.WorkingDirectory = shellPlan.WorkingDirectory
            customProcess.StartInfo.UseShellExecute = shellPlan.UseShellExecute
            customProcess.StartInfo.CreateNoWindow = shellPlan.CreateNoWindow
            customProcess.Start()
            If shellPlan.WaitForExit Then
                Do Until customProcess.HasExited OrElse Loader.IsAborted
                    Thread.Sleep(10)
                Loop
            End If
        Catch ex As Exception
            Log(ex, shellPlan.FailureLogMessage, LogLevel.Hint)
        Finally
            If Not customProcess.HasExited AndAlso Loader.IsAborted Then
                McLaunchLog(shellPlan.AbortKillLogMessage) '#1183
                customProcess.Kill()
            End If
        End Try
    End Sub

    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))
        If McLaunchSessionPlan Is Nothing Then Throw New InvalidOperationException("缺少启动会话计划。")
        Dim shellPlan = McLaunchSessionPlan.ProcessShellPlan

        '启动信息
        Dim GameProcess = New Process()
        Dim StartInfo As New ProcessStartInfo(shellPlan.FileName)

        '设置环境变量
        StartInfo.EnvironmentVariables("Path") = shellPlan.PathEnvironmentValue
        StartInfo.EnvironmentVariables("appdata") = shellPlan.AppDataEnvironmentValue

        '设置其他参数
        StartInfo.WorkingDirectory = shellPlan.WorkingDirectory
        StartInfo.UseShellExecute = shellPlan.UseShellExecute
        StartInfo.RedirectStandardOutput = shellPlan.RedirectStandardOutput
        StartInfo.RedirectStandardError = shellPlan.RedirectStandardError
        StartInfo.CreateNoWindow = shellPlan.CreateNoWindow
        StartInfo.Arguments = shellPlan.Arguments
        GameProcess.StartInfo = StartInfo

        '开始进程
        GameProcess.Start()
        McLaunchLog(shellPlan.StartedLogMessage)
        If Loader.IsAborted Then
            McLaunchLog(shellPlan.AbortKillLogMessage) '#1631
            GameProcess.Kill()
            Return
        End If
        Loader.Output = GameProcess
        McLaunchProcess = GameProcess
        '进程优先级处理
        Try
            GameProcess.PriorityBoostEnabled = True
            Select Case shellPlan.PriorityKind
                Case MinecraftLaunchProcessPriorityKind.AboveNormal
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal
                Case MinecraftLaunchProcessPriorityKind.BelowNormal
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal
                Case Else
            End Select
        Catch ex As Exception
            Log(ex, "设置进程优先级失败", LogLevel.Feedback)
        End Try

    End Sub
    Private Sub McLaunchWait(Loader As LoaderTask(Of Process, Integer))

        If McLaunchSessionPlan Is Nothing Then Throw New InvalidOperationException("缺少启动会话计划。")
        For Each logLine In McLaunchSessionPlan.WatcherWorkflowPlan.StartupSummaryLogLines
            McLaunchLog(logLine)
        Next

        '获取窗口标题
        Dim WindowTitle As String = ArgumentReplace(McLaunchSessionPlan.WatcherWorkflowPlan.RawWindowTitleTemplate, False)

        '初始化等待
        Dim Watcher As New Watcher(Loader, McInstanceSelected, WindowTitle, McLaunchSessionPlan.WatcherWorkflowPlan.JstackExecutablePath, McLaunchSessionPlan.WatcherWorkflowPlan.ShouldAttachRealtimeLog)
        McLaunchWatcher = Watcher

        '显示实时日志
        If McLaunchSessionPlan.WatcherWorkflowPlan.ShouldAttachRealtimeLog Then
            If FrmLogLeft Is Nothing Then RunInUiWait(Sub() FrmLogLeft = New PageLogLeft)
            If FrmLogRight Is Nothing Then RunInUiWait(Sub()
                                                           AniControlEnabled += 1
                                                           FrmLogRight = New PageLogRight
                                                           AniControlEnabled -= 1
                                                       End Sub)
            FrmLogLeft.Add(Watcher)
            If McLaunchSessionPlan.WatcherWorkflowPlan.RealtimeLogAttachedMessage IsNot Nothing Then McLaunchLog(McLaunchSessionPlan.WatcherWorkflowPlan.RealtimeLogAttachedMessage)
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
        Dim allocatedRam = PageInstanceSetup.GetRam(McInstanceSelected, Not McLaunchJavaSelected.Installation.Is64Bit)
        Return New MinecraftLaunchWatcherWorkflowRequest(
            New MinecraftLaunchSessionLogRequest(
                VersionBaseName,
                VersionCode,
                McInstanceSelected.Info.VanillaName,
                If(McInstanceSelected.Info.Vanilla?.ToString(), ""),
                McInstanceSelected.Info.Drop,
                McInstanceSelected.Info.Reliable,
                McAssetsGetIndexName(McInstanceSelected),
                McInstanceSelected.InheritInstanceName,
                allocatedRam,
                McFolderSelected,
                McInstanceSelected.PathInstance,
                McInstanceSelected.PathIndie = McInstanceSelected.PathInstance,
                McInstanceSelected.IsHmclFormatJson,
                If(McLaunchJavaSelected IsNot Nothing, McLaunchJavaSelected.ToString(), Nothing),
                GetNativesFolder(),
                McLoginLoader.Output.Name,
                McLoginLoader.Output.AccessToken,
                McLoginLoader.Output.ClientToken,
                McLoginLoader.Output.Uuid,
                McLoginLoader.Output.Type),
            New MinecraftLaunchWatcherRequest(
                Setup.Get("VersionArgumentTitle", instance:=McInstanceSelected),
                Setup.Get("VersionArgumentTitleEmpty", instance:=McInstanceSelected),
                Setup.Get("LaunchArgumentTitle"),
                McLaunchJavaSelected.Installation.JavaFolder,
                File.Exists(McLaunchJavaSelected.Installation.JavaFolder & "\jstack.exe")),
            CurrentLaunchOptions.IsTest)
    End Function

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
