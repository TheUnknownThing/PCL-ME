using System.Text.Json;
using PCL.Core.App;

namespace PCL.Core.Minecraft.Java;

internal sealed class StatesJavaStorage : IJavaStorage
{
    public JavaStorageItem[] Load()
    {
        return JsonSerializer.Deserialize<JavaStorageItem[]>(States.Game.JavaList) ?? [];
    }

    public void Save(JavaStorageItem[] items)
    {
        States.Game.JavaList = JsonSerializer.Serialize(items);
    }
}
