Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchNativesShell

    Public Function GetNativesFolder(instance As McInstance) As String
        If instance Is Nothing Then Throw New ArgumentNullException(NameOf(instance))

        Return MinecraftLaunchNativesDirectoryService.ResolvePath(
            New MinecraftLaunchNativesDirectoryRequest(
                instance.PathInstance & instance.Name & "-natives",
                IsGBKEncoding,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\bin\natives",
                OsDrive & "ProgramData\PCL\natives"))
    End Function

    Public Sub SyncNatives(nativesFolder As String,
                           libraries As IEnumerable(Of McLibToken),
                           modeDebug As Boolean,
                           logMessage As Action(Of String))
        Dim nativeSyncResult = MinecraftLaunchNativesSyncService.Sync(
            New MinecraftLaunchNativesSyncRequest(
                nativesFolder,
                libraries.
                    Where(Function(native) native.IsNatives).
                    Select(Function(native) native.LocalPath).
                    ToList(),
                modeDebug))
        For Each message In nativeSyncResult.LogMessages
            logMessage?.Invoke(message)
        Next
    End Sub

End Module
