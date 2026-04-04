Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchProfileShell

    Public Function GetCurrentProfileKind(selectedProfile As McProfile) As MinecraftLaunchProfileKind
        If selectedProfile Is Nothing Then Return MinecraftLaunchProfileKind.None
        Select Case selectedProfile.Type
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

    Public Function GetSelectedAuthServerBase(selectedProfile As McProfile) As String
        If selectedProfile Is Nothing OrElse selectedProfile.Type <> McLoginType.Auth Then Return Nothing
        Return selectedProfile.Server.BeforeLast("/authserver")
    End Function

    Public Function GetSelectedProfileIndex(selectedProfile As McProfile, profileList As List(Of McProfile)) As Integer?
        If selectedProfile Is Nothing OrElse profileList Is Nothing Then Return Nothing
        Dim index = profileList.IndexOf(selectedProfile)
        Return If(index >= 0, index, Nothing)
    End Function

    Public Function GetStoredProfiles(profileList As List(Of McProfile)) As List(Of MinecraftLaunchStoredProfile)
        If profileList Is Nothing Then Return New List(Of MinecraftLaunchStoredProfile)
        Return profileList.Select(AddressOf ConvertToStoredProfile).ToList()
    End Function

    Public Function CreateMicrosoftLoginResult(accessToken As String, userName As String, uuid As String, profileJson As String) As McLoginResult
        Return New McLoginResult With {
            .AccessToken = accessToken,
            .Name = userName,
            .Uuid = uuid,
            .Type = "Microsoft",
            .ClientToken = uuid,
            .ProfileJson = profileJson}
    End Function

    Public Function CreateMicrosoftLoginResultFromStored(profile As MinecraftLaunchStoredProfile) As McLoginResult
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
        Return CreateMicrosoftLoginResult(
            If(profile.AccessToken, ""),
            profile.Username,
            profile.Uuid,
            If(profile.RawJson, ""))
    End Function

    Public Function CreateCurrentMicrosoftLoginResult(selectedProfile As McProfile,
                                                      input As McLoginMs) As McLoginResult
        If selectedProfile IsNot Nothing AndAlso selectedProfile.Type = McLoginType.Ms Then
            Return CreateMicrosoftLoginResultFromStored(ConvertToStoredProfile(selectedProfile))
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

    Public Sub ApplyProfileMutationPlan(plan As MinecraftLaunchProfileMutationPlan,
                                        profileList As List(Of McProfile),
                                        ByRef selectedProfile As McProfile,
                                        ByRef isCreatingProfile As Boolean,
                                        showNotice As Action(Of String))
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        If profileList Is Nothing Then Throw New ArgumentNullException(NameOf(profileList))
        If Not String.IsNullOrWhiteSpace(plan.NoticeMessage) Then showNotice?.Invoke(plan.NoticeMessage)

        Select Case plan.Kind
            Case MinecraftLaunchProfileMutationKind.CreateNew
                Dim createdProfile = CreateProfileFromStored(plan.CreateProfile)
                profileList.Add(createdProfile)
                If plan.ShouldSelectCreatedProfile Then selectedProfile = createdProfile
            Case MinecraftLaunchProfileMutationKind.UpdateSelected, MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate
                If Not plan.TargetProfileIndex.HasValue OrElse plan.TargetProfileIndex.Value < 0 OrElse plan.TargetProfileIndex.Value >= profileList.Count Then
                    Throw New InvalidOperationException("无法应用档案变更：目标档案不存在。")
                End If
                UpdateProfileFromStored(profileList(plan.TargetProfileIndex.Value), plan.UpdateProfile)
            Case Else
                Throw New InvalidOperationException("未知的档案变更类型。")
        End Select

        If plan.ShouldClearCreatingProfile Then isCreatingProfile = False
    End Sub

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

End Module
