using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _selectedCommunityProjectId = string.Empty;
    private FrontendCommunityProjectState _communityProjectState = new(
        string.Empty,
        "未选择工程",
        "先从收藏夹中选择一个工程，再查看对应详情。",
        string.Empty,
        "未指定来源",
        "等待选择",
        "尚未加载",
        0,
        0,
        "未提供兼容信息",
        "未提供标签信息",
        [],
        string.Empty,
        false);
    private FrontendHomePageMarketState _homePageMarketState = new(
        "主页市场会聚合实时社区资源，方便直接浏览热门内容。",
        [],
        string.Empty,
        false);

    public ObservableCollection<DownloadCatalogSectionViewModel> CommunityProjectSections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> HomePageMarketSections { get; } = [];

    public bool ShowCompDetailSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.CompDetail;

    public bool ShowHomePageMarketSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.HomePageMarket;

    public string CommunityProjectTitle => _communityProjectState.Title;

    public string CommunityProjectSummary => _communityProjectState.Summary;

    public string CommunityProjectDescription => _communityProjectState.Description;

    public string CommunityProjectSource => _communityProjectState.Source;

    public string CommunityProjectStatus => _communityProjectState.Status;

    public string CommunityProjectUpdatedLabel => _communityProjectState.UpdatedLabel;

    public string CommunityProjectCompatibilitySummary => _communityProjectState.CompatibilitySummary;

    public string CommunityProjectCategorySummary => _communityProjectState.CategorySummary;

    public string CommunityProjectDownloadCountLabel => _communityProjectState.DownloadCount <= 0
        ? "暂无"
        : FormatCompactCount(_communityProjectState.DownloadCount);

    public string CommunityProjectFollowCountLabel => _communityProjectState.FollowCount <= 0
        ? "暂无"
        : FormatCompactCount(_communityProjectState.FollowCount);

    public bool HasCommunityProjectDescription => !string.IsNullOrWhiteSpace(CommunityProjectDescription);

    public bool HasCommunityProjectSections => CommunityProjectSections.Count > 0;

    public bool HasNoCommunityProjectSections => !HasCommunityProjectSections;

    public bool ShowCommunityProjectWarning => _communityProjectState.ShowWarning;

    public string CommunityProjectWarningText => _communityProjectState.WarningText;

    public string HomePageMarketSummary => _homePageMarketState.Summary;

    public bool HasHomePageMarketSections => HomePageMarketSections.Count > 0;

    public bool HasNoHomePageMarketSections => !HasHomePageMarketSections;

    public bool ShowHomePageMarketWarning => _homePageMarketState.ShowWarning;

    public string HomePageMarketWarningText => _homePageMarketState.WarningText;

    public void OpenCommunityProjectDetail(string projectId, string? projectTitle = null)
    {
        _selectedCommunityProjectId = projectId.Trim();
        RefreshCompDetailSurface();
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail),
            string.IsNullOrWhiteSpace(projectTitle)
                ? "已打开资源工程详情。"
                : $"已打开 {projectTitle} 的资源详情。");
    }

    private void RefreshCompDetailSurface()
    {
        _communityProjectState = FrontendCommunityProjectService.GetProjectState(
            _selectedCommunityProjectId,
            _instanceComposition.Selection.VanillaVersion,
            _selectedCommunityDownloadSourceIndex);

        ReplaceItems(
            CommunityProjectSections,
            _communityProjectState.Sections.Select(section => new DownloadCatalogSectionViewModel(
                section.Title,
                section.Entries.Select(entry => new DownloadCatalogEntryViewModel(
                    entry.Title,
                    entry.Info,
                    entry.Meta,
                    entry.ActionText,
                    CreateProjectSectionCommand(entry)))
                .ToArray())));

        RaiseCommunityProjectProperties();
    }

    private void RefreshHomePageMarketSurface()
    {
        _homePageMarketState = FrontendCommunityProjectService.BuildHomePageMarketState(
            _instanceComposition,
            _selectedCommunityDownloadSourceIndex);

        ReplaceItems(
            HomePageMarketSections,
            _homePageMarketState.Sections.Select(section => new DownloadCatalogSectionViewModel(
                section.Title,
                section.Entries.Select(entry => new DownloadCatalogEntryViewModel(
                    entry.Title,
                    entry.Info,
                    entry.Meta,
                    entry.ActionText,
                    CreateProjectSectionCommand(entry)))
                .ToArray())));

        RaiseHomePageMarketProperties();
    }

    private ActionCommand CreateProjectSectionCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand(entry.Title, entry.Info)
            : CreateOpenTargetCommand($"打开项目内容: {entry.Title}", entry.Target, entry.Target);
    }

    private void RaiseCommunityProjectProperties()
    {
        RaisePropertyChanged(nameof(CommunityProjectTitle));
        RaisePropertyChanged(nameof(CommunityProjectSummary));
        RaisePropertyChanged(nameof(CommunityProjectDescription));
        RaisePropertyChanged(nameof(CommunityProjectSource));
        RaisePropertyChanged(nameof(CommunityProjectStatus));
        RaisePropertyChanged(nameof(CommunityProjectUpdatedLabel));
        RaisePropertyChanged(nameof(CommunityProjectCompatibilitySummary));
        RaisePropertyChanged(nameof(CommunityProjectCategorySummary));
        RaisePropertyChanged(nameof(CommunityProjectDownloadCountLabel));
        RaisePropertyChanged(nameof(CommunityProjectFollowCountLabel));
        RaisePropertyChanged(nameof(HasCommunityProjectDescription));
        RaisePropertyChanged(nameof(HasCommunityProjectSections));
        RaisePropertyChanged(nameof(HasNoCommunityProjectSections));
        RaisePropertyChanged(nameof(ShowCommunityProjectWarning));
        RaisePropertyChanged(nameof(CommunityProjectWarningText));
    }

    private void RaiseHomePageMarketProperties()
    {
        RaisePropertyChanged(nameof(HomePageMarketSummary));
        RaisePropertyChanged(nameof(HasHomePageMarketSections));
        RaisePropertyChanged(nameof(HasNoHomePageMarketSections));
        RaisePropertyChanged(nameof(ShowHomePageMarketWarning));
        RaisePropertyChanged(nameof(HomePageMarketWarningText));
    }

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}亿",
            >= 10_000 => $"{value / 10_000d:0.#}万",
            _ => value.ToString()
        };
    }
}
