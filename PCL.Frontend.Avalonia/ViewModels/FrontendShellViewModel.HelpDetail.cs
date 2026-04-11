using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Xml.Linq;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private FrontendToolsHelpEntry? _currentHelpDetailEntry;

    public ObservableCollection<HelpDetailSectionViewModel> HelpDetailSections { get; } = [];

    public bool ShowHelpDetailSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.HelpDetail;

    public bool HasHelpDetailSections => HelpDetailSections.Count > 0;

    public bool HasNoHelpDetailSections => !HasHelpDetailSections;

    public string HelpDetailTitle => _currentHelpDetailEntry?.Title ?? "帮助详情";

    public string HelpDetailSummary => string.IsNullOrWhiteSpace(_currentHelpDetailEntry?.Summary)
        ? "已加载帮助条目详情。"
        : _currentHelpDetailEntry!.Summary;

    public string HelpDetailSource => _currentHelpDetailEntry?.RawPath ?? "未解析帮助来源";

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
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.HelpDetail), $"Opened help detail for {entry.Title}.");
        RefreshHelpDetailSurface();
        if (addActivity)
        {
            AddActivity($"查看帮助: {entry.Title}", entry.RawPath);
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
                    "条目内容缺失",
                    [
                        "当前帮助条目只有索引元数据，没有可渲染的详情内容。",
                        $"源路径：{entry.RawPath}"
                    ],
                    [])
            ];
        }

        try
        {
            var wrapped = $"<Root>{entry.DetailContent}</Root>";
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
                    FlushPendingSection(sections, pendingLines, pendingActions, "补充说明");
                    sections.Add(ParseHelpCard(child));
                    continue;
                }

                ParseHelpElement(child, pendingLines, pendingActions);
            }

            FlushPendingSection(sections, pendingLines, pendingActions, "补充说明");
            if (sections.Count == 0)
            {
                sections.Add(new HelpDetailSectionViewModel(
                    entry.Title,
                    [
                        "当前帮助条目没有可识别的卡片内容。",
                        $"源路径：{entry.RawPath}"
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
                    "加载失败",
                    [
                        "帮助详情内容解析失败，已保留原始来源以便继续排查。",
                        ex.Message,
                        $"源路径：{entry.RawPath}"
                    ],
                    [])
            ];
        }
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
            string.IsNullOrWhiteSpace(title) ? "帮助内容" : title,
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
            AddHelpText(lines, $"提示：{ReadAttribute(element, "Text")}");
            return;
        }

        if (IsNamed(element, "MyButton"))
        {
            var title = ReadAttribute(element, "Text");
            var eventType = ReadAttribute(element, "EventType");
            var eventData = ReadAttribute(element, "EventData");
            actions.Add(CreateHelpAction(
                string.IsNullOrWhiteSpace(title) ? "执行帮助操作" : title,
                string.IsNullOrWhiteSpace(eventData) ? eventType : eventData,
                eventType,
                eventData));
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
                    ? string.IsNullOrWhiteSpace(eventData) ? "打开帮助内动作。" : eventData
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
                actions.Add(CreateHelpAction("查看配图", source, "打开网页", source));
            }

            return;
        }

        foreach (var child in element.Elements())
        {
            ParseHelpElement(child, lines, actions);
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
                    AddFailureActivity($"{title} 失败", $"未找到帮助条目：{eventData}");
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
            default:
                if (!string.IsNullOrWhiteSpace(eventData))
                {
                    OpenHelpTarget(title, eventData, infoFallback: eventData);
                }
                else
                {
                    AddFailureActivity($"{title} 失败", "帮助动作缺少可执行的目标。");
                }

                return;
        }
    }

    private async Task CopyHelpTextAsync(string title, string? eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData))
        {
            AddFailureActivity($"{title} 失败", "没有可复制的文本。");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(eventData);
            AddActivity(title, "已复制帮助中的文本内容。");
        }
        catch (Exception ex)
        {
            AddFailureActivity($"{title} 失败", ex.Message);
        }
    }

    private async Task DownloadHelpFileAsync(string title, string? eventData)
    {
        if (!Uri.TryCreate(eventData, UriKind.Absolute, out var uri))
        {
            AddFailureActivity($"{title} 失败", "下载地址无效。");
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            await using var source = await client.GetStreamAsync(uri);
            Directory.CreateDirectory(ToolDownloadFolder);
            var fileName = Path.GetFileName(uri.LocalPath);
            fileName = string.IsNullOrWhiteSpace(fileName) ? "help-download.bin" : SanitizeFileSegment(fileName);
            var targetPath = Path.Combine(ToolDownloadFolder, fileName);
            await using var output = File.Create(targetPath);
            await source.CopyToAsync(output);
            OpenInstanceTarget(title, targetPath, "帮助文件下载完成但目标文件不存在。");
        }
        catch (Exception ex)
        {
            AddFailureActivity($"{title} 失败", ex.Message);
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
            AddFailureActivity($"{title} 失败", "帮助动作缺少目标地址。");
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity(title, infoFallback ?? target);
        }
        else
        {
            AddFailureActivity($"{title} 失败", error ?? target);
        }
    }

    private static HelpPopupSpec ParseHelpPopupSpec(string fallbackTitle, string? eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData))
        {
            return new HelpPopupSpec(fallbackTitle, "帮助弹窗缺少内容。", "确定");
        }

        var segments = eventData.Split('|', 3);
        if (segments.Length < 2)
        {
            return new HelpPopupSpec(
                fallbackTitle,
                NormalizeHelpPopupText(eventData),
                "确定");
        }

        var popupTitle = string.IsNullOrWhiteSpace(segments[0])
            ? fallbackTitle
            : NormalizeHelpPopupText(segments[0]);
        var popupMessage = NormalizeHelpPopupText(segments[1]);
        var buttonText = segments.Length >= 3 && !string.IsNullOrWhiteSpace(segments[2])
            ? NormalizeHelpPopupText(segments[2])
            : "确定";
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

    private static string ReadAttribute(XElement element, string attributeName)
    {
        return WebUtility.HtmlDecode(element.Attribute(attributeName)?.Value ?? string.Empty).Trim();
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

    private static string DeriveHelpActionTitle(string? eventType, string? eventData)
    {
        return eventType switch
        {
            "打开帮助" => $"打开帮助：{Path.GetFileNameWithoutExtension(eventData) ?? "帮助条目"}",
            "打开网页" => "打开网页",
            "复制文本" => "复制文本",
            "下载文件" => "下载文件",
            "打开文件" => "打开文件",
            _ => string.IsNullOrWhiteSpace(eventData) ? "帮助操作" : eventData
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
