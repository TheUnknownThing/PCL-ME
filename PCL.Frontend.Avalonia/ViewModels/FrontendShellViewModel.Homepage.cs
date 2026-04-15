using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string HomepageCacheUrlKey = "CacheSavedPageUrl";
    private IReadOnlyList<HomepagePresetDefinition> HomepagePresetCatalog =>
    [
        new(T("launch.homepage.presets.random_hint"), HomepagePresetKind.RandomHint, null),
        new(T("launch.homepage.presets.minecraft_news"), HomepagePresetKind.RemoteMarkup, "https://pcl.mcnews.thestack.top"),
        new(T("launch.homepage.presets.simple_homepage"), HomepagePresetKind.RemoteMarkup, "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/MFn233/Custom.xaml"),
        new(T("launch.homepage.presets.daily_modpacks"), HomepagePresetKind.RemoteMarkup, "https://pclsub.sodamc.com/"),
        new(T("launch.homepage.presets.skin_recommendations"), HomepagePresetKind.RemoteMarkup, "https://forgepixel.com/pcl_sub_file"),
        new(T("launch.homepage.presets.openbmclapi_dashboard"), HomepagePresetKind.RemoteMarkup, "https://pcl-bmcl.milu.ink/"),
        new(T("launch.homepage.presets.news_flash"), HomepagePresetKind.RemoteMarkup, "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Joker2184/UpdateHomepage.xaml"),
        new(T("launch.homepage.presets.whats_new"), HomepagePresetKind.RemoteMarkup, "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml"),
        new(T("launch.homepage.presets.magazine"), HomepagePresetKind.RemoteMarkup, "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml"),
        new(T("launch.homepage.presets.github_dashboard"), HomepagePresetKind.RemoteMarkup, "https://ddf.pcl-community.org/Custom.xaml"),
        new(T("launch.homepage.presets.update_summary"), HomepagePresetKind.RemoteMarkup, "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml"),
        new(T("launch.homepage.presets.announcement_board"), HomepagePresetKind.RemoteMarkup, "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml"),
        new(T("launch.homepage.presets.official_news"), HomepagePresetKind.OfficialNews, null)
    ];
    private CancellationTokenSource? _homepageRefreshCts;
    private int _homepageRefreshVersion;
    private bool _isLaunchHomepageLoading;
    private string _launchHomepageStatusText = string.Empty;

    public ObservableCollection<LaunchHomepageSectionViewModel> LaunchHomepageSections { get; } = [];

    public bool HasLaunchHomepageSections => LaunchHomepageSections.Count > 0;

    public bool ShowLaunchHomepageStatusCard =>
        (IsLaunchHomepageLoading && !HasLaunchHomepageSections) ||
        (!IsLaunchHomepageLoading && !HasLaunchHomepageSections && !string.IsNullOrWhiteSpace(LaunchHomepageStatusText));

    public bool IsLaunchHomepageLoading
    {
        get => _isLaunchHomepageLoading;
        private set
        {
            if (SetProperty(ref _isLaunchHomepageLoading, value))
            {
                RaisePropertyChanged(nameof(ShowLaunchHomepageStatusCard));
                RaisePropertyChanged(nameof(LaunchHomepageStatusTitle));
                RaisePropertyChanged(nameof(ShowLaunchHomepageRefreshAction));
            }
        }
    }

    public string LaunchHomepageStatusTitle => IsLaunchHomepageLoading
        ? T("launch.homepage.status.loading_title")
        : T("launch.homepage.status.unavailable_title");

    public string LaunchHomepageStatusText
    {
        get => _launchHomepageStatusText;
        private set
        {
            if (SetProperty(ref _launchHomepageStatusText, value))
            {
                RaisePropertyChanged(nameof(ShowLaunchHomepageStatusCard));
            }
        }
    }

    public bool ShowLaunchHomepageRefreshAction => !IsLaunchHomepageLoading && SelectedHomepageTypeIndex != 0;

    private void RefreshLaunchHomepage(bool forceRefresh, bool addActivity = false)
    {
        _homepageRefreshCts?.Cancel();
        _homepageRefreshCts = new CancellationTokenSource();
        var refreshVersion = ++_homepageRefreshVersion;
        var cancellationToken = _homepageRefreshCts.Token;

        switch (SelectedHomepageTypeIndex)
        {
            case 0:
                ApplyHomepageSections([], isLoading: false, statusText: string.Empty);
                if (addActivity)
                {
                    AddActivity(T("launch.homepage.actions.refresh"), T("launch.homepage.activities.switched_to_blank"));
                }

                return;
            case 1:
                RefreshHomepagePreset(forceRefresh, addActivity, refreshVersion, cancellationToken);
                return;
            case 2:
                RefreshLocalHomepage(addActivity);
                return;
            default:
                RefreshRemoteHomepage(HomepageUrl, T("launch.homepage.sources.remote"), forceRefresh, addActivity, refreshVersion, cancellationToken);
                return;
        }
    }

    private void RefreshHomepagePreset(bool forceRefresh, bool addActivity, int refreshVersion, CancellationToken cancellationToken)
    {
        var preset = HomepagePresetCatalog[Math.Clamp(SelectedHomepagePresetIndex, 0, HomepagePresetCatalog.Count - 1)];
        switch (preset.Kind)
        {
            case HomepagePresetKind.RandomHint:
                ApplyHomepageSections(
                    [
                        new LaunchHomepageSectionViewModel(
                            T("launch.homepage.random_hint.title"),
                            string.Empty,
                            [GetRandomHomepageHint()],
                            [],
                            [CreateHomepageAction(
                                T("launch.homepage.random_hint.actions.next"),
                                T("launch.homepage.random_hint.actions.next_info"),
                                T("launch.homepage.actions.refresh"),
                                "/")])
                    ],
                    isLoading: false,
                    statusText: string.Empty);
                if (addActivity)
                {
                    AddActivity(
                        T("launch.homepage.actions.refresh"),
                        T("launch.homepage.activities.loaded_preset", ("preset_title", preset.Title)));
                }

                return;
            case HomepagePresetKind.OfficialNews:
                ApplyHomepageSections([], isLoading: true, statusText: T("launch.homepage.status.loading_official_news"));
                _ = LoadOfficialNewsHomepageAsync(refreshVersion, cancellationToken, addActivity, preset.Title);
                return;
            default:
                RefreshRemoteHomepage(preset.Target ?? string.Empty, preset.Title, forceRefresh, addActivity, refreshVersion, cancellationToken);
                return;
        }
    }

    private void RefreshLocalHomepage(bool addActivity)
    {
        var tutorialPath = GetHomepageTutorialPath();
        if (!File.Exists(tutorialPath))
        {
            ApplyHomepageSections(
                [],
                isLoading: false,
                statusText: T("launch.homepage.status.local_file_missing", ("path", tutorialPath)));
            if (addActivity)
            {
                AddFailureActivity(T("launch.homepage.activities.refresh_failed"), tutorialPath);
            }

            return;
        }

        try
        {
            var content = File.ReadAllText(tutorialPath);
            var origin = new HomepageContentOrigin(tutorialPath, BaseUri: null, BaseDirectory: Path.GetDirectoryName(tutorialPath));
            ApplyHomepageMarkup(content, origin, addActivity ? T("launch.homepage.sources.local") : null);
        }
        catch (Exception ex)
        {
            ApplyHomepageSections([], isLoading: false, statusText: ex.Message);
            if (addActivity)
            {
                AddFailureActivity(T("launch.homepage.activities.refresh_failed"), ex.Message);
            }
        }
    }

    private void RefreshRemoteHomepage(
        string rawUrl,
        string sourceName,
        bool forceRefresh,
        bool addActivity,
        int refreshVersion,
        CancellationToken cancellationToken)
    {
        var normalizedUrl = rawUrl.Trim();
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            ApplyHomepageSections([], isLoading: false, statusText: T("launch.homepage.status.invalid_remote_url"));
            if (addActivity)
            {
                AddFailureActivity(T("launch.homepage.activities.refresh_failed"), T("launch.homepage.status.invalid_remote_url"));
            }

            return;
        }

        var appliedCachedContent = false;
        if (!forceRefresh
            && string.Equals(ReadCachedHomepageUrl(), normalizedUrl, StringComparison.OrdinalIgnoreCase)
            && File.Exists(GetHomepageCacheFilePath()))
        {
            try
            {
                var cachedContent = File.ReadAllText(GetHomepageCacheFilePath());
                ApplyHomepageMarkup(cachedContent, new HomepageContentOrigin(sourceName, uri, BaseDirectory: null), successActivity: null);
                appliedCachedContent = HasLaunchHomepageSections;
            }
            catch
            {
                // Ignore cache read failures and continue with the network request.
            }
        }

        ApplyHomepageLoadingState(T("launch.homepage.status.loading_remote"), clearSections: !appliedCachedContent);
        _ = LoadRemoteHomepageAsync(uri, sourceName, refreshVersion, cancellationToken, addActivity);
    }

    private async Task LoadRemoteHomepageAsync(
        Uri uri,
        string sourceName,
        int refreshVersion,
        CancellationToken cancellationToken,
        bool addActivity)
    {
        try
        {
            using var client = CreateToolHttpClient();
            var content = await client.GetStringAsync(uri, cancellationToken);
            var cachePath = GetHomepageCacheFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, content, cancellationToken);
            _shellActionService.PersistLocalValue(HomepageCacheUrlKey, uri.ToString());

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsHomepageRefreshCurrent(refreshVersion))
                {
                    return;
                }

                ApplyHomepageMarkup(content, new HomepageContentOrigin(sourceName, uri, BaseDirectory: null), addActivity ? sourceName : null);
            });
        }
        catch (OperationCanceledException)
        {
            // A newer homepage refresh superseded this one.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsHomepageRefreshCurrent(refreshVersion))
                {
                    return;
                }

                if (!HasLaunchHomepageSections)
                {
                    ApplyHomepageSections([], isLoading: false, statusText: ex.Message);
                }
                else
                {
                    IsLaunchHomepageLoading = false;
                }

                if (addActivity)
                {
                    AddFailureActivity(T("launch.homepage.activities.refresh_failed"), ex.Message);
                }
            });
        }
    }

    private async Task LoadOfficialNewsHomepageAsync(
        int refreshVersion,
        CancellationToken cancellationToken,
        bool addActivity,
        string sourceName)
    {
        const string requestUrl = "https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search?pageSize=12&sortType=Recent&category=News&newsOnly=true&page=1";

        try
        {
            using var client = CreateToolHttpClient();
            using var stream = await client.GetStreamAsync(requestUrl, cancellationToken);
            var response = await JsonSerializer.DeserializeAsync<MinecraftNewsApiResponse>(stream, cancellationToken: cancellationToken);
            var sections = response?.Result?.Results?
                .Select(item =>
                {
                    var publishTime = DateTimeOffset.FromUnixTimeSeconds(item.Time).LocalDateTime;
                    var subtitle = string.Join(
                        "  •  ",
                        new[]
                        {
                            string.IsNullOrWhiteSpace(item.Author) ? T("launch.homepage.official_news.author") : item.Author.Trim(),
                            publishTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                        }.Where(segment => !string.IsNullOrWhiteSpace(segment)));
                    var imageEntries = string.IsNullOrWhiteSpace(item.Image)
                        ? Array.Empty<LaunchHomepageImageViewModel>()
                        : [CreateHomepageImage(item.Image, item.ImageAltText ?? item.Title, new HomepageContentOrigin(sourceName, new Uri(requestUrl), BaseDirectory: null))];
                    return new LaunchHomepageSectionViewModel(
                        item.Title,
                        subtitle,
                        string.IsNullOrWhiteSpace(item.Description) ? [] : [WebUtility.HtmlDecode(item.Description).Trim()],
                        imageEntries,
                        [CreateHomepageAction(
                            T("launch.homepage.actions.view_details"),
                            item.Url,
                            T("launch.homepage.actions.open_web"),
                            item.Url)]);
                })
                .ToArray() ?? [];

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsHomepageRefreshCurrent(refreshVersion))
                {
                    return;
                }

                ApplyHomepageSections(sections, isLoading: false, statusText: string.Empty);
                if (addActivity)
                {
                    AddActivity(
                        T("launch.homepage.actions.refresh"),
                        T("launch.homepage.activities.loaded_preset", ("preset_title", sourceName)));
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer homepage refresh superseded this one.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsHomepageRefreshCurrent(refreshVersion))
                {
                    return;
                }

                ApplyHomepageSections([], isLoading: false, statusText: ex.Message);
                if (addActivity)
                {
                    AddFailureActivity(T("launch.homepage.activities.refresh_failed"), ex.Message);
                }
            });
        }
    }

    private void ApplyHomepageMarkup(string content, HomepageContentOrigin origin, string? successActivity)
    {
        try
        {
            var sections = ParseHomepageSections(content, origin);
            ApplyHomepageSections(sections, isLoading: false, statusText: string.Empty);
            if (!string.IsNullOrWhiteSpace(successActivity))
            {
                AddActivity(
                    T("launch.homepage.actions.refresh"),
                    T("launch.homepage.activities.loaded_content", ("source_name", successActivity)));
            }
        }
        catch (Exception ex)
        {
            ApplyHomepageSections([], isLoading: false, statusText: ex.Message);
            if (!string.IsNullOrWhiteSpace(successActivity))
            {
                AddFailureActivity(T("launch.homepage.activities.refresh_failed"), ex.Message);
            }
        }
    }

    private IReadOnlyList<LaunchHomepageSectionViewModel> ParseHomepageSections(string content, HomepageContentOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var wrapped = WrapHelpDetailContent(content);
        var root = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace).Root
                   ?? throw new InvalidOperationException(T("launch.homepage.errors.missing_root"));
        var sections = new List<LaunchHomepageSectionViewModel>();
        var pendingLines = new List<string>();
        var pendingImages = new List<LaunchHomepageImageViewModel>();
        var pendingActions = new List<SimpleListEntryViewModel>();

        foreach (var child in root.Elements())
        {
            if (IsNamed(child, "MyCard"))
            {
                FlushHomepageSection(sections, pendingLines, pendingImages, pendingActions, T("launch.homepage.sections.default_title"));
                sections.Add(ParseHomepageCard(child, origin));
                continue;
            }

            ParseHomepageElement(child, origin, pendingLines, pendingImages, pendingActions);
        }

        FlushHomepageSection(sections, pendingLines, pendingImages, pendingActions, T("launch.homepage.sections.default_title"));
        return sections;
    }

    private LaunchHomepageSectionViewModel ParseHomepageCard(XElement card, HomepageContentOrigin origin)
    {
        var title = ReadAttribute(card, "Title");
        var lines = new List<string>();
        var images = new List<LaunchHomepageImageViewModel>();
        var actions = new List<SimpleListEntryViewModel>();

        foreach (var child in card.Elements())
        {
            ParseHomepageElement(child, origin, lines, images, actions);
        }

        return new LaunchHomepageSectionViewModel(
            string.IsNullOrWhiteSpace(title) ? T("launch.homepage.sections.content_title") : title,
            string.Empty,
            lines.ToArray(),
            images.ToArray(),
            actions.ToArray());
    }

    private void ParseHomepageElement(
        XElement element,
        HomepageContentOrigin origin,
        List<string> lines,
        List<LaunchHomepageImageViewModel> images,
        List<SimpleListEntryViewModel> actions)
    {
        if (IsNamed(element, "TextBlock"))
        {
            AddHelpText(lines, ReadAttribute(element, "Text"));
            return;
        }

        if (IsNamed(element, "Label"))
        {
            AddHelpText(lines, ReadAttribute(element, "Content"));
            return;
        }

        if (IsNamed(element, "MyHint"))
        {
            AddHelpText(lines, T("launch.homepage.hints.prefix", ("text", ReadAttribute(element, "Text"))));
            return;
        }

        if (IsButtonLikeElement(element))
        {
            ParseHomepageButtonElement(element, origin, lines, actions);
            return;
        }

        if (IsNamed(element, "MyListItem"))
        {
            var title = ReadAttribute(element, "Title");
            var eventType = ReadAttribute(element, "EventType");
            var eventData = ResolveHomepageEventData(eventType, ReadAttribute(element, "EventData"), origin);
            var info = ReadAttribute(element, "Info");
            actions.Add(CreateHomepageAction(
                string.IsNullOrWhiteSpace(title) ? DeriveHelpActionTitle(eventType, eventData) : title,
                string.IsNullOrWhiteSpace(info)
                    ? string.IsNullOrWhiteSpace(eventData) ? T("launch.homepage.actions.inline_default_info") : eventData
                    : info,
                eventType,
                eventData));
            return;
        }

        if (IsNamed(element, "MyImage"))
        {
            var source = ResolveHomepageAssetTarget(ReadAttribute(element, "Source"), origin);
            if (!string.IsNullOrWhiteSpace(source))
            {
                images.Add(CreateHomepageImage(source, ReadAttribute(element, "ToolTip"), origin));
            }

            return;
        }

        foreach (var child in element.Elements())
        {
            ParseHomepageElement(child, origin, lines, images, actions);
        }
    }

    private void ParseHomepageButtonElement(
        XElement element,
        HomepageContentOrigin origin,
        List<string> lines,
        List<SimpleListEntryViewModel> actions)
    {
        var title = ReadAttribute(element, "Text");
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReadAttribute(element, "Title");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReadAttribute(element, "ToolTip");
        }

        var eventType = ReadAttribute(element, "EventType");
        var eventData = ResolveHomepageEventData(eventType, ReadAttribute(element, "EventData"), origin);
        if (!string.IsNullOrWhiteSpace(eventType) || !string.IsNullOrWhiteSpace(eventData))
        {
            var info = ReadAttribute(element, "ToolTip");
            actions.Add(CreateHomepageAction(
                string.IsNullOrWhiteSpace(title) ? DeriveHelpActionTitle(eventType, eventData) : title,
                string.IsNullOrWhiteSpace(info)
                    ? string.IsNullOrWhiteSpace(eventData) ? eventType : eventData
                    : info,
                eventType,
                eventData));
            return;
        }

        var customEventSummary = DescribeCustomEvents(element);
        if (!string.IsNullOrWhiteSpace(customEventSummary))
        {
            AddHelpText(
                lines,
                string.IsNullOrWhiteSpace(title)
                    ? T("launch.homepage.examples.button_with_events_untitled", ("summary", customEventSummary))
                    : T("launch.homepage.examples.button_with_events", ("title", title), ("summary", customEventSummary)));
            return;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            AddHelpText(lines, T("launch.homepage.examples.button", ("title", title)));
        }
    }

    private SimpleListEntryViewModel CreateHomepageAction(
        string title,
        string info,
        string? eventType,
        string? eventData)
    {
        return new SimpleListEntryViewModel(
            title,
            info,
            new ActionCommand(() => ExecuteHelpEvent(title, eventType, eventData)));
    }

    private LaunchHomepageImageViewModel CreateHomepageImage(string source, string? description, HomepageContentOrigin origin)
    {
        var image = new LaunchHomepageImageViewModel(source, string.IsNullOrWhiteSpace(description) ? origin.SourceName : description.Trim());
        _ = LoadHomepageImageAsync(image);
        return image;
    }

    private async Task LoadHomepageImageAsync(LaunchHomepageImageViewModel image)
    {
        try
        {
            string? bitmapPath = null;
            if (Uri.TryCreate(image.Source, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                bitmapPath = await FrontendCommunityIconCache.EnsureCachedIconAsync(image.Source);
            }
            else if (File.Exists(image.Source))
            {
                bitmapPath = image.Source;
            }

            if (string.IsNullOrWhiteSpace(bitmapPath) || !File.Exists(bitmapPath))
            {
                return;
            }

            var bitmap = new Bitmap(bitmapPath);
            await Dispatcher.UIThread.InvokeAsync(() => image.Bitmap = bitmap);
        }
        catch
        {
            // Ignore individual image load failures and keep rendering the rest of the homepage.
        }
    }

    private void ApplyHomepageLoadingState(string statusText, bool clearSections)
    {
        if (clearSections)
        {
            DisposeHomepageSections(LaunchHomepageSections);
            ReplaceItems(LaunchHomepageSections, []);
            RaisePropertyChanged(nameof(HasLaunchHomepageSections));
        }

        IsLaunchHomepageLoading = true;
        LaunchHomepageStatusText = statusText;
        RaisePropertyChanged(nameof(ShowLaunchHomepageStatusCard));
    }

    private void ApplyHomepageSections(
        IReadOnlyList<LaunchHomepageSectionViewModel> sections,
        bool isLoading,
        string statusText)
    {
        DisposeHomepageSections(LaunchHomepageSections);
        ReplaceItems(LaunchHomepageSections, sections);
        RaisePropertyChanged(nameof(HasLaunchHomepageSections));
        IsLaunchHomepageLoading = isLoading;
        LaunchHomepageStatusText = statusText;
        RaisePropertyChanged(nameof(ShowLaunchHomepageStatusCard));
    }

    private bool IsHomepageRefreshCurrent(int refreshVersion)
    {
        return refreshVersion == _homepageRefreshVersion && _homepageRefreshCts?.IsCancellationRequested != true;
    }

    private string ResolveHomepageEventData(string? eventType, string rawValue, HomepageContentOrigin origin)
    {
        return eventType switch
        {
            "open_web" or "\u6253\u5F00\u7F51\u9875" => ResolveHomepageAssetTarget(rawValue, origin),
            "open_file" or "\u6253\u5F00\u6587\u4EF6" => ResolveHomepageAssetTarget(rawValue, origin),
            "download_file" or "\u4E0B\u8F7D\u6587\u4EF6" => ResolveHomepageAssetTarget(rawValue, origin),
            _ when string.IsNullOrWhiteSpace(eventType) => ResolveHomepageAssetTarget(rawValue, origin),
            _ => rawValue
        };
    }

    private static string ResolveHomepageAssetTarget(string rawValue, HomepageContentOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(rawValue, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.IsFile ? absoluteUri.LocalPath : absoluteUri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(origin.BaseDirectory))
        {
            return Path.GetFullPath(Path.Combine(origin.BaseDirectory, rawValue.Replace('/', Path.DirectorySeparatorChar)));
        }

        if (origin.BaseUri is not null)
        {
            return new Uri(origin.BaseUri, rawValue).ToString();
        }

        return rawValue;
    }

    private string ReadCachedHomepageUrl()
    {
        try
        {
            var provider = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
            return provider.Exists(HomepageCacheUrlKey)
                ? provider.Get<string>(HomepageCacheUrlKey)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetHomepageCacheFilePath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.FrontendTempDirectory, "homepage-cache", "Custom.xaml");
    }

    private static void FlushHomepageSection(
        List<LaunchHomepageSectionViewModel> sections,
        List<string> pendingLines,
        List<LaunchHomepageImageViewModel> pendingImages,
        List<SimpleListEntryViewModel> pendingActions,
        string title)
    {
        if (pendingLines.Count == 0 && pendingImages.Count == 0 && pendingActions.Count == 0)
        {
            return;
        }

        sections.Add(new LaunchHomepageSectionViewModel(
            title,
            string.Empty,
            pendingLines.ToArray(),
            pendingImages.ToArray(),
            pendingActions.ToArray()));
        pendingLines.Clear();
        pendingImages.Clear();
        pendingActions.Clear();
    }

    private static void DisposeHomepageSections(IEnumerable<LaunchHomepageSectionViewModel> sections)
    {
        foreach (var section in sections)
        {
            section.Dispose();
        }
    }

    private string GetRandomHomepageHint()
    {
        var lines = ReadHomepageHintLines();
        if (lines.Count == 0)
        {
            return T("launch.homepage.random_hint.fallback");
        }

        return lines[Random.Shared.Next(lines.Count)];
    }

    private static IReadOnlyList<string> ReadHomepageHintLines()
    {
        var externalPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory, "PCL", "hints.txt");
        if (File.Exists(externalPath))
        {
            try
            {
                return File.ReadAllLines(externalPath)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
            }
            catch
            {
                // Fall back to the bundled asset below.
            }
        }

        var bundledPath = Path.Combine(LauncherRootDirectory, "Resources", "hints.txt");
        return File.Exists(bundledPath)
            ? File.ReadAllLines(bundledPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray()
            : [];
    }
}

internal sealed class LaunchHomepageSectionViewModel(
    string title,
    string subtitle,
    IReadOnlyList<string> lines,
    IReadOnlyList<LaunchHomepageImageViewModel> images,
    IReadOnlyList<SimpleListEntryViewModel> actions) : IDisposable
{
    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public IReadOnlyList<string> Lines { get; } = lines;

    public IReadOnlyList<LaunchHomepageImageViewModel> Images { get; } = images;

    public IReadOnlyList<SimpleListEntryViewModel> Actions { get; } = actions;

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public bool HasLines => Lines.Count > 0;

    public bool HasImages => Images.Count > 0;

    public bool HasActions => Actions.Count > 0;

    public void Dispose()
    {
        foreach (var image in Images)
        {
            image.Dispose();
        }
    }
}

internal sealed class LaunchHomepageImageViewModel(string source, string description) : ViewModelBase, IDisposable
{
    private Bitmap? _bitmap;
    private bool _isDisposed;

    public string Source { get; } = source;

    public string Description { get; } = description;

    public Bitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            if (ReferenceEquals(_bitmap, value))
            {
                return;
            }

            if (_isDisposed)
            {
                value?.Dispose();
                return;
            }

            var previousBitmap = _bitmap;
            _bitmap = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasBitmap));
            previousBitmap?.Dispose();
        }
    }

    public bool HasBitmap => Bitmap is not null;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        var previousBitmap = _bitmap;
        _bitmap = null;
        previousBitmap?.Dispose();
    }
}

internal sealed record HomepageContentOrigin(
    string SourceName,
    Uri? BaseUri,
    string? BaseDirectory);

internal sealed record HomepagePresetDefinition(
    string Title,
    HomepagePresetKind Kind,
    string? Target);

internal enum HomepagePresetKind
{
    RandomHint,
    RemoteMarkup,
    OfficialNews
}

internal sealed record MinecraftNewsApiResponse(
    [property: JsonPropertyName("result")] MinecraftNewsResult? Result);

internal sealed record MinecraftNewsResult(
    [property: JsonPropertyName("results")] IReadOnlyList<MinecraftNewsItem>? Results);

internal sealed record MinecraftNewsItem(
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("author")]
    string? Author,
    [property: JsonPropertyName("image")]
    string Image,
    [property: JsonPropertyName("imageAltText")]
    string? ImageAltText,
    [property: JsonPropertyName("time")]
    long Time);
