Public Module ModMainWindowFocusShell

    Public Sub HandleActivated(pageCurrent As FormMain.PageType, pageCurrentSub As FormMain.PageSubType)
        If Setup.Get("ToolDownloadClipboard") Then CompClipboard.GetClipboardResource()

        If pageCurrent = FormMain.PageType.InstanceSetup AndAlso pageCurrentSub = FormMain.PageSubType.VersionMod Then
            FrmInstanceMod.ReloadCompFileList()
        ElseIf pageCurrent = FormMain.PageType.InstanceSetup AndAlso pageCurrentSub = FormMain.PageSubType.VersionResourcePack Then
            If FrmInstanceResourcePack IsNot Nothing Then FrmInstanceResourcePack.ReloadCompFileList()
        ElseIf pageCurrent = FormMain.PageType.InstanceSetup AndAlso pageCurrentSub = FormMain.PageSubType.VersionShader Then
            If FrmInstanceShader IsNot Nothing Then FrmInstanceShader.ReloadCompFileList()
        ElseIf pageCurrent = FormMain.PageType.InstanceSetup AndAlso pageCurrentSub = FormMain.PageSubType.VersionSchematic Then
            If FrmInstanceSchematic IsNot Nothing Then FrmInstanceSchematic.ReloadCompFileList()
        ElseIf pageCurrent = FormMain.PageType.InstanceSelect Then
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        ElseIf TypeOf FrmMain.PageRight Is PageInstanceSavesDatapack AndAlso FrmInstanceSavesDatapack IsNot Nothing Then
            FrmInstanceSavesDatapack.ReloadDatapackFileList()
        End If
    End Sub

End Module
