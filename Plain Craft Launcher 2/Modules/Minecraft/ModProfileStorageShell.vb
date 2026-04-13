Imports PCL.Core.App
Imports PCL.Core.Minecraft.Launch

Public Module ModProfileStorageShell

    Public Function ReadProfilesDocument(json As String) As MinecraftLaunchProfileDocument
        Return MinecraftLaunchProfileStorageService.ParseDocument(
            json,
            Function(value) SecretDataProtection.Unprotect(value))
    End Function

    Public Function WriteProfilesDocument(lastUsedProfile As Integer, profileList As IEnumerable(Of McProfile)) As String
        Dim document = New MinecraftLaunchProfileDocument(
            lastUsedProfile,
            profileList.Select(AddressOf ConvertToPersistedProfile).ToList())
        Return MinecraftLaunchProfileStorageService.SerializeDocument(
            document,
            Function(value) SecretDataProtection.Protect(value))
    End Function

    Public Function CreateProfileFromPersisted(profile As MinecraftLaunchPersistedProfile) As McProfile
        If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))

        Select Case profile.Kind
            Case MinecraftLaunchStoredProfileKind.Microsoft
                Return New McProfile With {
                    .Type = McLoginType.Ms,
                    .Uuid = If(profile.Uuid, ""),
                    .Username = If(profile.Username, ""),
                    .AccessToken = If(profile.AccessToken, ""),
                    .RefreshToken = If(profile.RefreshToken, ""),
                    .Expires = profile.Expires,
                    .Desc = If(profile.Desc, ""),
                    .RawJson = If(profile.RawJson, ""),
                    .SkinHeadId = If(profile.SkinHeadId, "")
                }
            Case MinecraftLaunchStoredProfileKind.Authlib
                Return New McProfile With {
                    .Type = McLoginType.Auth,
                    .Uuid = If(profile.Uuid, ""),
                    .Username = If(profile.Username, ""),
                    .Server = If(profile.Server, ""),
                    .ServerName = If(profile.ServerName, ""),
                    .Name = If(profile.LoginName, ""),
                    .Password = If(profile.Password, ""),
                    .AccessToken = If(profile.AccessToken, ""),
                    .RefreshToken = If(profile.RefreshToken, ""),
                    .ClientToken = If(profile.ClientToken, ""),
                    .Expires = profile.Expires,
                    .Desc = If(profile.Desc, ""),
                    .SkinHeadId = If(profile.SkinHeadId, "")
                }
            Case Else
                Return New McProfile With {
                    .Type = McLoginType.Legacy,
                    .Uuid = If(profile.Uuid, ""),
                    .Username = If(profile.Username, ""),
                    .Expires = profile.Expires,
                    .Desc = If(profile.Desc, ""),
                    .SkinHeadId = If(profile.SkinHeadId, "")
                }
        End Select
    End Function

    Private Function ConvertToPersistedProfile(profile As McProfile) As MinecraftLaunchPersistedProfile
        Dim kind = MinecraftLaunchStoredProfileKind.Offline
        Select Case profile.Type
            Case McLoginType.Auth
                kind = MinecraftLaunchStoredProfileKind.Authlib
            Case McLoginType.Ms
                kind = MinecraftLaunchStoredProfileKind.Microsoft
        End Select

        Return New MinecraftLaunchPersistedProfile(
            kind,
            profile.Uuid,
            profile.Username,
            profile.Desc,
            profile.SkinHeadId,
            profile.Expires,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.Name,
            profile.Password,
            profile.ClientToken,
            profile.RawJson)
    End Function

End Module
