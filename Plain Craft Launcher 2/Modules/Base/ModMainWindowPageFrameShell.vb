Public Module ModMainWindowPageFrameShell

    Public Class PageFrameTargets
        Public Property Left As MyPageLeft
        Public Property Right As MyPageRight
    End Class

    Public Function ResolvePageTargets(stack As FormMain.PageStackData, subType As FormMain.PageSubType) As PageFrameTargets
        If stack Is Nothing Then Throw New ArgumentNullException(NameOf(stack))

        Select Case stack.Page
            Case FormMain.PageType.Launch
                Return New PageFrameTargets With {
                    .Left = FrmLaunchLeft,
                    .Right = FrmLaunchRight
                }
            Case FormMain.PageType.Download
                If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                Return New PageFrameTargets With {
                    .Left = FrmDownloadLeft,
                    .Right = FrmDownloadLeft.PageGet(subType)
                }
            Case FormMain.PageType.Tools
                If FrmToolsLeft Is Nothing Then FrmToolsLeft = New PageToolsLeft
                Return New PageFrameTargets With {
                    .Left = FrmToolsLeft,
                    .Right = FrmToolsLeft.PageGet(subType)
                }
            Case FormMain.PageType.Setup
                If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                Return New PageFrameTargets With {
                    .Left = FrmSetupLeft,
                    .Right = FrmSetupLeft.PageGet(subType)
                }
            Case FormMain.PageType.GameLog
                If FrmLogLeft Is Nothing Then FrmLogLeft = New PageLogLeft
                If FrmLogRight Is Nothing Then FrmLogRight = New PageLogRight
                Return New PageFrameTargets With {
                    .Left = FrmLogLeft,
                    .Right = FrmLogRight
                }
            Case FormMain.PageType.InstanceSelect
                If FrmSelectLeft Is Nothing Then FrmSelectLeft = New PageSelectLeft
                If FrmSelectRight Is Nothing Then FrmSelectRight = New PageSelectRight
                Return New PageFrameTargets With {
                    .Left = FrmSelectLeft,
                    .Right = FrmSelectRight
                }
            Case FormMain.PageType.TaskManager
                If FrmSpeedLeft Is Nothing Then FrmSpeedLeft = New PageSpeedLeft
                If FrmSpeedRight Is Nothing Then FrmSpeedRight = New PageSpeedRight
                Return New PageFrameTargets With {
                    .Left = FrmSpeedLeft,
                    .Right = FrmSpeedRight
                }
            Case FormMain.PageType.InstanceSetup
                If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                Return New PageFrameTargets With {
                    .Left = FrmInstanceLeft,
                    .Right = FrmInstanceLeft.PageGet(subType)
                }
            Case FormMain.PageType.CompDetail
                If FrmDownloadCompDetail Is Nothing Then FrmDownloadCompDetail = New PageDownloadCompDetail
                Return New PageFrameTargets With {
                    .Left = New MyPageLeft,
                    .Right = FrmDownloadCompDetail
                }
            Case FormMain.PageType.HelpDetail
                Return New PageFrameTargets With {
                    .Left = New MyPageLeft,
                    .Right = CType(stack.Additional(1), MyPageRight)
                }
            Case FormMain.PageType.VersionSaves
                If FrmInstanceSavesLeft Is Nothing Then FrmInstanceSavesLeft = New PageInstanceSavesLeft
                PageInstanceSavesLeft.CurrentSave = stack.Additional
                Return New PageFrameTargets With {
                    .Left = FrmInstanceSavesLeft,
                    .Right = FrmInstanceSavesLeft.PageGet(subType)
                }
            Case FormMain.PageType.HomePageMarket
                FrmHomepageMarket = If(FrmHomepageMarket, New PageHomePageMarket)
                Return New PageFrameTargets With {
                    .Left = New MyPageLeft,
                    .Right = FrmHomepageMarket
                }
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(stack), stack.Page, "未知的页面类型")
        End Select
    End Function

End Module
