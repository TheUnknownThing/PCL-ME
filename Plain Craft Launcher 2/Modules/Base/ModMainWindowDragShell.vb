Imports System.IO
Imports System.Windows
Imports PCL.Core.Logging

Public Module ModMainWindowDragShell

    Public Sub HandleDrag(e As DragEventArgs)
        Try
            If e.Handled AndAlso (e.Effects <> DragDropEffects.None) Then Return
            e.Handled = True
            Static PrevData As IDataObject, PrevEffects As DragDropEffects
            If e.Data Is PrevData Then
                e.Effects = PrevEffects
                Return
            End If

            e.Effects = DragDropEffects.None
            If e.Data.GetDataPresent(DataFormats.Text) Then
                Dim Str As String = e.Data.GetData(DataFormats.Text)
                If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                    e.Effects = DragDropEffects.Copy
                ElseIf Str.StartsWithF("file:///") Then
                    e.Effects = DragDropEffects.Copy
                End If
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim Files As String() = e.Data.GetData(DataFormats.FileDrop)
                If Files IsNot Nothing AndAlso Files.Length > 0 Then
                    e.Effects = DragDropEffects.Link
                End If
            End If

            PrevData = e.Data
            PrevEffects = e.Effects
            Log("[System] 设置拖放类型：" & GetStringFromEnum(e.Effects))
        Catch ex As Exception
            Log(ex, "处理拖放时出错", LogLevel.Feedback)
        End Try
    End Sub

    Public Sub HandleDrop(e As DragEventArgs)
        Try
            If e.Data.GetDataPresent(DataFormats.Text) Then
                Try
                    Dim Str As String = e.Data.GetData(DataFormats.Text)
                    Log("[System] 接受文本拖拽：" & Str)
                    If Str.StartsWithF("authlib-injector:yggdrasil-server:") Then
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        Dim AuthlibServer As String = Net.WebUtility.UrlDecode(Str.Substring("authlib-injector:yggdrasil-server:".Length))
                        Log("[System] Authlib 拖拽：" & AuthlibServer)
                        If Not String.IsNullOrEmpty(New ValidateHttp().Validate(AuthlibServer)) Then
                            Hint($"输入的 Authlib 验证服务器不符合网址格式（{AuthlibServer}）！", HintType.Critical)
                            Return
                        End If
                        If MyMsgBox($"是否要创建新的第三方验证档案？{vbCrLf}验证服务器地址：{AuthlibServer}", "创建新的第三方验证档案", "确定", "取消") = 2 Then Exit Sub
                        SelectedProfile = Nothing
                        RunInUi(Sub()
                                    PageLoginAuth.DraggedAuthServer = AuthlibServer
                                    FrmLaunchLeft.RefreshPage(True, McLoginType.Auth)
                                End Sub)
                        If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.VersionSetup Then
                            FrmInstanceSetup.Reload()
                        End If
                    ElseIf Str.StartsWithF("file:///") Then
                        Dim FilePath = Net.WebUtility.UrlDecode(Str).Substring("file:///".Length).Replace("/", "\")
                        e.Handled = True
                        e.Effects = DragDropEffects.Copy
                        ProcessFileDrag(New List(Of String) From {FilePath})
                    End If
                Catch ex As Exception
                    Log(ex, "无法接取文本拖拽事件", LogLevel.Developer)
                    Return
                End Try
            ElseIf e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim FilePathRaw = e.Data.GetData(DataFormats.FileDrop)
                If FilePathRaw Is Nothing Then
                    Hint("请将文件解压后再拖入！", HintType.Critical)
                    Return
                End If
                e.Handled = True
                e.Effects = DragDropEffects.Link
                ProcessFileDrag(CType(FilePathRaw, IEnumerable(Of String)))
            End If
        Catch ex As Exception
            Log(ex, "接取拖拽事件失败", LogLevel.Feedback)
        End Try
    End Sub

    Public Sub ProcessFileDrag(filePathList As IEnumerable(Of String))
        RunInNewThread(
        Sub()
            Dim FilePath As String = filePathList.First
            Log("[System] 接受文件拖拽：" & FilePath & If(filePathList.Any, $" 等 {filePathList.Count} 个文件", ""), LogLevel.Developer)
            If Directory.Exists(filePathList.First) AndAlso Not File.Exists(filePathList.First) Then
                Hint("请拖入一个文件，而非文件夹！", HintType.Critical)
                Return
            ElseIf Not File.Exists(filePathList.First) Then
                Hint("拖入的文件不存在：" & filePathList.First, HintType.Critical)
                Return
            End If

            If filePathList.Count > 1 Then
                Dim FirstExtension = filePathList.First.AfterLast(".").ToLower
                Dim AllSameType = filePathList.All(Function(f) f.AfterLast(".").ToLower = FirstExtension)

                If Not (AllSameType AndAlso {"jar", "litemod", "disabled", "old", "litematic", "nbt", "schematic", "schem"}.Contains(FirstExtension)) Then
                    Hint("一次请只拖入相同类型的文件！", HintType.Critical)
                    Return
                End If
            End If

            Dim Extension As String = FilePath.AfterLast(".").ToLower
            If Extension = "xaml" Then
                Log("[System] 文件后缀为 XAML，作为主页加载")
                If File.Exists(ExePath & "PCL\Custom.xaml") Then
                    If MyMsgBox("已存在一个主页文件，是否要将它覆盖？", "覆盖确认", "覆盖", "取消") = 2 Then
                        Return
                    End If
                End If
                CopyFile(FilePath, ExePath & "PCL\Custom.xaml")
                RunInUi(
                Sub()
                    Setup.Set("UiCustomType", 1)
                    FrmLaunchRight.ForceRefresh()
                    Hint("已加载主页自定义文件！", HintType.Finish)
                End Sub)
                Return
            End If

            If PageInstanceCompResource.InstallMods(filePathList) Then Exit Sub

            If {"litematic", "nbt", "schematic", "schem"}.Contains(Extension) Then
                Log($"[System] 文件为 {Extension} 格式，尝试作为原理图安装")
                Dim targetFolderPath As String = Nothing
                If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.VersionSchematic AndAlso
                   FrmInstanceSchematic IsNot Nothing AndAlso TypeOf FrmInstanceSchematic Is PageInstanceCompResource Then
                    targetFolderPath = DirectCast(FrmInstanceSchematic, PageInstanceCompResource).CurrentFolderPath
                End If
                PageInstanceCompResource.InstallCompFiles(filePathList, CompType.Schematic, targetFolderPath)
                Exit Sub
            End If

            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso {"zip"}.Any(Function(i) i = Extension) Then
                Select Case FrmMain.PageCurrentSub
                    Case FormMain.PageSubType.VersionWorld
                        Dim DestFolder = PageInstanceLeft.Instance.PathIndie + "saves\" + GetFileNameWithoutExtentionFromPath(FilePath)
                        If Directory.Exists(DestFolder) Then
                            Hint("发现同名文件夹，无法粘贴：" + DestFolder, HintType.Critical)
                            Exit Sub
                        End If
                        ExtractFile(FilePath, DestFolder)
                        Hint($"已导入 {GetFileNameWithoutExtentionFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceSaves IsNot Nothing Then RunInUi(Sub() FrmInstanceSaves.Reload())
                        Exit Sub
                    Case FormMain.PageSubType.VersionResourcePack
                        Dim DestFile = PageInstanceLeft.Instance.PathIndie + "resourcepacks\" + GetFileNameFromPath(FilePath)
                        If File.Exists(DestFile) Then
                            Hint("已存在同名文件：" + DestFile, HintType.Critical)
                            Exit Sub
                        End If
                        CopyFile(FilePath, DestFile)
                        Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceResourcePack IsNot Nothing Then RunInUi(Sub() FrmInstanceResourcePack.ReloadCompFileList())
                        Exit Sub
                    Case FormMain.PageSubType.VersionShader
                        Dim DestFile = PageInstanceLeft.Instance.PathIndie + "shaderpacks\" + GetFileNameFromPath(FilePath)
                        If File.Exists(DestFile) Then
                            Hint("已存在同名文件：" + DestFile, HintType.Critical)
                            Exit Sub
                        End If
                        CopyFile(FilePath, DestFile)
                        Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                        If FrmInstanceShader IsNot Nothing Then RunInUi(Sub() FrmInstanceShader.ReloadCompFileList())
                        Exit Sub
                End Select
            End If

            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso {"litematic", "nbt", "schematic", "schem"}.Contains(Extension) AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.VersionSchematic Then
                Dim DestFile = PageInstanceLeft.Instance.PathIndie + "schematics\" + GetFileNameFromPath(FilePath)
                If File.Exists(DestFile) Then
                    Hint("已存在同名文件：" + DestFile, HintType.Critical)
                    Exit Sub
                End If
                Directory.CreateDirectory(PageInstanceLeft.Instance.PathIndie + "schematics\")
                CopyFile(FilePath, DestFile)
                Hint($"已导入 {GetFileNameFromPath(FilePath)}", HintType.Finish)
                If FrmInstanceSchematic IsNot Nothing Then RunInUi(Sub() FrmInstanceSchematic.ReloadCompFileList())
                Exit Sub
            End If

            If {"zip", "rar", "mrpack"}.Any(Function(t) t = Extension) Then
                Log("[System] 文件为压缩包，尝试作为整合包安装")
                Try
                    ModpackInstall(FilePath)
                    Return
                Catch ex As CancelledException
                    Return
                Catch ex As Exception
                End Try
            End If
            If {"zip", "rar"}.Any(Function(t) t = Extension) Then
                Log("[System] 文件为压缩包，尝试作为存档分析")
                Try
                    ReadWorld(FilePath)
                    Return
                Catch ex As CancelledException
                    Return
                Catch ex As Exception
                End Try
            End If

            Try
                Log("[System] 尝试进行错误报告分析")
                Dim Analyzer As New CrashAnalyzer(GetUuid())
                Analyzer.Import(FilePath)
                If Not Analyzer.Prepare() Then Exit Try
                Analyzer.Analyze()
                Analyzer.Output(True, New List(Of String))
                Return
            Catch ex As Exception
                Log(ex, "自主错误报告分析失败", LogLevel.Feedback)
            End Try

            Hint("PCL 无法确定应当执行的文件拖拽操作……")
        End Sub, "文件拖拽")
    End Sub

End Module
