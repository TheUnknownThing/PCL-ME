Public Enum McLoginType
    Legacy = 1
    Auth = 2
    Ms = 3
End Enum

Public MustInherit Class McLoginData
    ''' <summary>
    ''' 登录方式。
    ''' </summary>
    Public Type As McLoginType
    Public Overrides Function Equals(obj As Object) As Boolean
        Return obj IsNot Nothing AndAlso obj.GetHashCode() = GetHashCode()
    End Function
End Class

Public Class McLoginServer
    Inherits McLoginData

    Public UserName As String
    Public Password As String
    Public BaseUrl As String
    Public Description As String
    Public ForceReselectProfile As Boolean = False
    Public IsExist As Boolean = False

    Public Sub New(type As McLoginType)
        Me.Type = type
    End Sub

    Public Overrides Function GetHashCode() As Integer
        Return GetHash(UserName & Password & BaseUrl & Type) Mod Integer.MaxValue
    End Function
End Class

Public Class McLoginMs
    Inherits McLoginData

    Public OAuthRefreshToken As String = ""
    Public AccessToken As String = ""
    Public Uuid As String = ""
    Public UserName As String = ""
    Public ProfileJson As String = ""

    Public Sub New()
        Type = McLoginType.Ms
    End Sub

    Public Overrides Function GetHashCode() As Integer
        Return GetHash(OAuthRefreshToken & AccessToken & Uuid & UserName & ProfileJson) Mod Integer.MaxValue
    End Function
End Class

Public Class McLoginLegacy
    Inherits McLoginData

    Public UserName As String
    Public SkinType As Integer
    Public SkinName As String
    Public Uuid As String

    Public Sub New()
        Type = McLoginType.Legacy
    End Sub

    Public Overrides Function GetHashCode() As Integer
        Return GetHash(UserName & SkinType & SkinName & Type) Mod Integer.MaxValue
    End Function
End Class

Public Structure McLoginResult
    Public Name As String
    Public Uuid As String
    Public AccessToken As String
    Public Type As String
    Public ClientToken As String
    Public ProfileJson As String
End Structure
