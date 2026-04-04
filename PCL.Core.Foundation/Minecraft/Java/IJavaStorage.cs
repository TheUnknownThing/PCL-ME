namespace PCL.Core.Minecraft.Java;

public interface IJavaStorage
{
    JavaStorageItem[] Load();
    void Save(JavaStorageItem[] items);
}
