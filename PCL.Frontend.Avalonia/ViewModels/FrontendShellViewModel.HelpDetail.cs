using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Xml.Linq;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private FrontendToolsHelpEntry? _currentHelpDetailEntry;

    public ObservableCollection<HelpDetailSectionViewModel> HelpDetailSections { get; } = [];

    public string ToolsHelpSearchWatermark => LT("shell.tools.help.search.watermark");

    public string ToolsHelpNoResultsText => LT("shell.tools.help.search.no_results");

    public bool ShowHelpDetailSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.HelpDetail;

    public bool HasHelpDetailSections => HelpDetailSections.Count > 0;

    public bool HasNoHelpDetailSections => !HasHelpDetailSections;

    public string HelpDetailEmptyTitle => LT("shell.help_detail.empty.title");

    public string HelpDetailEmptyDescription => LT("shell.help_detail.empty.description");

    public string HelpDetailTitle => _currentHelpDetailEntry?.Title ?? LT("shell.help_detail.title_default");

    public string HelpDetailSummary => string.IsNullOrWhiteSpace(_currentHelpDetailEntry?.Summary)
        ? LT("shell.help_detail.summary_loaded")
        : _currentHelpDetailEntry!.Summary;

    public string HelpDetailSource => _currentHelpDetailEntry?.RawPath ?? LT("shell.help_detail.source_unresolved");

    private void OpenHelpTopic(FrontendToolsHelpEntry entry)
    {
        if (entry.IsEvent)
        {
            ExecuteHelpEvent(entry.Title, entry.EventType, entry.EventData);
            return;
        }

        ShowHelpDetail(entry, addActivity: true);
    }

    private void ShowHelpDetail(FrontendToolsHelpEntry entry, bool addActivity)
    {
        _currentHelpDetailEntry = entry;
        var route = new LauncherFrontendRoute(LauncherFrontendPageKey.HelpDetail);
        if (_currentRoute == route)
        {
            ChangeRoute(route, $"Opened help detail for {entry.Title}.", ShellNavigationTransitionDirection.Forward);
        }
        else
        {
            NavigateTo(route, $"Opened help detail for {entry.Title}.");
        }

        if (addActivity)
        {
            AddActivity(LT("shell.help_detail.activities.open", ("title", entry.Title)), entry.RawPath);
        }
    }

    private void RefreshHelpDetailSurface()
    {
        ReplaceItems(HelpDetailSections, []);

        if (!ShowHelpDetailSurface || _currentHelpDetailEntry is null)
        {
            RaiseHelpDetailProperties();
            return;
        }

        ReplaceItems(HelpDetailSections, BuildHelpDetailSections(_currentHelpDetailEntry));
        RaiseHelpDetailProperties();
    }

    private void RaiseHelpDetailProperties()
    {
        RaisePropertyChanged(nameof(ShowHelpDetailSurface));
        RaisePropertyChanged(nameof(HasHelpDetailSections));
        RaisePropertyChanged(nameof(HasNoHelpDetailSections));
        RaisePropertyChanged(nameof(HelpDetailEmptyTitle));
        RaisePropertyChanged(nameof(HelpDetailEmptyDescription));
        RaisePropertyChanged(nameof(HelpDetailTitle));
        RaisePropertyChanged(nameof(HelpDetailSummary));
        RaisePropertyChanged(nameof(HelpDetailSource));
    }

    private IReadOnlyList<HelpDetailSectionViewModel> BuildHelpDetailSections(FrontendToolsHelpEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DetailContent))
        {
            return
            [
                new HelpDetailSectionViewModel(
                    LT("shell.help_detail.sections.missing_content.title"),
                    [
                        LT("shell.help_detail.sections.missing_content.body"),
                        LT("shell.help_detail.sections.source_line", ("source", entry.RawPath))
                    ],
                    [])
            ];
        }

        try
        {
            var wrapped = WrapHelpDetailContent(entry.DetailContent);
            var root = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace).Root;
            if (root is null)
            {
                throw new InvalidOperationException("Help detail root is missing.");
            }

            var sections = new List<HelpDetailSectionViewModel>();
            var pendingLines = new List<string>();
            var pendingActions = new List<SimpleListEntryViewModel>();

            foreach (var child in root.Elements())
            {
                if (IsNamed(child, "MyCard"))
                {
                    FlushPendingSection(sections, pendingLines, pendingActions, LT("shell.help_detail.sections.supplement.title"));
                    sections.Add(ParseHelpCard(child));
                    continue;
                }

                ParseHelpElement(child, pendingLines, pendingActions);
            }

            FlushPendingSection(sections, pendingLines, pendingActions, LT("shell.help_detail.sections.supplement.title"));
            if (sections.Count == 0)
            {
                sections.Add(new HelpDetailSectionViewModel(
                    entry.Title,
                    [
                        LT("shell.help_detail.sections.no_cards.body"),
                        LT("shell.help_detail.sections.source_line", ("source", entry.RawPath))
                    ],
                    []));
            }

            return sections;
        }
        catch (Exception ex)
        {
            return
            [
                new HelpDetailSectionViewModel(
                    LT("shell.help_detail.sections.parse_failed.title"),
                    [
                        LT("shell.help_detail.sections.parse_failed.body"),
                        ex.Message,
                        LT("shell.help_detail.sections.source_line", ("source", entry.RawPath))
                    ],
                    [])
            ];
        }
    }

    private static string WrapHelpDetailContent(string content)
    {
        return $"""
                <Root xmlns:local="urn:pcl:help:local" xmlns:x="urn:pcl:help:x" xmlns:sys="urn:pcl:help:sys">
                {content.TrimStart('\uFEFF')}
                </Root>
                """;
    }

    private HelpDetailSectionViewModel ParseHelpCard(XElement card)
    {
        var title = ReadAttribute(card, "Title");
        var lines = new List<string>();
        var actions = new List<SimpleListEntryViewModel>();

        foreach (var child in card.Elements())
        {
            ParseHelpElement(child, lines, actions);
        }

        return new HelpDetailSectionViewModel(
            string.IsNullOrWhiteSpace(title) ? LT("shell.help_detail.sections.card_default_title") : title,
            lines,
            actions);
    }

    private void ParseHelpElement(
        XElement element,
        List<string> lines,
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
            AddHelpText(lines, LT("shell.help_detail.generated.hint", ("text", ReadAttribute(element, "Text"))));
            return;
        }

        if (IsButtonLikeElement(element))
        {
            ParseHelpButtonElement(element, lines, actions);
            return;
        }

        if (IsNamed(element, "MyListItem"))
        {
            var title = ReadAttribute(element, "Title");
            var eventType = ReadAttribute(element, "EventType");
            var eventData = ReadAttribute(element, "EventData");
            var info = ReadAttribute(element, "Info");
            actions.Add(CreateHelpAction(
                string.IsNullOrWhiteSpace(title)
                    ? DeriveHelpActionTitle(eventType, eventData)
                    : title,
                string.IsNullOrWhiteSpace(info)
                    ? string.IsNullOrWhiteSpace(eventData) ? LT("shell.help_detail.actions.inline_default_info") : eventData
                    : info,
                eventType,
                eventData));
            return;
        }

        if (IsNamed(element, "MyImage"))
        {
            var source = ReadAttribute(element, "Source");
            if (!string.IsNullOrWhiteSpace(source))
            {
                actions.Add(CreateHelpAction(LT("shell.help_detail.actions.view_image"), source, "打开网页", source));
            }

            return;
        }

        foreach (var child in element.Elements())
        {
            ParseHelpElement(child, lines, actions);
        }
    }

    private void ParseHelpButtonElement(
        XElement element,
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
        var eventData = ReadAttribute(element, "EventData");
        if (!string.IsNullOrWhiteSpace(eventType) || !string.IsNullOrWhiteSpace(eventData))
        {
            var info = ReadAttribute(element, "ToolTip");
            actions.Add(CreateHelpAction(
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
                    ? LT("shell.help_detail.generated.example_button_with_summary", ("summary", customEventSummary))
                    : LT("shell.help_detail.generated.example_button_with_title_and_summary", ("title", title), ("summary", customEventSummary)));
            return;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            AddHelpText(lines, LT("shell.help_detail.generated.example_button", ("title", title)));
        }
    }

    private SimpleListEntryViewModel CreateHelpAction(
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

    private void ExecuteHelpEvent(string title, string? eventType, string? eventData)
    {
        switch (eventType?.Trim())
        {
            case "打开网页":
                OpenHelpTarget(title, eventData, infoFallback: eventData);
                return;
            case "打开文件":
                OpenHelpTarget(title, eventData, infoFallback: eventData);
                return;
            case "打开帮助":
                if (TryResolveHelpEntry(eventData, out var helpEntry))
                {
                    ShowHelpDetail(helpEntry, addActivity: true);
                }
                else
                {
                    AddFailureActivity(
                        LT("shell.help_detail.failures.action", ("title", title)),
                        LT("shell.help_detail.failures.entry_not_found", ("reference", eventData ?? string.Empty)));
                }

                return;
            case "复制文本":
                _ = CopyHelpTextAsync(title, eventData);
                return;
            case "下载文件":
                _ = DownloadHelpFileAsync(title, eventData);
                return;
            case "弹出窗口":
                OpenHelpPopup(title, eventData);
                return;
            case "启动游戏":
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Launch), $"Opened the launch route from help detail: {title}.");
                return;
            case "内存优化":
                OpenMemoryOptimizeDialog();
                return;
            case "清理垃圾":
                ClearToolboxRubbish();
                return;
            case "刷新主页":
                RefreshLaunchHomepage(forceRefresh: true, addActivity: true);
                return;
            default:
                if (!string.IsNullOrWhiteSpace(eventData))
                {
                    OpenHelpTarget(title, eventData, infoFallback: eventData);
                }
                else
                {
                    AddFailureActivity(
                        LT("shell.help_detail.failures.action", ("title", title)),
                        LT("shell.help_detail.failures.missing_action_target"));
                }

                return;
        }
    }

    private async Task CopyHelpTextAsync(string title, string? eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData))
        {
            AddFailureActivity(
                LT("shell.help_detail.failures.action", ("title", title)),
                LT("shell.help_detail.failures.missing_copy_text"));
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(eventData);
            AddActivity(title, LT("shell.help_detail.actions.copy_text_completed"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.help_detail.failures.action", ("title", title)), ex.Message);
        }
    }

    private async Task DownloadHelpFileAsync(string title, string? eventData)
    {
        if (!Uri.TryCreate(eventData, UriKind.Absolute, out var uri))
        {
            AddFailureActivity(
                LT("shell.help_detail.failures.action", ("title", title)),
                LT("shell.help_detail.failures.invalid_download_address"));
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            Directory.CreateDirectory(ToolDownloadFolder);
            var fileName = Path.GetFileName(uri.LocalPath);
            fileName = string.IsNullOrWhiteSpace(fileName) ? "help-download.bin" : SanitizeFileSegment(fileName);
            var targetPath = Path.Combine(ToolDownloadFolder, fileName);
            var speedLimiter = _shellActionService.GetDownloadTransferOptions().MaxBytesPerSecond is long speedLimit
                ? new FrontendDownloadSpeedLimiter(speedLimit)
                : null;
            await FrontendDownloadTransferService.DownloadToPathAsync(
                client,
                uri.ToString(),
                targetPath,
                speedLimiter: speedLimiter);
            OpenInstanceTarget(title, targetPath, LT("shell.help_detail.failures.download_target_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.help_detail.failures.action", ("title", title)), ex.Message);
        }
    }

    private void OpenHelpPopup(string title, string? eventData) => _ = OpenHelpPopupAsync(title, eventData);

    private async Task OpenHelpPopupAsync(string title, string? eventData)
    {
        var popup = ParseHelpPopupSpec(title, eventData);
        var result = await ShowToolboxConfirmationAsync(popup.Title, popup.Message, popup.ButtonText);
        if (result is null)
        {
            return;
        }

        AddActivity(title, popup.Title);
    }

    private void OpenHelpTarget(string title, string? target, string? infoFallback)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity(
                LT("shell.help_detail.failures.action", ("title", title)),
                LT("shell.help_detail.failures.missing_target"));
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity(title, infoFallback ?? target);
        }
        else
        {
            AddFailureActivity(LT("shell.help_detail.failures.action", ("title", title)), error ?? target);
        }
    }

    private HelpPopupSpec ParseHelpPopupSpec(string fallbackTitle, string? eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData))
        {
            return new HelpPopupSpec(
                fallbackTitle,
                LT("shell.help_detail.popup.missing_content"),
                LT("shell.help_detail.popup.default_button"));
        }

        var segments = eventData.Split('|', 3);
        if (segments.Length < 2)
        {
            return new HelpPopupSpec(
                fallbackTitle,
                NormalizeHelpPopupText(eventData),
                LT("shell.help_detail.popup.default_button"));
        }

        var popupTitle = string.IsNullOrWhiteSpace(segments[0])
            ? fallbackTitle
            : NormalizeHelpPopupText(segments[0]);
        var popupMessage = NormalizeHelpPopupText(segments[1]);
        var buttonText = segments.Length >= 3 && !string.IsNullOrWhiteSpace(segments[2])
            ? NormalizeHelpPopupText(segments[2])
            : LT("shell.help_detail.popup.default_button");
        return new HelpPopupSpec(popupTitle, popupMessage, buttonText);
    }

    private static string NormalizeHelpPopupText(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
            .Trim();
    }

    private bool TryResolveHelpEntry(string? reference, out FrontendToolsHelpEntry entry)
    {
        entry = default!;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var normalizedReference = NormalizeHelpReference(reference);
        var candidate = _toolsComposition.Help.Entries.FirstOrDefault(item =>
        {
            var normalizedPath = NormalizeHelpReference(item.RawPath);
            return string.Equals(normalizedPath, normalizedReference, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith(normalizedReference, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith(normalizedReference.Replace(".json", ".xaml", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(normalizedPath), Path.GetFileName(normalizedReference), StringComparison.OrdinalIgnoreCase);
        });
        if (candidate is null)
        {
            return false;
        }

        entry = candidate;
        return true;
    }

    private static string NormalizeHelpReference(string reference)
    {
        var normalized = reference.Replace('\\', '/').Trim();
        var zipMarkerIndex = normalized.IndexOf("::", StringComparison.Ordinal);
        if (zipMarkerIndex >= 0)
        {
            normalized = normalized[(zipMarkerIndex + 2)..];
        }

        return normalized.TrimStart('/');
    }

    private static bool IsNamed(XElement element, string name)
    {
        return string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);
    }

    private static bool IsButtonLikeElement(XElement element)
    {
        return IsNamed(element, "MyButton")
            || IsNamed(element, "MyTextButton")
            || IsNamed(element, "MyIconTextButton")
            || IsNamed(element, "MyIconButton");
    }

    private static string ReadAttribute(XElement element, string attributeName)
    {
        var attribute = element.Attributes()
            .FirstOrDefault(item => string.Equals(item.Name.LocalName, attributeName, StringComparison.Ordinal));
        return WebUtility.HtmlDecode(attribute?.Value ?? string.Empty).Trim();
    }

    private static string DescribeCustomEvents(XElement element)
    {
        var eventTypes = element.Descendants()
            .Where(item => IsNamed(item, "CustomEvent"))
            .Select(item => ReadAttribute(item, "Type"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return eventTypes.Length == 0
            ? string.Empty
            : string.Join("、", eventTypes);
    }

    private static void AddHelpText(List<string> lines, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var segment in value
                     .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(segment => !string.IsNullOrWhiteSpace(segment)))
        {
            lines.Add(segment);
        }
    }

    private string DeriveHelpActionTitle(string? eventType, string? eventData)
    {
        var helpName = Path.GetFileNameWithoutExtension(eventData) ?? LT("shell.help_detail.actions.help_item");
        return eventType switch
        {
            "打开帮助" => LT("shell.help_detail.actions.open_help", ("name", helpName)),
            "打开网页" => LT("shell.help_detail.actions.open_web"),
            "复制文本" => LT("shell.help_detail.actions.copy_text"),
            "下载文件" => LT("shell.help_detail.actions.download_file"),
            "打开文件" => LT("shell.help_detail.actions.open_file"),
            _ => string.IsNullOrWhiteSpace(eventData) ? LT("shell.help_detail.actions.generic") : eventData
        };
    }

    private static void FlushPendingSection(
        List<HelpDetailSectionViewModel> sections,
        List<string> pendingLines,
        List<SimpleListEntryViewModel> pendingActions,
        string title)
    {
        if (pendingLines.Count == 0 && pendingActions.Count == 0)
        {
            return;
        }

        sections.Add(new HelpDetailSectionViewModel(title, pendingLines.ToArray(), pendingActions.ToArray()));
        pendingLines.Clear();
        pendingActions.Clear();
    }
}

internal sealed record HelpPopupSpec(string Title, string Message, string ButtonText);

internal sealed class HelpDetailSectionViewModel(
    string title,
    IReadOnlyList<string> lines,
    IReadOnlyList<SimpleListEntryViewModel> actions)
{
    public string Title { get; } = title;

    public IReadOnlyList<string> Lines { get; } = lines;

    public IReadOnlyList<SimpleListEntryViewModel> Actions { get; } = actions;

    public bool HasLines => Lines.Count > 0;

    public bool HasActions => Actions.Count > 0;
}
