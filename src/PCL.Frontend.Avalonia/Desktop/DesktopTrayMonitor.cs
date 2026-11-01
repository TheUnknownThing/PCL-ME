using Avalonia.Threading;
using PCL.Core.Logging;
using Tmds.DBus.Protocol;

namespace PCL.Frontend.Avalonia.Desktop;

/// <summary>Tracks Linux tray hosts without polling or assuming that IsVisible means a tray exists.</summary>
internal sealed class DesktopTrayMonitor(Action<bool> availabilityChanged) : IDisposable
{
    private const string Watcher = "org.kde.StatusNotifierWatcher";
    private Connection? _connection;
    private IDisposable? _subscription;
    private bool _disposed;

    public async Task StartAsync()
    {
        try
        {
            if (_disposed || Address.Session is not { } address) return;
            var connection = new Connection(address);
            _connection = connection;
            await connection.ConnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            if (_disposed) return;
            _subscription = await connection.AddMatchAsync(
                new MatchRule
                {
                    Type = MessageType.Signal,
                    Sender = "org.freedesktop.DBus",
                    Path = "/org/freedesktop/DBus",
                    Interface = "org.freedesktop.DBus",
                    Member = "NameOwnerChanged",
                    Arg0 = Watcher
                },
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    reader.ReadString();
                    reader.ReadString();
                    return reader.ReadString().Length > 0;
                },
                (exception, available, _, _) => Publish(exception is null && available),
                flags: ObserverFlags.EmitOnConnectionDispose,
                emitOnCapturedContext: false).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            if (_disposed)
            {
                _subscription.Dispose();
                return;
            }
            var available = await connection.CallMethodAsync(
                CreateQuery(connection), static (message, _) => message.GetBodyReader().ReadBool())
                .WaitAsync(TimeSpan.FromSeconds(3));
            Publish(available);
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                LogWrapper.Warn(ex, "Desktop", "No Linux tray host is available; keeping the launcher recoverable.");
                Publish(false);
            }
            _subscription?.Dispose();
            _connection?.Dispose();
        }
    }

    private static MessageBuffer CreateQuery(Connection connection)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: "org.freedesktop.DBus",
            path: "/org/freedesktop/DBus",
            @interface: "org.freedesktop.DBus",
            member: "NameHasOwner",
            signature: "s");
        writer.WriteString(Watcher);
        return writer.CreateMessage();
    }

    private void Publish(bool available) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed) availabilityChanged(available);
    });

    public void Dispose()
    {
        _disposed = true;
        _subscription?.Dispose();
        _connection?.Dispose();
    }
}
