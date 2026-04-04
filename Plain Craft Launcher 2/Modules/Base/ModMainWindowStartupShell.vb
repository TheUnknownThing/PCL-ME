Imports PCL.Core.App.Essentials
Imports PCL.Core.App

Public Module ModMainWindowStartupShell

    Public Sub ApplyMilestone(milestoneResult As LauncherStartupMilestoneResult)
        If milestoneResult Is Nothing Then Throw New ArgumentNullException(NameOf(milestoneResult))

        Setup.Set("SystemCount", milestoneResult.UpdatedCount)
        If milestoneResult.ShouldAttemptUnlockHiddenTheme AndAlso ThemeUnlock(6, False) Then
            MyMsgBox(milestoneResult.HiddenThemeNotice.Message, milestoneResult.HiddenThemeNotice.Title)
        End If
    End Sub

    Public Sub ApplyVersionTransition(
        workflowPlan As LauncherVersionTransitionWorkflowPlan,
        onMigrateOldProfile As Action,
        onShowCommunityAnnouncement As Action,
        onShowUpdateLog As Action)
        If workflowPlan Is Nothing Then Throw New ArgumentNullException(NameOf(workflowPlan))

        For Each settingWrite In workflowPlan.SettingWrites
            Setup.Set(settingWrite.Key, settingWrite.Value)
        Next
        If workflowPlan.HighestVersionLogMessage IsNot Nothing Then
            Log(workflowPlan.HighestVersionLogMessage)
        End If
        For Each notice In workflowPlan.Transition.Notices
            MyMsgBox(notice.Message, notice.Title)
        Next

        If workflowPlan.CustomSkinMigration IsNot Nothing Then
            CopyFile(workflowPlan.CustomSkinMigration.SourcePath, workflowPlan.CustomSkinMigration.TargetPath)
            Log(workflowPlan.CustomSkinMigration.LogMessage)
        End If

        If workflowPlan.Transition.ShouldUnhideSetupAbout Then
            Config.Preference.Hide.SetupAbout = False
            Log(workflowPlan.SetupAboutUnhideLogMessage)
        End If
        If workflowPlan.Transition.ShouldMigrateOldProfile AndAlso onMigrateOldProfile IsNot Nothing Then
            RunInNewThread(Sub() onMigrateOldProfile())
        End If
        If workflowPlan.ModNameMigrationLogMessage IsNot Nothing Then
            Log(workflowPlan.ModNameMigrationLogMessage)
        End If
        If workflowPlan.Transition.ShouldShowCommunityAnnouncement AndAlso onShowCommunityAnnouncement IsNot Nothing Then
            onShowCommunityAnnouncement()
        End If
        If workflowPlan.Transition.ShouldShowUpdateLog AndAlso onShowUpdateLog IsNot Nothing Then
            onShowUpdateLog()
        End If
    End Sub

End Module
