Imports PCL.Core.Utils.Codecs
Imports PCL.Core.Utils.Exts

Public Partial Class CrashAnalyzer

    Private LogMc As String = Nothing, LogMcDebug As String = Nothing, LogHs As String = Nothing, LogCrash As String = Nothing
    Private LogAll As String
    Private CrashReasons As New Dictionary(Of CrashReason, List(Of String))

    Private Enum CrashReason
        Mod文件被解压
        MixinBootstrap缺失
        内存不足
        使用JDK
        显卡不支持OpenGL
        使用OpenJ9
        Java版本过高
        Java版本不兼容
        Mod名称包含特殊字符
        显卡驱动不支持导致无法设置像素格式
        极短的程序输出
        Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION
        AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION
        Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION
        玩家手动触发调试崩溃
        光影或资源包导致OpenGL1282错误
        文件或内容校验失败
        确定Mod导致游戏崩溃
        怀疑Mod导致游戏崩溃
        Mod配置文件导致游戏崩溃
        ModMixin失败
        Mod加载器报错
        Mod初始化失败
        堆栈分析发现关键字
        堆栈分析发现Mod名称
        OptiFine导致无法加载世界
        特定方块导致崩溃
        特定实体导致崩溃
        材质过大或显卡配置不足
        没有可用的分析文件
        使用32位Java导致JVM无法分配足够多的内存
        Mod重复安装
        Mod互不兼容
        OptiFine与Forge不兼容
        Fabric报错
        Fabric报错并给出解决方案
        Forge报错
        低版本Forge与高版本Java不兼容
        实例Json中存在多个Forge
        Mod过多导致超出ID限制
        NightConfig的Bug
        ShadersMod与OptiFine同时安装
        Forge安装不完整
        Mod需要Java11
        Mod缺少前置或MC版本错误
    End Enum

    Private _version As McInstance = Nothing

    Public Sub Analyze(Optional version As McInstance = Nothing)
        _version = version
        Log("[Crash] 步骤 3：分析崩溃原因")
        LogAll = If(LogMc, If(LogMcDebug, "")) & If(LogHs, "") & If(LogCrash, "")

        If LogAll.Contains("quilt") AndAlso LogAll.Contains("Mod Table Version") Then
            Log("[Crash] 处理 Quilt Mod Table 后再继续分析")
            Dim beforeTable = LogAll.BeforeFirst("| Index")
            Dim afterTable = LogAll.AfterFirst("Mod Table Version:")
            LogAll = beforeTable & afterTable
        End If

        AnalyzeCrit1()
        If CrashReasons.Any Then GoTo Done
        AnalyzeCrit2()
        If CrashReasons.Any Then GoTo Done

        If LogAll.Contains("orge") OrElse LogAll.Contains("abric") OrElse LogAll.Contains("uilt") OrElse LogAll.Contains("iteloader") Then
            Dim keywords As New List(Of String)
            If LogCrash IsNot Nothing Then
                Log("[Crash] 开始进行崩溃日志堆栈分析")
                keywords.AddRange(AnalyzeStackKeyword(LogCrash.BeforeFirst("System Details")))
            End If
            If LogMc IsNot Nothing Then
                Dim fatals As List(Of String) = RegexSearch(LogMc, "/FATAL] .+?(?=[\n]+\[)")
                If LogMc.Contains("Unreported exception thrown!") Then fatals.Add(LogMc.Between("Unreported exception thrown!", "at oolloo.jlw.Wrapper"))
                Log("[Crash] 开始进行 Minecraft 日志堆栈分析，发现 " & fatals.Count & " 个报错项")
                For Each fatal In fatals
                    keywords.AddRange(AnalyzeStackKeyword(fatal))
                Next
            End If
            If LogHs IsNot Nothing Then
                Log("[Crash] 开始进行虚拟机堆栈分析")
                Dim stackLogs As String = LogHs.Between("T H R E A D", "Registers:")
                keywords.AddRange(AnalyzeStackKeyword(stackLogs))
            End If
            If keywords.Any Then
                Dim names = AnalyzeModName(keywords)
                If names Is Nothing Then
                    AppendReason(CrashReason.堆栈分析发现关键字, keywords)
                Else
                    AppendReason(CrashReason.堆栈分析发现Mod名称, names)
                End If
                GoTo Done
            End If
        Else
            Log("[Crash] 可能并未安装 Mod，不进行堆栈分析")
        End If

        AnalyzeCrit3()
Done:
        If Not CrashReasons.Any() Then
            Log("[Crash] 步骤 3：分析崩溃原因完成，未找到可能的原因")
        Else
            Log("[Crash] 步骤 3：分析崩溃原因完成，找到 " & CrashReasons.Count & " 条可能的原因")
            For Each reason In CrashReasons
                Log("[Crash]  - " & GetStringFromEnum(reason.Key) & If(reason.Value.Any, "（" & Join(reason.Value, "；") & "）", ""))
            Next
        End If
    End Sub

    Private Sub AppendReason(reason As CrashReason, Optional additional As ICollection(Of String) = Nothing)
        If CrashReasons.ContainsKey(reason) Then
            If additional IsNot Nothing Then
                CrashReasons(reason).AddRange(additional)
                CrashReasons(reason) = CrashReasons(reason).Distinct.ToList
            End If
        Else
            CrashReasons.Add(reason, New List(Of String)(If(additional, {})))
        End If
        Log("[Crash] 可能的崩溃原因：" & GetStringFromEnum(reason) & If(additional IsNot Nothing AndAlso additional.Any, "（" & Join(additional, "；") & "）", ""))
    End Sub

    Private Sub AppendReason(reason As CrashReason, additional As String)
        AppendReason(reason, If(String.IsNullOrEmpty(additional), Nothing, New List(Of String) From {additional}))
    End Sub

    Private Sub AnalyzeCrit1()
        If LogMc Is Nothing AndAlso LogHs Is Nothing AndAlso LogCrash Is Nothing Then
            AppendReason(CrashReason.没有可用的分析文件)
            Return
        End If
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass") Then AppendReason(CrashReason.Java版本过高)
        End If
        If LogMc IsNot Nothing Then
            If LogMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one") Then AppendReason(CrashReason.实例Json中存在多个Forge)
            If LogMc.Contains("The driver does not appear to support OpenGL") Then AppendReason(CrashReason.显卡不支持OpenGL)
            If LogMc.Contains("java.lang.ClassCastException: java.base/jdk") Then AppendReason(CrashReason.使用JDK)
            If LogMc.Contains("java.lang.ClassCastException: class jdk.") Then AppendReason(CrashReason.使用JDK)
            If LogMc.Contains("TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("Open J9 is not supported") OrElse LogMc.Contains("OpenJ9 is incompatible") OrElse LogMc.Contains(".J9VMInternals.") Then AppendReason(CrashReason.使用OpenJ9)
            If LogMc.Contains("java.lang.NoSuchFieldException: ucp") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("because module java.base does not export") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue.") Then AppendReason(CrashReason.Mod文件被解压)
            If LogMc.Contains("Extracted mod jars found, loading will NOT continue") Then AppendReason(CrashReason.Mod文件被解压)
            If LogMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker") Then AppendReason(CrashReason.MixinBootstrap缺失)
            If LogMc.Contains("Couldn't set pixel format") Then AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式)
            If LogMc.Contains("java.lang.OutOfMemoryError") OrElse LogMc.Contains("an out of memory error") Then AppendReason(CrashReason.内存不足)
            If LogMc.Contains("java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders.") Then AppendReason(CrashReason.ShadersMod与OptiFine同时安装)
            If LogMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier") Then AppendReason(CrashReason.低版本Forge与高版本Java不兼容)
            If LogMc.Contains("1282: Invalid operation") Then AppendReason(CrashReason.光影或资源包导致OpenGL1282错误)
            If LogMc.Contains("signer information does not match signer information of other classes in the same package") Then AppendReason(CrashReason.文件或内容校验失败, If(RegexSeek(LogMc, "(?<=class "")[^']+(?=""'s signer information)"), "").TrimEnd(vbCrLf))
            If LogMc.Contains("Maybe try a lower resolution resourcepack?") Then AppendReason(CrashReason.材质过大或显卡配置不足)
            If LogMc.Contains("java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") AndAlso LogMc.Contains("OptiFine") Then AppendReason(CrashReason.OptiFine导致无法加载世界)
            If LogMc.Contains("Unsupported class file major version") Then AppendReason(CrashReason.Java版本不兼容)
            If LogMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available") Then AppendReason(CrashReason.NightConfig的Bug)
            If LogMc.Contains("Cannot find launch target fmlclient, unable to launch") Then AppendReason(CrashReason.Forge安装不完整)
            If LogMc.Contains("Invalid paths argument, contained no existing paths") AndAlso LogMc.Contains("libraries\net\minecraftforge\fmlcore") Then AppendReason(CrashReason.Forge安装不完整)
            If LogMc.Contains("Invalid module name: '' is not a Java identifier") Then AppendReason(CrashReason.Mod名称包含特殊字符)
            If LogMc.Contains("has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("Unsupported major.minor version") Then AppendReason(CrashReason.Java版本不兼容)
            If LogMc.Contains("Invalid maximum heap size") Then AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存)
            If LogMc.Contains("Could not reserve enough space") Then
                If LogMc.Contains("for 1048576KB object heap") Then
                    AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存)
                Else
                    AppendReason(CrashReason.内存不足)
                End If
            End If
            If LogMc.Contains("Caught exception from ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(RegexSeek(LogMc, "(?<=Caught exception from )[^\n]+?")?.TrimEnd((vbCrLf & " ").ToCharArray)))
            If LogMc.Contains("DuplicateModsFoundException") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(LogMc, "(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Found a duplicate mod") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(If(RegexSeek(LogMc, "Found a duplicate mod[^\n]+"), ""), "[^\\/]+.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Found duplicate mods") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(LogMc, "(?<=Mod ID: ')\w+?(?=' from mod files:)").Distinct.ToList)
            If LogMc.Contains("ModResolutionException: Duplicate") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(If(RegexSeek(LogMc, "ModResolutionException: Duplicate[^\n]+"), ""), "[^\\/]+.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Incompatible mods found!") Then AppendReason(CrashReason.Mod互不兼容, If(RegexSeek(LogMc, "(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )"), ""))
            If LogMc.Contains("Missing or unsupported mandatory dependencies:") Then
                AppendReason(CrashReason.Mod缺少前置或MC版本错误,
                    RegexSearch(LogMc, "(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+", RegularExpressions.RegexOptions.IgnoreCase).
                    Select(Function(s) s.Trim((vbCrLf & vbTab & " ").ToCharArray)).Distinct().ToList())
            End If
        End If
        If LogHs IsNot Nothing Then
            If LogHs.Contains("The system is out of physical RAM or swap space") Then AppendReason(CrashReason.内存不足)
            If LogHs.Contains("Out of Memory Error") Then AppendReason(CrashReason.内存不足)
            If LogHs.Contains("EXCEPTION_ACCESS_VIOLATION") Then
                If LogHs.Contains("# C  [ig") Then AppendReason(CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
                If LogHs.Contains("# C  [atio") Then AppendReason(CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
                If LogHs.Contains("# C  [nvoglv") Then AppendReason(CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
            End If
        End If
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains("maximum id range exceeded") Then AppendReason(CrashReason.Mod过多导致超出ID限制)
            If LogCrash.Contains("java.lang.OutOfMemoryError") Then AppendReason(CrashReason.内存不足)
            If LogCrash.Contains("Pixel format not accelerated") Then AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式)
            If LogCrash.Contains("Manually triggered debug crash") Then AppendReason(CrashReason.玩家手动触发调试崩溃)
            If LogCrash.Contains("has mods that were not found") AndAlso RegexCheck(LogCrash, "The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogCrash.Contains("-- MOD ") Then
                Dim logCrashMod As String = LogCrash.Between("-- MOD ", "Failure message:")
                If logCrashMod.ContainsF(".jar", True) Then
                    AppendReason(CrashReason.确定Mod导致游戏崩溃, If(RegexSeek(logCrashMod, "(?<=Mod File: ).+"), "").TrimEnd((vbCrLf & " ").ToCharArray))
                Else
                    AppendReason(CrashReason.Mod加载器报错, If(RegexSeek(LogCrash, "(?<=Failure message: )[\w\W]+?(?=\tMod)"), "").Replace(vbTab, " ").TrimEnd((vbCrLf & " ").ToCharArray))
                End If
            End If
            If LogCrash.Contains("Multiple entries with same key: ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=Multiple entries with same key: )[^=]+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
            If LogCrash.Contains("LoaderExceptionModCrash: Caught exception from ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
            If LogCrash.Contains("Failed loading config file ") Then AppendReason(CrashReason.Mod配置文件导致游戏崩溃, {TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=Failed loading config file .+ for modid )[^\n]+"), "").TrimEnd(vbCrLf)).First, If(RegexSeek(LogCrash, "(?<=Failed loading config file ).+(?= of type)"), "").TrimEnd(vbCrLf)})
        End If
    End Sub

    Private Sub AnalyzeCrit2()
        Dim mixinAnalyze =
        Function(logText As String) As Boolean
            Dim isMixin As Boolean =
                logText.Contains("Mixin prepare failed ") OrElse logText.Contains("Mixin apply failed ") OrElse
                logText.Contains("MixinApplyError") OrElse logText.Contains("MixinTransformerError") OrElse
                logText.Contains("mixin.injection.throwables.") OrElse logText.Contains(".json] FAILED during )")
            If Not isMixin Then Return False
            Dim modName As String = RegexSeek(logText, "(?<=from mod )[^.\/ ]+(?=\] from)")
            If modName Is Nothing Then modName = RegexSeek(logText, "(?<=for mod )[^.\/ ]+(?= failed)")
            If modName IsNot Nothing Then
                AppendReason(CrashReason.ModMixin失败, TryAnalyzeModName(modName.TrimEnd((vbCrLf & " ").ToCharArray)))
                Return True
            End If
            For Each jsonName In RegexSearch(logText, "(?<=^[^\t]+[ \[{(]{1})[^ \[{(]+\.[^ ]+(?=\.json)", RegularExpressions.RegexOptions.Multiline)
                AppendReason(CrashReason.ModMixin失败, TryAnalyzeModName(jsonName.Replace("mixins", "mixin").Replace(".mixin", "").Replace("mixin.", "")))
                Return True
            Next
            AppendReason(CrashReason.ModMixin失败)
            Return True
        End Function

        If LogMc IsNot Nothing Then
            Dim isMixin As Boolean = mixinAnalyze(LogMc)
            If LogMc.Contains("An exception was thrown, the game will display an error screen and halt.") Then AppendReason(CrashReason.Forge报错, RegexSeek(LogMc, "(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")?.Trim(vbCrLf))
            If LogMc.Contains("A potential solution has been determined:") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If LogMc.Contains("A potential solution has been determined, this may resolve your problem:") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If LogMc.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题：") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If Not isMixin AndAlso LogMc.Contains("due to errors, provided by ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogMc, "(?<=due to errors, provided by ')[^']+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
        End If

        If LogCrash IsNot Nothing Then
            mixinAnalyze(LogCrash)
            If LogCrash.Contains("Suspected Mod") Then
                Dim suspectsRaw As String = LogCrash.Between("Suspected Mod", "Stacktrace")
                If Not suspectsRaw.StartsWithF("s: None") Then
                    Dim suspects = RegexSearch(suspectsRaw, "(?<=\n\t[^(\t]+\()[^)\n]+")
                    If suspects.Any Then AppendReason(CrashReason.怀疑Mod导致游戏崩溃, TryAnalyzeModName(suspects))
                End If
            End If
        End If
    End Sub

    Private Sub AnalyzeCrit3()
        If LogMc IsNot Nothing Then
            If Not (LogMc.Contains("at net.") OrElse LogMc.Contains("INFO]")) AndAlso LogHs Is Nothing AndAlso LogCrash Is Nothing AndAlso LogMc.Length < 100 Then
                AppendReason(CrashReason.极短的程序输出, LogMc)
            End If
            If LogMc.Contains("Mod resolution failed") Then AppendReason(CrashReason.Mod加载器报错)
            If LogMc.Contains("Failed to create mod instance.") Then AppendReason(CrashReason.Mod初始化失败, TryAnalyzeModName(If(RegexSeek(LogMc, "(?<=Failed to create mod instance. ModID: )[^,]+"), If(RegexSeek(LogMc, "(?<=Failed to create mod instance. ModId )[^\n]+(?= for )"), "")).TrimEnd(vbCrLf)))
        End If
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains(vbTab & "Block location: World: ") Then AppendReason(CrashReason.特定方块导致崩溃, If(RegexSeek(LogCrash, "(?<=\tBlock: Block\{)[^\}]+"), "") & " " & If(RegexSeek(LogCrash, "(?<=\tBlock location: World: )\([^\)]+\)"), ""))
            If LogCrash.Contains(vbTab & "Entity's Exact location: ") Then AppendReason(CrashReason.特定实体导致崩溃, If(RegexSeek(LogCrash, "(?<=\tEntity Type: )[^\n]+(?= \()"), "") & " (" & If(RegexSeek(LogCrash, "(?<=\tEntity's Exact location: )[^\n]+"), "").TrimEnd(vbCrLf.ToCharArray) & ")")
        End If
    End Sub

    Private Function AnalyzeStackKeyword(errorStack As String) As List(Of String)
        errorStack = vbLf & If(errorStack, "") & vbLf
        Dim stackSearchResults As New List(Of String)
        stackSearchResults.AddRange(RegexSearch(errorStack, "(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)"))
        stackSearchResults.AddRange(RegexSearch(errorStack, "(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()").Select(Function(s) s.Replace("$", ".")))
        stackSearchResults = stackSearchResults.Distinct.ToList

        Dim possibleStacks As New List(Of String)
        For Each stack As String In stackSearchResults
            For Each ignoreStack In {
                "java", "sun", "javax", "jdk", "oolloo",
                "org.lwjgl", "com.sun", "net.minecraftforge", "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache", "org.spongepowered", "net.fabricmc", "com.mumfrey", "org.quiltmc",
                "com.electronwill.nightconfig", "it.unimi.dsi",
                "MojangTricksIntelDriversForPerformance_javaw"}
                If stack.StartsWithF(ignoreStack) Then GoTo NextStack
            Next
            possibleStacks.Add(stack.Trim)
NextStack:
        Next
        possibleStacks = possibleStacks.Distinct.ToList

        Log("[Crash] 找到 " & possibleStacks.Count & " 条可能的堆栈信息")
        If Not possibleStacks.Any() Then Return New List(Of String)
        For Each stack As String In possibleStacks
            Log("[Crash]  - " & stack)
        Next

        Dim possibleWords As New List(Of String)
        For Each stack As String In possibleStacks
            Dim splited = stack.Split(".")
            For i = 0 To Math.Min(3, splited.Count - 1)
                Dim word As String = splited(i)
                If word.Length <= 2 OrElse word.StartsWithF("func_") Then Continue For
                If {"com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio", "api", "dsi", "top", "mcp",
                    "core", "init", "mods", "main", "file", "game", "load", "read", "done", "util", "tile", "item", "base", "oshi", "impl", "data", "pool", "task",
                    "forge", "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl",
                    "gitlab", "common", "server", "config", "mixins", "compat", "loader", "launch", "entity", "assist", "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render", "packet", "events",
                    "preinit", "preload", "machine", "reflect", "channel", "general", "handler", "content", "systems", "modules", "service",
                    "fastutil", "optifine", "internal", "platform", "override", "fabricmc", "neoforge",
                    "injection", "listeners", "scheduler", "minecraft", "universal", "multipart", "neoforged", "microsoft",
                    "transformer", "transformers", "minecraftforge", "blockentity", "spongepowered", "electronwill"
                   }.Contains(word.ToLower) Then Continue For
                possibleWords.Add(word.Trim)
            Next
        Next
        possibleWords = possibleWords.Distinct.ToList
        Log("[Crash] 从堆栈信息中找到 " & possibleWords.Count & " 个可能的 Mod ID 关键词")
        If possibleWords.Any Then Log("[Crash]  - " & Join(possibleWords, ", "))
        If possibleWords.Count > 10 Then
            Log("[Crash] 关键词过多，考虑匹配出错，不纳入考虑")
            Return New List(Of String)
        End If
        Return possibleWords
    End Function

    Private Function AnalyzeModName(keywords As List(Of String)) As List(Of String)
        Dim modFileNames As New List(Of String)
        Dim realKeywords As New List(Of String)
        For Each keyword In keywords
            For Each subKeyword In keyword.Split("(")
                realKeywords.Add(subKeyword.Trim(" )".ToCharArray))
            Next
        Next
        keywords = realKeywords

        If LogCrash IsNot Nothing AndAlso LogCrash.Contains("A detailed walkthrough of the error") Then
            Dim details As String = LogCrash.Replace("A detailed walkthrough of the error", "¨")
            Dim isFabricDetail As Boolean = details.Contains("Fabric Mods")
            If isFabricDetail Then
                details = details.Replace("Fabric Mods", "¨")
                Log("[Crash] 崩溃报告中检测到 Fabric Mod 信息格式")
            End If
            Dim isQuiltDetail As Boolean = details.Contains("quilt-loader")
            If isQuiltDetail Then
                details = details.Replace("Mod Table Version", "¨")
                Log("[Crash] 崩溃报告中检测到 Quilt Mod 信息格式")
            End If
            details = details.AfterLast("¨")

            Dim modNameLines As New List(Of String)
            For Each line In details.Split(vbLf)
                If (line.ContainsF(".jar", True) AndAlso line.Length - line.Replace(".jar", "").Length = 4) OrElse
                   (isFabricDetail AndAlso line.StartsWithF(vbTab & vbTab) AndAlso Not RegexCheck(line, "\t\tfabric[\w-]*: Fabric")) Then modNameLines.Add(line)
            Next
            Log("[Crash] 崩溃报告中找到 " & modNameLines.Count & " 个可能的 Mod 项目行")

            Dim hintLines As New List(Of String)
            For Each keyword In keywords
                For Each modString In modNameLines
                    Dim realModString As String = modString.ToLower.Replace("_", "")
                    If Not realModString.Contains(keyword.ToLower.Replace("_", "")) Then Continue For
                    If realModString.Contains("minecraft.jar") OrElse realModString.Contains(" forge-") OrElse realModString.Contains(" mixin-") Then Continue For
                    hintLines.Add(modString.Trim(vbCrLf.ToCharArray))
                    Exit For
                Next
            Next
            hintLines = hintLines.Distinct.ToList
            Log("[Crash] 崩溃报告中找到 " & hintLines.Count & " 个可能的崩溃 Mod 匹配行")
            For Each modLine As String In hintLines
                Log("[Crash]  - " & modLine)
            Next

            For Each line As String In hintLines
                Dim name As String
                If isFabricDetail Then
                    name = RegexSeek(line, "(?<=: )[^\n]+(?= [^\n]+)")
                Else
                    name = RegexSeek(line, "(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar", RegularExpressions.RegexOptions.IgnoreCase)
                End If
                If name IsNot Nothing Then modFileNames.Add(name)
            Next
        End If

        If LogMcDebug IsNot Nothing Then
            Dim modNameLines As List(Of String) = RegexSearch(LogMcDebug, "(?<=valid mod file ).*", RegularExpressions.RegexOptions.Multiline)
            Log("[Crash] Debug 信息中找到 " & modNameLines.Count & " 个可能的 Mod 项目行")

            Dim hintLines As New List(Of String)
            For Each keyword In keywords
                For Each modString In modNameLines
                    If modString.Contains($"{{{keyword}}}") Then hintLines.Add(modString)
                Next
            Next
            hintLines = hintLines.Distinct.ToList
            Log("[Crash] Debug 信息中找到 " & hintLines.Count & " 个可能的崩溃 Mod 匹配行")
            For Each modLine As String In hintLines
                Log("[Crash]  - " & modLine)
            Next

            For Each line As String In hintLines
                Dim name As String = RegexSeek(line, ".*(?= with)")
                If name IsNot Nothing Then modFileNames.Add(name)
            Next
        End If

        modFileNames = modFileNames.Distinct.ToList
        If Not modFileNames.Any() Then Return Nothing
        Log("[Crash] 找到 " & modFileNames.Count & " 个可能的崩溃 Mod 文件名")
        For Each modFileName As String In modFileNames
            Log("[Crash]  - " & modFileName)
        Next
        Return modFileNames
    End Function

    Private Function TryAnalyzeModName(keyword As String) As List(Of String)
        Dim rawList As New List(Of String) From {If(keyword, "")}
        If String.IsNullOrEmpty(keyword) Then Return rawList
        Return If(AnalyzeModName(rawList), rawList)
    End Function

    Private Function TryAnalyzeModName(keywords As List(Of String)) As List(Of String)
        If Not keywords.Any Then Return keywords
        Return If(AnalyzeModName(keywords), keywords)
    End Function

End Class
