Public Module ModMainWindowPageSelectionShell

    Public Sub SelectMainPage(form As FormMain, stack As FormMain.PageStackData, subType As FormMain.PageSubType, isCurrentTopLevel As Boolean)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If stack Is Nothing Then Throw New ArgumentNullException(NameOf(stack))

        CType(form.PanTitleSelect.Children(stack), MyRadioButton).SetChecked(True, True, isCurrentTopLevel)
        Select Case stack.Page
            Case FormMain.PageType.Download
                If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft
                For Each item In FrmDownloadLeft.PanItem.Children
                    If item.GetType() Is GetType(MyListItem) AndAlso Val(item.Tag) = subType Then
                        CType(item, MyListItem).SetChecked(True, True, stack = form.PageCurrent)
                        Exit For
                    End If
                Next
            Case FormMain.PageType.Setup
                If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft
                If TypeOf FrmSetupLeft.PanItem.Children(subType) Is MyListItem Then
                    CType(FrmSetupLeft.PanItem.Children(subType), MyListItem).SetChecked(True, True, stack = form.PageCurrent)
                End If
        End Select
    End Sub

    Public Sub SelectSubPage(form As FormMain, stack As FormMain.PageStackData, subType As FormMain.PageSubType)
        If form Is Nothing Then Throw New ArgumentNullException(NameOf(form))
        If stack Is Nothing Then Throw New ArgumentNullException(NameOf(stack))

        Select Case stack.Page
            Case FormMain.PageType.InstanceSetup
                If FrmInstanceLeft Is Nothing Then FrmInstanceLeft = New PageInstanceLeft
                For Each item In FrmInstanceLeft.PanItem.Children
                    If item.GetType() Is GetType(MyListItem) AndAlso Val(item.Tag) = subType Then
                        CType(item, MyListItem).SetChecked(True, True, stack = form.PageCurrent)
                        Exit For
                    End If
                Next
            Case FormMain.PageType.VersionSaves
                If FrmInstanceSavesLeft Is Nothing Then FrmInstanceSavesLeft = New PageInstanceSavesLeft
                For Each item In FrmInstanceSavesLeft.PanItem.Children
                    If item.GetType() Is GetType(MyListItem) AndAlso Val(item.Tag) = subType Then
                        CType(item, MyListItem).SetChecked(True, True, stack = form.PageCurrent)
                        Exit For
                    End If
                Next
        End Select
    End Sub

End Module
