using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private bool _lobbyRuntimeBridgeInitialized;
    private DispatcherTimer? _lobbyRuntimePollTimer;
    private LobbyRuntimeProxy? _lobbyRuntimeProxy;

    private LobbyRuntimeProxy LobbyRuntime =>
        _lobbyRuntimeProxy ??= new LobbyRuntimeProxy(_shellActionService.RuntimePaths.ExecutableDirectory, Environment.CurrentDirectory);

    private void InitializeLobbyRuntimeBridge()
    {
        if (_lobbyRuntimeBridgeInitialized)
        {
            return;
        }

        _lobbyRuntimeBridgeInitialized = true;
        LobbyRuntime.SubscribeCallbacks(
            OnNeedDownloadEasyTier,
            OnLobbyUserStopGame,
            OnLobbyClientPing,
            OnServerStarted,
            OnLobbyServerShutDown,
            OnLobbyServerException);

        _lobbyRuntimePollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _lobbyRuntimePollTimer.Tick += (_, _) => SyncLobbyRuntimeState(preserveTypedLobbyId: true);
        _lobbyRuntimePollTimer.Start();
    }

    private void OnNeedDownloadEasyTier()
    {
        Dispatcher.UIThread.Post(() =>
        {
            GameLinkAnnouncement = "检测到缺少 EasyTier 运行时文件，请先完成下载或补齐运行时后再试。";
            AddActivity("联机大厅", "当前运行环境缺少 EasyTier 运行时文件。");
        });
    }

    private void OnLobbyUserStopGame()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddActivity("联机大厅", "检测到房主已经关闭了局域网世界，会话已准备退出。");
            SyncLobbyRuntimeState(preserveTypedLobbyId: false);
        });
    }

    private void OnLobbyClientPing(long latency)
    {
        Dispatcher.UIThread.Post(() =>
        {
            GameLinkSessionPing = $"{Math.Max(0, latency)}ms";
            RaisePropertyChanged(nameof(GameLinkSessionPing));
        });
    }

    private void OnServerStarted()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddActivity("联机大厅", "EasyTier / Scaffolding 运行时已启动。");
            SyncLobbyRuntimeState(preserveTypedLobbyId: false);
        });
    }

    private void OnLobbyServerShutDown()
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddActivity("联机大厅", "大厅运行时已停止。");
            SyncLobbyRuntimeState(preserveTypedLobbyId: false);
        });
    }

    private void OnLobbyServerException(Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddActivity("联机大厅失败", ex.Message);
            SyncLobbyRuntimeState(preserveTypedLobbyId: true);
        });
    }

    private async Task EnsureLobbyServiceInitializedAsync(bool refreshWorlds)
    {
        InitializeLobbyRuntimeBridge();

        if (!LobbyRuntime.IsAvailable)
        {
            throw new InvalidOperationException(LobbyRuntime.UnavailableReason ?? "当前环境没有可用的联机运行时。");
        }

        await LobbyRuntime.InitializeAsync().ConfigureAwait(false);
        if (refreshWorlds && IsLobbyState(LobbyRuntime.CurrentState, "Initialized", "Idle"))
        {
            await LobbyRuntime.DiscoverWorldAsync().ConfigureAwait(false);
        }
    }

    private void SyncLobbyRuntimeState(bool preserveTypedLobbyId)
    {
        InitializeLobbyRuntimeBridge();

        var runtime = LobbyRuntime;
        var runtimeWorlds = runtime.GetDiscoveredWorlds()
            .Select(world => $"{world.Name} - {world.Port}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _gameLinkWorldOptions = runtimeWorlds.Length > 0
            ? runtimeWorlds
            : _toolsComposition.GameLink.WorldOptions.Count > 0
                ? _toolsComposition.GameLink.WorldOptions
                : ["未检测到可用存档"];
        _selectedGameLinkWorldIndex = Math.Clamp(_selectedGameLinkWorldIndex, 0, _gameLinkWorldOptions.Count - 1);

        GameLinkAccountStatus = ResolveNatayarkAccountStatus();

        var runtimeUser = string.IsNullOrWhiteSpace(runtime.CurrentUserName)
            ? ResolveGameLinkDisplayUserName()
            : runtime.CurrentUserName.Trim();
        GameLinkConnectedUserName = runtimeUser;
        GameLinkConnectedUserType = IsLobbyState(runtime.CurrentState, "Connected")
            ? runtime.IsHost ? "大厅房主" : "大厅访客"
            : !string.IsNullOrWhiteSpace(LinkUsername)
                ? "已配置显示名"
                : "大厅访客";

        var runtimeLobbyCode = runtime.CurrentLobbyCode?.Trim();
        _gameLinkSessionIsHost = runtime.IsHost;
        _gameLinkSessionPort = runtime.ResolveRuntimeLobbyPort();
        if (!string.IsNullOrWhiteSpace(runtimeLobbyCode))
        {
            GameLinkLobbyId = runtimeLobbyCode;
            GameLinkSessionId = runtimeLobbyCode;
        }
        else
        {
            GameLinkSessionId = "尚未创建大厅";
            if (!preserveTypedLobbyId)
            {
                GameLinkLobbyId = string.Empty;
            }
        }

        if (!IsLobbyState(runtime.CurrentState, "Connected"))
        {
            GameLinkSessionPing = "-ms";
        }

        GameLinkConnectionType = ResolveLobbyConnectionType();
        GameLinkAnnouncement = ResolveLobbyAnnouncement(runtimeWorlds.Length);
        ReplaceItems(GameLinkPlayerEntries, BuildLobbyPlayerEntries(runtimeUser));
        RaiseToolsGameLinkProperties();
    }

    private string ResolveNatayarkAccountStatus()
    {
        var natayarkUserName = LobbyRuntime.NatayarkUsername?.Trim();
        if (!string.IsNullOrWhiteSpace(natayarkUserName))
        {
            return natayarkUserName;
        }

        if (HasStoredNatayarkRefreshToken())
        {
            return "正在同步 Natayark 账户";
        }

        return "点击登录 Natayark 账户";
    }

    private bool HasStoredNatayarkRefreshToken()
    {
        var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
        if (!provider.Exists("LinkNaidRefreshToken"))
        {
            return false;
        }

        try
        {
            return !string.IsNullOrWhiteSpace(provider.Get<string>("LinkNaidRefreshToken"));
        }
        catch
        {
            return false;
        }
    }

    private string ResolveLobbyConnectionType()
    {
        return LobbyRuntime.CurrentState switch
        {
            "Initializing" => "正在初始化联机服务",
            "Discovering" => "正在扫描局域网世界",
            "Creating" => "正在创建 EasyTier 大厅",
            "Joining" => "正在加入 EasyTier 大厅",
            "Connected" => LobbyRuntime.IsHost ? "EasyTier 主机会话" : "EasyTier 加入会话",
            "Leaving" => "正在退出大厅",
            "Error" => "联机服务异常",
            _ => HasAcceptedGameLinkTerms() ? "未连接" : "等待授权"
        };
    }

    private string ResolveLobbyAnnouncement(int runtimeWorldCount)
    {
        if (!HasAcceptedGameLinkTerms())
        {
            return "请先阅读并同意联机大厅说明与条款。";
        }

        return LobbyRuntime.CurrentState switch
        {
            "Unavailable" => LobbyRuntime.UnavailableReason ?? "当前环境没有可用的联机运行时。",
            "Initializing" => "正在初始化联机运行时，请稍候。",
            "Discovering" => "正在扫描当前设备上的局域网世界。",
            "Creating" => "正在创建大厅并启动 EasyTier / Scaffolding 运行时。",
            "Joining" => string.IsNullOrWhiteSpace(GameLinkLobbyId.Trim())
                ? "正在加入大厅。"
                : $"正在加入大厅 {GameLinkLobbyId.Trim()}。",
            "Connected" => LobbyRuntime.IsHost
                ? $"大厅已创建完成，当前可见 {Math.Max(1, GameLinkPlayerEntries.Count)} 名成员。"
                : $"已加入大厅 {GameLinkSessionId}，可以直接进入多人游戏中的局域网列表。",
            "Leaving" => "正在退出当前大厅。",
            "Error" => "联机运行时出现错误，请查看活动记录或日志后重试。",
            _ => runtimeWorldCount > 0
                ? $"已检测到 {runtimeWorldCount} 个可用于创建大厅的世界。"
                : "尚未检测到可用于创建大厅的世界，请先在 Minecraft 中打开局域网端口。"
        };
    }

    private IReadOnlyList<SimpleListEntryViewModel> BuildLobbyPlayerEntries(string runtimeUser)
    {
        var players = LobbyRuntime.GetPlayers();
        if (players.Count > 0)
        {
            return players.Select(player =>
                    new SimpleListEntryViewModel(
                        player.Name,
                        DescribeLobbyPlayer(player, runtimeUser),
                        new ActionCommand(() => AddActivity("查看大厅成员", player.Name))))
                .ToArray();
        }

        if (IsLobbyState(LobbyRuntime.CurrentState, "Connected"))
        {
            if (LobbyRuntime.IsHost)
            {
                return
                [
                    new SimpleListEntryViewModel(runtimeUser, "大厅房主 • 运行时已就绪", new ActionCommand(() => AddActivity("查看大厅成员", runtimeUser))),
                    new SimpleListEntryViewModel("等待好友加入", "大厅成员槽位 • 空闲", new ActionCommand(() => AddActivity("查看大厅成员", "等待好友加入")))
                ];
            }

            return
            [
                new SimpleListEntryViewModel(runtimeUser, $"当前设备 • 已加入大厅 • 延迟 {GameLinkSessionPing}", new ActionCommand(() => AddActivity("查看大厅成员", runtimeUser)))
            ];
        }

        return [];
    }

    private static string DescribeLobbyPlayer(LobbyPlayerSnapshot player, string runtimeUser)
    {
        if (string.Equals(player.Kind, "HOST", StringComparison.OrdinalIgnoreCase))
        {
            return "大厅房主 • 在线";
        }

        return string.Equals(player.Name, runtimeUser, StringComparison.Ordinal)
            ? "当前设备 • 在线"
            : "大厅成员 • 在线";
    }

    private static bool IsLobbyState(string state, params string[] values)
    {
        return values.Any(value => string.Equals(state, value, StringComparison.Ordinal));
    }

    private sealed class LobbyRuntimeProxy(string executableDirectory, string currentDirectory)
    {
        private readonly object _syncRoot = new();
        private readonly string _executableDirectory = executableDirectory;
        private readonly string _currentDirectory = currentDirectory;
        private readonly List<(EventInfo Event, Delegate Handler)> _subscriptions = [];
        private Assembly? _assembly;
        private Type? _lobbyServiceType;
        private Type? _lobbyInfoProviderType;
        private Type? _natayarkProfileManagerType;
        private bool _loadAttempted;
        private bool _callbacksSubscribed;

        public bool IsAvailable
        {
            get
            {
                EnsureLoaded();
                return _lobbyServiceType is not null;
            }
        }

        public string? UnavailableReason { get; private set; }

        public string CurrentState => GetStaticPropertyValue(_lobbyServiceType, "CurrentState")?.ToString() ?? "Unavailable";

        public string? CurrentLobbyCode => GetStaticPropertyValue(_lobbyServiceType, "CurrentLobbyCode")?.ToString();

        public string? CurrentUserName => GetStaticPropertyValue(_lobbyServiceType, "CurrentUserName")?.ToString();

        public bool IsHost => ReadBool(GetStaticPropertyValue(_lobbyServiceType, "IsHost"));

        public string? NatayarkUsername
        {
            get
            {
                var profile = GetStaticPropertyValue(_natayarkProfileManagerType, "NaidProfile");
                return ReadString(profile, "Username");
            }
        }

        public void SubscribeCallbacks(
            Action onNeedDownloadEasyTier,
            Action onUserStopGame,
            Action<long> onClientPing,
            Action onServerStarted,
            Action onServerShutDown,
            Action<Exception> onServerException)
        {
            EnsureLoaded();
            if (_callbacksSubscribed || _lobbyServiceType is null)
            {
                return;
            }

            _callbacksSubscribed = true;
            SubscribeSimpleEvent("OnNeedDownloadEasyTier", onNeedDownloadEasyTier);
            SubscribeSimpleEvent("OnUserStopGame", onUserStopGame);
            SubscribeSimpleEvent("OnClientPing", onClientPing);
            SubscribeSimpleEvent("OnServerStarted", onServerStarted);
            SubscribeSimpleEvent("OnServerShutDown", onServerShutDown);
            SubscribeSimpleEvent("OnServerException", onServerException);
        }

        public async Task InitializeAsync()
        {
            await InvokeTaskAsync("InitializeAsync").ConfigureAwait(false);
        }

        public async Task DiscoverWorldAsync()
        {
            await InvokeTaskAsync("DiscoverWorldAsync").ConfigureAwait(false);
        }

        public async Task<bool> CreateLobbyAsync(int port, string username)
        {
            return await InvokeTaskAsync<bool>("CreateLobbyAsync", port, username).ConfigureAwait(false);
        }

        public async Task<bool> JoinLobbyAsync(string lobbyCode, string username)
        {
            return await InvokeTaskAsync<bool>("JoinLobbyAsync", lobbyCode, username).ConfigureAwait(false);
        }

        public async Task LeaveLobbyAsync()
        {
            await InvokeTaskAsync("LeaveLobbyAsync").ConfigureAwait(false);
        }

        public IReadOnlyList<LobbyWorldSnapshot> GetDiscoveredWorlds()
        {
            var collection = GetStaticPropertyValue(_lobbyServiceType, "DiscoveredWorlds") as System.Collections.IEnumerable;
            if (collection is null)
            {
                return [];
            }

            var worlds = new List<LobbyWorldSnapshot>();
            foreach (var item in collection)
            {
                if (item is null)
                {
                    continue;
                }

                var name = ReadString(item, "Name");
                var port = ReadInt(item, "Port");
                if (!string.IsNullOrWhiteSpace(name) && port > 0)
                {
                    worlds.Add(new LobbyWorldSnapshot(name, port));
                }
            }

            return worlds;
        }

        public IReadOnlyList<LobbyPlayerSnapshot> GetPlayers()
        {
            var collection = GetStaticPropertyValue(_lobbyServiceType, "Players") as System.Collections.IEnumerable;
            if (collection is null)
            {
                return [];
            }

            var players = new List<LobbyPlayerSnapshot>();
            foreach (var item in collection)
            {
                if (item is null)
                {
                    continue;
                }

                var name = ReadString(item, "Name");
                var machineId = ReadString(item, "MachineId");
                var kind = ReadObject(item, "Kind")?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    players.Add(new LobbyPlayerSnapshot(name, machineId, kind));
                }
            }

            return players;
        }

        public int ResolveRuntimeLobbyPort()
        {
            var mcForward = GetStaticPropertyValue(_lobbyInfoProviderType, "McForward");
            var localPort = ReadInt(mcForward, "LocalPort");
            if (localPort > 0)
            {
                return localPort;
            }

            var joinerLocalPort = ReadInt(GetStaticPropertyValue(_lobbyInfoProviderType, "JoinerLocalPort"));
            if (joinerLocalPort > 0)
            {
                return joinerLocalPort;
            }

            var targetLobby = GetStaticPropertyValue(_lobbyInfoProviderType, "TargetLobby");
            var targetPort = ReadInt(targetLobby, "Port");
            return targetPort > 0 ? targetPort : 25565;
        }

        private void EnsureLoaded()
        {
            if (_loadAttempted)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_loadAttempted)
                {
                    return;
                }

                _loadAttempted = true;

                try
                {
                    _assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "PCL.Core", StringComparison.Ordinal))
                        ?? LoadPclCoreAssembly();
                    if (_assembly is null)
                    {
                        UnavailableReason = "未找到 PCL.Core 联机运行时程序集，请先构建或分发 PCL.Core.dll。";
                        return;
                    }

                    _lobbyServiceType = _assembly.GetType("PCL.Core.Link.Lobby.LobbyService", throwOnError: false);
                    _lobbyInfoProviderType = _assembly.GetType("PCL.Core.Link.Lobby.LobbyInfoProvider", throwOnError: false);
                    _natayarkProfileManagerType = _assembly.GetType("PCL.Core.Link.Natayark.NatayarkProfileManager", throwOnError: false);
                    if (_lobbyServiceType is null || _lobbyInfoProviderType is null || _natayarkProfileManagerType is null)
                    {
                        UnavailableReason = "PCL.Core 联机运行时程序集缺少大厅所需的类型定义。";
                    }
                }
                catch (Exception ex)
                {
                    UnavailableReason = $"加载 PCL.Core 联机运行时失败：{ex.Message}";
                }
            }
        }

        private Assembly? LoadPclCoreAssembly()
        {
            foreach (var candidate in EnumerateCandidateAssemblyPaths())
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                }
                catch
                {
                    // Try the next candidate path.
                }
            }

            return null;
        }

        private IEnumerable<string> EnumerateCandidateAssemblyPaths()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in EnumerateCandidateRoots())
            {
                var directPath = Path.Combine(root, "PCL.Core.dll");
                if (seen.Add(directPath))
                {
                    yield return directPath;
                }

                var binDirectory = Path.Combine(root, "PCL.Core", "bin");
                if (!Directory.Exists(binDirectory))
                {
                    continue;
                }

                foreach (var buildPath in Directory.EnumerateFiles(binDirectory, "PCL.Core.dll", SearchOption.AllDirectories)
                             .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                             .Take(8))
                {
                    if (seen.Add(buildPath))
                    {
                        yield return buildPath;
                    }
                }
            }
        }

        private IEnumerable<string> EnumerateCandidateRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in new[] { _executableDirectory, _currentDirectory })
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                var current = new DirectoryInfo(Path.GetFullPath(root));
                while (current is not null && seen.Add(current.FullName))
                {
                    yield return current.FullName;
                    current = current.Parent;
                }
            }
        }

        private void SubscribeSimpleEvent(string eventName, Delegate handler)
        {
            if (_lobbyServiceType is null)
            {
                return;
            }

            var eventInfo = _lobbyServiceType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo is null)
            {
                return;
            }

            eventInfo.AddEventHandler(null, handler);
            _subscriptions.Add((eventInfo, handler));
        }

        private async Task InvokeTaskAsync(string methodName, params object?[]? arguments)
        {
            EnsureLoaded();
            if (_lobbyServiceType is null)
            {
                throw new InvalidOperationException(UnavailableReason ?? "当前环境没有可用的联机运行时。");
            }

            var method = _lobbyServiceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(_lobbyServiceType.FullName, methodName);
            var task = method.Invoke(null, arguments) as Task
                ?? throw new InvalidOperationException($"{methodName} 没有返回可等待的任务。");
            await task.ConfigureAwait(false);
        }

        private async Task<TResult> InvokeTaskAsync<TResult>(string methodName, params object?[]? arguments)
        {
            EnsureLoaded();
            if (_lobbyServiceType is null)
            {
                throw new InvalidOperationException(UnavailableReason ?? "当前环境没有可用的联机运行时。");
            }

            var method = _lobbyServiceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(_lobbyServiceType.FullName, methodName);
            var taskObject = method.Invoke(null, arguments)
                ?? throw new InvalidOperationException($"{methodName} 没有返回任务结果。");
            if (taskObject is not Task task)
            {
                throw new InvalidOperationException($"{methodName} 没有返回可等待的任务。");
            }

            await task.ConfigureAwait(false);
            var resultProperty = taskObject.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            var result = resultProperty?.GetValue(taskObject);
            return result is TResult typedResult ? typedResult : default!;
        }

        private static object? GetStaticPropertyValue(Type? ownerType, string propertyName)
        {
            return ownerType?
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null);
        }

        private static object? ReadObject(object owner, string propertyName)
        {
            return owner.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
        }

        private static string ReadString(object owner, string propertyName)
        {
            return ReadObject(owner, propertyName)?.ToString() ?? string.Empty;
        }

        private static int ReadInt(object? owner, string propertyName)
        {
            if (owner is null)
            {
                return 0;
            }

            return ReadInt(ReadObject(owner, propertyName));
        }

        private static int ReadInt(object? value)
        {
            if (value is null)
            {
                return 0;
            }

            return value switch
            {
                int typedInt => typedInt,
                long typedLong => (int)typedLong,
                _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
            };
        }

        private static bool ReadBool(object? value)
        {
            if (value is null)
            {
                return false;
            }

            return value switch
            {
                bool typedBool => typedBool,
                _ => bool.TryParse(value.ToString(), out var parsed) && parsed
            };
        }
    }

    private sealed record LobbyWorldSnapshot(string Name, int Port);

    private sealed record LobbyPlayerSnapshot(string Name, string MachineId, string Kind);
}
