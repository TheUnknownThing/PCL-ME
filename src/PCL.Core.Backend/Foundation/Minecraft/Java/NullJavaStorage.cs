namespace PCL.Core.Minecraft.Java;

public sealed class NullJavaStorage : IJavaStorage
{
    public static NullJavaStorage Instance { get; } = new();

    public JavaStorageItem[] Load() => [];
    public void Save(JavaStorageItem[] items) { }
}
