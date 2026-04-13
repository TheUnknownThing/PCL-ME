namespace PCL.Core.Minecraft.Java;

public interface IJavaInstallationEvaluator
{
    bool ShouldEnableByDefault(JavaInstallation installation);
}
