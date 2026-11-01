using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace PCL.Frontend.Avalonia.E2E;

// Substitute only the OS picker boundary; package installation still uses the real UI and runtime.
public class PickerProvider : DispatchProxy
{
    public Window? Dialog { get; private set; }
    public IStorageProvider Service => (IStorageProvider)(object)this;
    public static PickerProvider Create() => (PickerProvider)DispatchProxy.Create<IStorageProvider, PickerProvider>();
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method!.Name switch
    {
        "get_CanOpen" => true,
        "get_CanSave" or "get_CanPickFolder" => false,
        "OpenFilePickerAsync" => OpenFilePickerAsync((FilePickerOpenOptions)arguments![0]!),
        "Dispose" => null,
        _ => throw new NotSupportedException(method.Name)
    };
    public async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
    {
        var dialog = Dialog = new Window { Title = options.Title, Width = 460, Height = 180 };
        var path = new TextBox { Name = "PickerPath" };
        var open = new Button { Name = "PickerOpen", Content = "Open" };
        var cancel = new Button { Name = "PickerCancel", Content = "Cancel" };
        var selected = false;
        open.Click += (_, _) => { selected = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { open, cancel } };
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 15, Children = { path, buttons } };
        dialog.Show();
        var completion = new TaskCompletionSource();
        dialog.Closed += (_, _) => completion.TrySetResult();
        await completion.Task;
        Dialog = null;
        return selected && !string.IsNullOrWhiteSpace(path.Text) ? [LocalFile.Create(path.Text)] : [];
    }
    public class LocalFile : DispatchProxy
    {
        private string _path = "";
        public static IStorageFile Create(string path)
        {
            var file = DispatchProxy.Create<IStorageFile, LocalFile>();
            ((LocalFile)file)._path = path;
            return file;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method!.Name switch
        {
            "get_Name" => System.IO.Path.GetFileName(_path),
            "get_Path" => new Uri(System.IO.Path.GetFullPath(_path)),
            "get_CanBookmark" => false,
            "Dispose" => null,
            _ => throw new NotSupportedException(method.Name)
        };
    }
}
