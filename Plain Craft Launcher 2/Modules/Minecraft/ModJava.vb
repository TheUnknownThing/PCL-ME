Imports PCL.Core.Minecraft
Imports PCL.Core.App
Imports PCL.Core.Minecraft.Launch
Imports System.Text.Json
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Minecraft.Java.UserPreference
Imports PCL.Core.IO

Public Module ModJava
    Public JavaListCacheVersion As Integer = 7

    ''' <summary>
    ''' 目前所有可用的 Java。
    ''' </summary>
    Public ReadOnly Property Javas As JavaManager
        Get
            Return JavaService.JavaManager
        End Get
    End Property

    ''' <summary>
    ''' 防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    ''' </summary>
    Public JavaLock As New Object
    ''' <summary>
    ''' 根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ''' 最小与最大版本在与输入相同时也会通过。
    ''' 必须在工作线程调用，且必须包括 SyncLock JavaLock。
    ''' </summary>
    Public Function JavaSelect(CancelException As String,
                               Optional MinVersion As Version = Nothing,
                               Optional MaxVersion As Version = Nothing,
                               Optional RelatedInstance As McInstance = Nothing) As JavaEntry
        Return ModJavaPreferenceShell.JavaSelectShell(Javas, CancelException, MinVersion, MaxVersion, RelatedInstance)
    End Function

    Public Function GetInstanceJavaPreference(instance As McInstance) As JavaPreference
        Return ModJavaPreferenceShell.GetInstanceJavaPreferenceShell(instance)
    End Function

    ''' <summary>
    ''' 是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    ''' </summary>
    Public Function IsGameSet64BitJava(Optional RelatedVersion As McInstance = Nothing) As Boolean
        Return ModJavaPreferenceShell.IsGameSet64BitJavaShell(Javas, RelatedVersion)
    End Function

#Region "下载"

    ''' <summary>
    ''' 提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    ''' </summary>
    Public Function JavaDownloadConfirm(VersionDescription As String, Optional ForcedManualDownload As Boolean = False) As Boolean
        Return ModJavaPromptShell.ConfirmJavaDownload(VersionDescription, ForcedManualDownload)
    End Function

    ''' <summary>
    ''' 获取下载 Java 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    ''' </summary>
    Public Function GetJavaDownloadLoader() As LoaderCombo(Of String)
        Return ModJavaLoaderShell.CreateDownloadLoader()
    End Function

#End Region

    Public Function ResolveLaunchJavaSelection(task As LoaderTask(Of Integer, Integer),
                                               javaWorkflow As MinecraftLaunchJavaWorkflowPlan,
                                               relatedInstance As McInstance) As JavaEntry
        Return ModJavaSelectionShell.ResolveLaunchJavaSelectionShell(task, javaWorkflow, relatedInstance)
    End Function

End Module
