Imports System.Net
Imports System.Net.Http
Imports System.Text.Json.Nodes
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.IO.Net.Http.Client.Request

Public Module ModLaunchMicrosoftRequestShell

    Public Function ExecuteFormPost(requestPlan As MinecraftLaunchHttpRequestPlan) As String
        ValidateRequestPlan(requestPlan)

        Using response = HttpRequest.
            CreatePost(requestPlan.Url).
            WithFormContent(requestPlan.Body).
            SendAsync().
            GetAwaiter().
            GetResult()

            response.EnsureSuccessStatusCode()
            Return response.AsString()
        End Using
    End Function

    Public Function ExecuteJsonPost(requestPlan As MinecraftLaunchHttpRequestPlan) As String
        Dim response = ExecuteJsonPostWithStatus(requestPlan)
        If Not response.IsSuccessStatusCode Then
            Throw New HttpRequestException(
                $"HTTP request failed with status code {CInt(response.StatusCode)} ({response.StatusCode}).",
                Nothing,
                response.StatusCode)
        End If

        Return response.Body
    End Function

    Public Function ExecuteJsonPostWithStatus(requestPlan As MinecraftLaunchHttpRequestPlan) As MicrosoftLaunchHttpShellResponse
        ValidateRequestPlan(requestPlan)

        Using response = HttpRequest.
            CreatePost(requestPlan.Url).
            WithJsonContent(JsonNode.Parse(requestPlan.Body)).
            SendAsync().
            GetAwaiter().
            GetResult()

            Return New MicrosoftLaunchHttpShellResponse(response.StatusCode, response.IsSuccessStatusCode, response.AsString())
        End Using
    End Function

    Public Function ExecuteBearerGet(requestPlan As MinecraftLaunchHttpRequestPlan) As String
        Dim response = ExecuteBearerGetWithStatus(requestPlan)
        If Not response.IsSuccessStatusCode Then
            Throw New HttpRequestException(
                $"HTTP request failed with status code {CInt(response.StatusCode)} ({response.StatusCode}).",
                Nothing,
                response.StatusCode)
        End If

        Return response.Body
    End Function

    Public Function ExecuteBearerGetWithStatus(requestPlan As MinecraftLaunchHttpRequestPlan) As MicrosoftLaunchHttpShellResponse
        ValidateRequestPlan(requestPlan)

        Using response = HttpRequest.Create(requestPlan.Url).
            WithBearerToken(requestPlan.BearerToken).
            SendAsync().
            GetAwaiter().
            GetResult()

            Return New MicrosoftLaunchHttpShellResponse(response.StatusCode, response.IsSuccessStatusCode, response.AsString())
        End Using
    End Function

    Private Sub ValidateRequestPlan(requestPlan As MinecraftLaunchHttpRequestPlan)
        If requestPlan Is Nothing Then Throw New ArgumentNullException(NameOf(requestPlan))
        If String.IsNullOrWhiteSpace(requestPlan.Url) Then Throw New ArgumentException("缺少请求地址。", NameOf(requestPlan))
    End Sub

    Public Structure MicrosoftLaunchHttpShellResponse
        Public Sub New(statusCode As HttpStatusCode, isSuccessStatusCode As Boolean, body As String)
            Me.StatusCode = statusCode
            Me.IsSuccessStatusCode = isSuccessStatusCode
            Me.Body = body
        End Sub

        Public StatusCode As HttpStatusCode
        Public IsSuccessStatusCode As Boolean
        Public Body As String
    End Structure

End Module
