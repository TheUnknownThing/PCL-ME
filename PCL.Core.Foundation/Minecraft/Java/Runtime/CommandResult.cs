namespace PCL.Core.Minecraft.Java.Runtime;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
