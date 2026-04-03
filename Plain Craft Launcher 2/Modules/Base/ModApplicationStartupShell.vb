Imports System.IO
Imports PCL.Core.App
Imports PCL.Core.App.Essentials
Imports PCL.Core.Utils.OS

Public Module ModApplicationStartupShell

    Public Sub ExecuteImmediateCommand(startupCommandPlan As LauncherStartupImmediateCommandPlan)
        If startupCommandPlan Is Nothing Then Throw New ArgumentNullException(NameOf(startupCommandPlan))

        Select Case startupCommandPlan.Kind
            Case LauncherStartupImmediateCommandKind.Invalid
                Throw New ArgumentException(startupCommandPlan.InvalidMessage)
            Case LauncherStartupImmediateCommandKind.SetGpuPreference
                Try
                    ProcessInterop.SetGpuPreference(startupCommandPlan.Argument)
                    Environment.Exit(ProcessReturnValues.TaskDone)
                Catch ex As Exception
                    Environment.Exit(ProcessReturnValues.Fail)
                End Try
            Case LauncherStartupImmediateCommandKind.OptimizeMemory
                Dim ram = KernelInterop.GetAvailablePhysicalMemoryBytes()
                Try
                    PageToolsTest.MemoryOptimizeInternal(False)
                Catch ex As Exception
                    MsgBox(ex.Message, MsgBoxStyle.Critical, "内存优化失败")
                    Environment.Exit(-1)
                End Try
                If KernelInterop.GetAvailablePhysicalMemoryBytes() < ram Then
                    Environment.Exit(0)
                Else
                    Environment.Exit((KernelInterop.GetAvailablePhysicalMemoryBytes() - ram) / 1024)
                End If
        End Select
    End Sub

    Public Sub ApplyBootstrap(bootstrapResult As LauncherStartupBootstrapResult)
        If bootstrapResult Is Nothing Then Throw New ArgumentNullException(NameOf(bootstrapResult))

        For Each directoryPath In bootstrapResult.DirectoriesToCreate
            Directory.CreateDirectory(directoryPath)
        Next
        For Each configKey In bootstrapResult.ConfigKeysToLoad
            Setup.Load(configKey)
        Next

        Dim updateBranchCfg = Config.Update.UpdateChannelConfig
        If updateBranchCfg.IsDefault() Then
            updateBranchCfg.SetValue(CInt(bootstrapResult.DefaultUpdateChannel))
        End If

        For Each oldLogFile In bootstrapResult.LegacyLogFilesToDelete
            If File.Exists(oldLogFile) Then File.Delete(oldLogFile)
        Next
    End Sub

    Public Sub ApplyVisualPlan(visualPlan As LauncherStartupVisualPlan)
        If visualPlan Is Nothing Then Throw New ArgumentNullException(NameOf(visualPlan))

        ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(visualPlan.TooltipDefaults.InitialShowDelayMilliseconds))
        ToolTipService.BetweenShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(visualPlan.TooltipDefaults.BetweenShowDelayMilliseconds))
        ToolTipService.ShowDurationProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(visualPlan.TooltipDefaults.ShowDurationMilliseconds))
        ToolTipService.PlacementProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(Primitives.PlacementMode.Bottom))
        ToolTipService.HorizontalOffsetProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(visualPlan.TooltipDefaults.HorizontalOffset))
        ToolTipService.VerticalOffsetProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(visualPlan.TooltipDefaults.VerticalOffset))

        If visualPlan.ShouldShowSplashScreen Then
            FrmStart = New SplashScreen(visualPlan.SplashScreen.IconPath)
            FrmStart.Show(False, True)
        End If
    End Sub

    Public Sub ApplyEnvironmentWarning(environmentWarningPrompt As LauncherStartupPrompt)
        If environmentWarningPrompt Is Nothing Then Return
        RunStartupPrompt(environmentWarningPrompt)
    End Sub

End Module
