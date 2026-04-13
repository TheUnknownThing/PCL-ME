Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Launch
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts

Public Module ModLaunchPrecheckShell

    Public Function EvaluatePrecheck(instance As McInstance,
                                     selectedProfile As McProfile,
                                     profileList As List(Of McProfile),
                                     checkResult As String) As MinecraftLaunchPrecheckResult
        Return MinecraftLaunchPrecheckService.Evaluate(
            New MinecraftLaunchPrecheckRequest(
                If(instance?.Name, ""),
                If(instance?.PathIndie, ""),
                If(instance?.PathInstance, ""),
                instance IsNot Nothing,
                instance?.State = McInstanceState.Error,
                If(instance?.Desc, ""),
                IsUtf8CodePage(),
                Setup.Get("HintDisableGamePathCheckTip"),
                If(instance Is Nothing, True, instance.PathInstance.IsASCII()),
                checkResult,
                ModLaunchProfileShell.GetCurrentProfileKind(selectedProfile),
                instance IsNot Nothing AndAlso instance.Info.HasLabyMod,
                If(instance Is Nothing, MinecraftLaunchLoginRequirement.None, CType(Setup.Get("VersionServerLoginRequire", instance), MinecraftLaunchLoginRequirement)),
                If(instance Is Nothing, Nothing, Setup.Get("VersionServerAuthServer", instance)),
                ModLaunchProfileShell.GetSelectedAuthServerBase(selectedProfile),
                If(profileList, New List(Of McProfile)).Any(Function(x) x.Type = McLoginType.Ms),
                RegionUtils.IsRestrictedFeatAllowed))
    End Function

End Module
