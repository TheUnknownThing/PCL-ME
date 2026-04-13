namespace PCL.Core.Minecraft.Java.Runtime;

public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments);
}
