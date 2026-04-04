Imports System.Text
Imports PCL.Core.Minecraft.Launch

Public Module ModLaunchAuthlibRequestShell

    Public Function ExecuteRequest(requestPlan As MinecraftLaunchHttpRequestPlan) As String
        If requestPlan Is Nothing Then Throw New ArgumentNullException(NameOf(requestPlan))

        Return NetRequestRetry(
            Url:=requestPlan.Url,
            Method:=requestPlan.Method,
            Data:=requestPlan.Body,
            Headers:=BuildRequestHeaders(requestPlan),
            ContentType:=requestPlan.ContentType)
    End Function

    Public Function ExecuteMetadataRequest(url As String) As String
        Return NetGetCodeByRequestRetry(url, Encoding.UTF8)
    End Function

    Private Function BuildRequestHeaders(requestPlan As MinecraftLaunchHttpRequestPlan) As Dictionary(Of String, String)
        If requestPlan.Headers Is Nothing Then Return New Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(requestPlan.Headers)
    End Function

End Module
