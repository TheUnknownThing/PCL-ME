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
        ModLaunchInteractionShell.RunPrecheckPrompts(precheckResult, CurrentLaunchOptions)
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
    Private Sub McLoginMsStart(Data As LoaderTask(Of McLoginMs, McLoginResult))
        Dim Input As McLoginMs = Data.Input
        Dim LogUsername As String = Input.UserName
        ProfileLog("验证方式：正版（" & If(String.IsNullOrEmpty(LogUsername), "尚未登录", LogUsername) & "）")
        ModLaunchMicrosoftLoginShell.RunLogin(
            New ModLaunchMicrosoftLoginShell.MicrosoftLoginExecutionContext With {
                .Data = Data,
                .Input = Input,
                .ShouldReuseCachedLogin = MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(
                    New MinecraftLaunchMicrosoftSessionReuseRequest(
                        Data.IsForceRestarting,
                        Input.AccessToken,
                        McLoginMsRefreshTime,
                        TimeUtils.GetTimeTick())),
                .HasOAuthRefreshToken = Not String.IsNullOrEmpty(Input.OAuthRefreshToken),
                .IsCreatingProfile = IsCreatingProfile,
                .SelectedProfileIndex = GetSelectedProfileIndex(),
                .StoredProfiles = GetStoredProfiles(),
                .CreateCurrentMicrosoftLoginResult = AddressOf CreateCurrentMicrosoftLoginResult,
                .CreateMicrosoftLoginResult = AddressOf CreateMicrosoftLoginResult,
                .CreateMicrosoftLoginResultFromStored = AddressOf CreateMicrosoftLoginResultFromStored,
                .ApplyProfileMutationPlan = AddressOf ApplyProfileMutationPlan,
                .SaveProfile = AddressOf SaveProfile,
                .RequestDeviceCodeOAuthTokens = AddressOf MsLoginStep1New,
                .RefreshOAuthTokens = AddressOf MsLoginStep1Refresh,
                .GetXboxLiveToken = AddressOf MsLoginStep2,
                .GetXboxSecurityToken = AddressOf MsLoginStep3,
                .GetMinecraftAccessToken = AddressOf MsLoginStep4,
                .VerifyOwnership = AddressOf MsLoginStep5,
                .GetMinecraftProfile = AddressOf MsLoginStep6})
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
        Return ModLaunchInteractionShell.RequestMicrosoftDeviceCodeOAuthTokens(Data, OAuthClientId)
    End Function
    ''' <summary>
    ''' 正版验证步骤 1，刷新登录：从 OAuth Code 或 OAuth RefreshToken 获取 {OAuth accessToken, OAuth RefreshToken}
    ''' </summary>
    ''' <param name="Code"></param>
    ''' <returns></returns>
    Private Function MsLoginStep1Refresh(Code As String) As MicrosoftOAuthStepResult
        Return ModLaunchMicrosoftStepShell.RefreshOAuthTokens(Code, OAuthClientId)
    End Function
    ''' <summary>
    ''' 正版验证步骤 2：从 OAuth accessToken 获取 XBLToken
    ''' </summary>
    ''' <param name="accessToken">OAuth accessToken</param>
    ''' <returns>XBLToken</returns>
    Private Function MsLoginStep2(accessToken As String) As MicrosoftStringStepResult
        Return ModLaunchMicrosoftStepShell.GetXboxLiveToken(accessToken)
    End Function
    ''' <summary>
    ''' 正版验证步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
    ''' </summary>
    ''' <returns>包含 XSTSToken 与 UHS 的字符串组</returns>
    Private Function MsLoginStep3(xblTokenResult As MicrosoftStringStepResult) As MicrosoftXstsStepResult
        Return ModLaunchMicrosoftStepShell.GetXboxSecurityToken(xblTokenResult)
    End Function
    ''' <summary>
    ''' 正版验证步骤 4：从 {XSTSToken, UHS} 获取 Minecraft accessToken
    ''' </summary>
    ''' <param name="Tokens">包含 XSTSToken 与 UHS 的字符串组</param>
    ''' <returns>Minecraft accessToken</returns>
    Private Function MsLoginStep4(tokens As MicrosoftXstsStepResult) As MicrosoftStringStepResult
        Return ModLaunchMicrosoftStepShell.GetMinecraftAccessToken(tokens)
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
            ModLaunchInteractionShell.EnsureMicrosoftOwnership(accessToken)
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
        Return ModLaunchMicrosoftStepShell.GetMinecraftProfile(AccessToken)
    End Function
#End Region

#Region "第三方验证"
    Private Sub McLoginServerStart(Data As LoaderTask(Of McLoginServer, McLoginResult))
        Dim Input As McLoginServer = Data.Input
        ProfileLog("验证方式：" & Input.Description)
        ModLaunchThirdPartyLoginShell.RunLogin(
            New ModLaunchThirdPartyLoginShell.ThirdPartyLoginExecutionContext With {
                .Data = Data,
                .IsCreatingProfile = IsCreatingProfile,
                .RunValidate = AddressOf McLoginRequestValidate,
                .RunRefresh = AddressOf McLoginRequestRefresh,
                .RunLogin = AddressOf McLoginRequestLogin})
    End Sub
    'Server 登录：三种验证方式的请求
    Private Function McLoginRequestValidate(input As McLoginServer) As McLoginResult
        Return ModLaunchAuthlibStepShell.ValidateCachedSession(input, SelectedProfile, AddressOf ExecuteAuthlibRequest)
    End Function
    Private Function McLoginRequestRefresh(input As McLoginServer) As McLoginResult
        Return ModLaunchAuthlibStepShell.RefreshCachedSession(
            input,
            SelectedProfile,
            GetSelectedProfileIndex(),
            AddressOf ExecuteAuthlibRequest,
            AddressOf ApplyProfileMutationPlan)
    End Function
    Private Function McLoginRequestLogin(input As McLoginServer) As AuthlibLoginStepResult
        Return ModLaunchAuthlibStepShell.Authenticate(
            input,
            SelectedProfile,
            GetSelectedProfileIndex(),
            AddressOf ExecuteAuthlibRequest,
            AddressOf ExecuteAuthlibMetadataRequest,
            AddressOf ApplyProfileMutationPlan,
            AddressOf SaveProfile)
    End Function

    Private Function ExecuteAuthlibRequest(requestPlan As MinecraftLaunchHttpRequestPlan) As String
        Return ModLaunchAuthlibRequestShell.ExecuteRequest(requestPlan)
    End Function

    Private Function ExecuteAuthlibMetadataRequest(url As String) As String
        Return ModLaunchAuthlibRequestShell.ExecuteMetadataRequest(url)
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
        If javaWorkflow.RecommendedVersionLogMessage IsNot Nothing Then McLaunchLog(javaWorkflow.RecommendedVersionLogMessage)

        McLaunchJavaSelected = ResolveLaunchJavaSelection(task, javaWorkflow, McInstanceSelected)
        If task.IsAborted Then Return
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
                Dim scriptExportPlan = MinecraftLaunchShellService.BuildScriptExportPlan(CurrentLaunchOptions.SaveBatch)
                McLaunchLog(scriptExportPlan.CompletionLogMessage)
                AbortHint = scriptExportPlan.AbortHint
                OpenExplorer(scriptExportPlan.RevealInShellPath)
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
        Dim customProcess As Process = Nothing
        Try
            customProcess = SystemProcessManager.Current.Start(
                MinecraftLaunchProcessExecutionService.BuildCustomCommandStartRequest(shellPlan))
            If customProcess Is Nothing Then Throw New InvalidOperationException("自定义命令进程启动失败。")
            If shellPlan.WaitForExit Then
                Do Until customProcess.HasExited OrElse Loader.IsAborted
                    Thread.Sleep(10)
                Loop
            End If
        Catch ex As Exception
            Log(ex, shellPlan.FailureLogMessage, LogLevel.Hint)
        Finally
            If customProcess IsNot Nothing AndAlso Not customProcess.HasExited AndAlso Loader.IsAborted Then
                McLaunchLog(shellPlan.AbortKillLogMessage) '#1183
                SystemProcessManager.Current.Kill(customProcess)
            End If
        End Try
    End Sub

    Private Sub McLaunchRun(Loader As LoaderTask(Of Integer, Process))
        If McLaunchSessionPlan Is Nothing Then Throw New InvalidOperationException("缺少启动会话计划。")
        Dim shellPlan = McLaunchSessionPlan.ProcessShellPlan

        '启动信息
        Dim GameProcess = SystemProcessManager.Current.Start(
            MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(shellPlan))
        If GameProcess Is Nothing Then Throw New InvalidOperationException("游戏进程启动失败。")

        '开始进程
        McLaunchLog(shellPlan.StartedLogMessage)
        If Loader.IsAborted Then
            McLaunchLog(shellPlan.AbortKillLogMessage) '#1631
            SystemProcessManager.Current.Kill(GameProcess)
            Return
        End If
        Loader.Output = GameProcess
        McLaunchProcess = GameProcess
        '进程优先级处理
        If Not MinecraftLaunchProcessExecutionService.TryApplyPriority(GameProcess, shellPlan.PriorityKind) Then
            Log("设置进程优先级失败", LogLevel.Feedback)
        End If

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
        text = text.Replace("{identify}", replacer(LauncherIdentity.LauncherId))
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
