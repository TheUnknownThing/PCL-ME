Public Module ModMainWindowPageNameShell

    Public Function GetPageName(stack As FormMain.PageStackData) As String
        If stack Is Nothing Then Throw New ArgumentNullException(NameOf(stack))

        Select Case stack.Page
            Case FormMain.PageType.InstanceSelect
                Return "实例选择"
            Case FormMain.PageType.TaskManager
                Return "任务管理"
            Case FormMain.PageType.GameLog
                Return "实时日志"
            Case FormMain.PageType.InstanceSetup
                Return "实例设置 - " & If(PageInstanceLeft.Instance Is Nothing, "未知实例", PageInstanceLeft.Instance.Name)
            Case FormMain.PageType.CompDetail
                Return "资源下载 - " & CType(stack.Additional(0), CompProject).TranslatedName
            Case FormMain.PageType.HelpDetail
                Return CType(stack.Additional(0), HelpEntry).Title
            Case FormMain.PageType.VersionSaves
                Return $"存档管理 - {GetFolderNameFromPath(stack.Additional)}"
            Case FormMain.PageType.HomePageMarket
                Return "主页市场"
            Case Else
                Return ""
        End Select
    End Function

    Public Sub RefreshPageName(form As FormMain, stack As FormMain.PageStackData)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        form.LabTitleInner.Text = GetPageName(stack)
    End Sub

End Module
