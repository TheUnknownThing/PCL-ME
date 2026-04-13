Public Class LaunchArgument
    Private _features As New List(Of String)

    Public Sub New(minecraft As McInstance)
        If minecraft.IsOldJson Then
            _features = minecraft.JsonObject("minecraftArguments").ToString.Split(" "c).ToList()
        Else
            For Each item In minecraft.JsonObject("arguments")("game")
                If item.Type = JTokenType.String Then
                    _features.Add(item.ToString)
                ElseIf item.Type = JTokenType.Object Then
                    _features.AddRange(item("value").Select(Function(x) x.ToString))
                End If
            Next
        End If
    End Sub

    Public Function HasArguments(key As String)
        Return _features.Contains(key)
    End Function
End Class
