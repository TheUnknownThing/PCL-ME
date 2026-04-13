Imports System.Net.Http
Imports PCL.Core.App
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Minecraft.Launch.Utils

Public Module ModLaunchArgumentShell

    Private ReadOnly ExtractJavaWrapperLock As New Object
    Private ReadOnly ExtractLinkDLock As New Object

    Public Function ExtractJavaWrapperShell(pathPure As String) As String
        Dim wrapperPath = pathPure & "JavaWrapper.jar"
        Log("[Java] 选定的 Java Wrapper 路径：" & wrapperPath)
        SyncLock ExtractJavaWrapperLock
            Try
                WriteJavaWrapper(wrapperPath)
            Catch ex As Exception
                If File.Exists(wrapperPath) Then
                    Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(wrapperPath)
                        WriteJavaWrapper(wrapperPath)
                    Catch ex2 As Exception
                        Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", LogLevel.Developer)
                        wrapperPath = pathPure & "JavaWrapper2.jar"
                        Try
                            WriteJavaWrapper(wrapperPath)
                        Catch ex3 As Exception
                            Throw New FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3)
                        End Try
                    End Try
                Else
                    Throw New FileNotFoundException("释放 Java Wrapper 失败", ex)
                End If
            End Try
        End SyncLock
        Return wrapperPath
    End Function

    Public Function ExtractLinkDShell(pathPure As String) As String
        Dim linkDPath = pathPure & "linkd.exe"
        SyncLock ExtractLinkDLock
            Try
                WriteLinkD(linkDPath)
            Catch ex As Exception
                If File.Exists(linkDPath) Then
                    Log(ex, "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成", LogLevel.Developer)
                    Try
                        File.Delete(linkDPath)
                        WriteLinkD(linkDPath)
                    Catch ex2 As Exception
                        Throw New FileNotFoundException("释放 linkd 失败", ex2)
                    End Try
                Else
                    Throw New FileNotFoundException("释放 linkd 失败", ex)
                End If
            End Try
        End SyncLock
        Return linkDPath
    End Function

    Public Function ShouldUseRetroWrapper(instance As McInstance) As Boolean
        Return MinecraftLaunchRetroWrapperService.ShouldUse(
            New MinecraftLaunchRetroWrapperRequest(
                instance.ReleaseTime,
                instance.Info.Drop,
                Setup.Get("LaunchAdvanceDisableRW"),
                Setup.Get("VersionAdvanceDisableRW", instance)))
    End Function

    Public Function CollectArgumentSectionJsons(instance As McInstance, sectionName As String) As List(Of String)
        Dim sections As New List(Of String)
        Dim currentInstance As McInstance = instance
        Do
            Dim argumentsToken = currentInstance.JsonObject("arguments")
            Dim sectionToken = If(argumentsToken Is Nothing, Nothing, argumentsToken(sectionName))
            If sectionToken IsNot Nothing Then sections.Add(sectionToken.ToString())
            If currentInstance.InheritInstanceName = "" Then Exit Do
            currentInstance = New McInstance(currentInstance.InheritInstanceName)
        Loop
        Return sections
    End Function

    Public Function GetSelectedJvmArgumentOverrides(instance As McInstance) As String
        Dim argumentJvm As String = Setup.Get("VersionAdvanceJvm", instance:=instance)
        If argumentJvm = "" Then argumentJvm = Setup.Get("LaunchAdvanceJvm")
        Return argumentJvm
    End Function

    Public Function BuildAuthlibInjectorArgument(loginType As String,
                                                 authBaseUrl As String,
                                                 pathPure As String,
                                                 includeDetailedHttpError As Boolean) As String
        If loginType <> "Auth" Then Return Nothing

        Dim server As String = authBaseUrl.Replace("/authserver", "")
        Try
            Dim response As String = NetGetCodeByRequestRetry(server, Encoding.UTF8)
            Return "-javaagent:""" & pathPure & "authlib-injector.jar""=" & server &
                   " -Dauthlibinjector.side=client" &
                   " -Dauthlibinjector.yggdrasil.prefetched=" & Convert.ToBase64String(Encoding.UTF8.GetBytes(response))
        Catch ex As HttpWebException When includeDetailedHttpError
            Throw New Exception($"无法连接到第三方登录服务器（{If(server, Nothing)}）{vbCrLf}详细信息：" & ex.InnerHttpException.WebResponse, ex)
        Catch ex As Exception
            Throw New Exception($"无法连接到第三方登录服务器（{If(server, Nothing)}）", ex)
        End Try
    End Function

    Public Function GetDebugLog4jConfigurationPath(instance As McInstance) As String
        If Not Config.Instance.UseDebugLof4j2Config.Item(instance.PathIndie) Then Return Nothing
        If instance.ReleaseTime.Year >= 2017 Then
            Return LaunchEnvUtils.ExtractDebugLog4j2Config()
        End If
        Return LaunchEnvUtils.ExtractLegacyDebugLog4j2Config()
    End Function

    Public Function GetRendererAgentArgument(instance As McInstance, pathPure As String) As String
        Dim renderer As Integer
        If Setup.Get("VersionAdvanceRenderer", instance:=instance) <> 0 Then
            renderer = Setup.Get("VersionAdvanceRenderer", instance:=instance) - 1
        Else
            renderer = Setup.Get("LaunchAdvanceRenderer")
        End If
        If renderer = 0 Then Return Nothing

        Dim mesaLoaderWindowsVersion = "25.3.5"
        Dim mesaLoaderWindowsTargetFile = pathPure & "\mesa-loader-windows\" & mesaLoaderWindowsVersion & "\Loader.jar"
        Return "-javaagent:""" & mesaLoaderWindowsTargetFile & """=" & If(renderer = 1, "llvmpipe", If(renderer = 2, "d3d12", "zink"))
    End Function

    Public Function TryGetLaunchProxyAddress(instance As McInstance) As Uri
        If Not Config.Instance.UseProxy.Item(instance.PathIndie) OrElse
           Not Config.Network.HttpProxy.Type.Equals(2) OrElse
           String.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress) Then
            Return Nothing
        End If

        Try
            Return New Uri(Setup.Get("SystemHttpProxy"))
        Catch ex As Exception
            Log(ex, "添加代理信息到游戏失败，放弃加入", LogLevel.Hint)
            Return Nothing
        End Try
    End Function

    Public Function GetLaunchProxyScheme(proxyAddress As Uri) As String
        If proxyAddress Is Nothing Then Return Nothing
        Return If(proxyAddress.Scheme.StartsWith("https", StringComparison.OrdinalIgnoreCase), "https", "http")
    End Function

    Public Function ShouldUseJavaWrapper(instance As McInstance) As Boolean
        Return IsUtf8CodePage() AndAlso
               Not Setup.Get("LaunchAdvanceDisableJLW") AndAlso
               Not Setup.Get("VersionAdvanceDisableJLW", instance)
    End Function

    Public Function GetMainClassOrThrow(instance As McInstance) As String
        If instance.JsonObject("mainClass") Is Nothing Then
            Throw New Exception("实例 JSON 中没有 mainClass 项！")
        End If
        Return instance.JsonObject("mainClass").ToString()
    End Function

    Public Sub ApplyGameArgumentPlan(plan As MinecraftLaunchGameArgumentPlan, instance As McInstance)
        If plan Is Nothing Then Throw New ArgumentNullException(NameOf(plan))
        For Each logMessage In plan.LogMessages
            Log(logMessage)
        Next
        If plan.ShouldRewriteOptiFineTweakerInJson Then
            Try
                WriteFile(instance.PathInstance & instance.Name & ".json", ReadFile(instance.PathInstance & instance.Name & ".json").Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"))
            Catch ex As Exception
                Log(ex, "替换 OptiFineForge TweakClass 失败")
            End Try
        End If
    End Sub

    Private Sub WriteJavaWrapper(path As String)
        WriteFile(path, GetResourceStream("Resources/java-wrapper.jar"))
    End Sub

    Private Sub WriteLinkD(path As String)
        WriteFile(path, GetResourceStream("Resources/linkd.exe"))
    End Sub

End Module
