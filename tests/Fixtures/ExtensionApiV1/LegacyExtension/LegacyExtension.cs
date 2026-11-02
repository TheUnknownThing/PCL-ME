using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Controls;
using PCL.UI.Commands;

namespace LegacyExtension;

public sealed class LegacyExtension : IExtension
{
    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        var ui = context.GetService<IExtensionUi>();
        ui.RegisterView(new() { Id = "example.hello.page", Title = "Hello", TitleKey = "title" }, _ =>
        {
            var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
            panel.Children.Add(new TextBlock { Text = context.Settings.Get("greeting", "Hello from an extension!"), FontSize = 24 });
            var button = new PclButton { Text = context.Text.Get("notify"), Command = new ActionCommand(() => context.Launcher.Notify(context.Text.Get("message"))) };
            panel.Children.Add(button);
            return new PclCard { Header = context.Text.Get("title"), CardContent = panel, Margin = new Thickness(24) };
        });
        ui.RegisterView(new() { Id = "example.hello.instance", Title = "Extension information", Location = ExtensionViewLocation.InstanceTab }, view =>
            new TextBlock { Text = view.InstanceId, Margin = new(16) });
        context.Commands.Register(context.Manifest.Commands.Single(), token => ui.ShowViewAsync("example.hello.page", cancellationToken: token));
        ui.RegisterAction(new("example.hello.instance-menu", "Hello", "example.hello.open", ExtensionActionLocation.InstanceMenu, "title"));
        context.Search.Register("example.hello.search", (query, token) => ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(
            "hello".Contains(query.Text, StringComparison.OrdinalIgnoreCase)
                ? [new() { Id = "hello", Kind = ExtensionSearchKind.Page, Score = .8, Title = context.Text.Get("title"), CommandId = "example.hello.open" }] : []));
        context.Events.Subscribe("launcher.instance.changed", message => context.Log(ExtensionLogLevel.Information, "Selected instance: " + message.Data));
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class HelloWorker : IExtensionWorkerHandler
{
    public async ValueTask<JsonElement> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken)
    {
        if (method == "delay") await Task.Delay(payload.GetInt32(), cancellationToken);
        return JsonSerializer.SerializeToElement(new { method, payload });
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
