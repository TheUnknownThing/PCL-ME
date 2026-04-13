using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InitializeFeedbackSections()
    {
        if (_feedbackSnapshot is not null)
        {
            ApplyFeedbackSnapshot(_feedbackSnapshot);
        }
        else
        {
            ReplaceItems(FeedbackSections,
            [
                CreateFeedbackSection("正在获取", true,
                [
                    CreateSimpleEntry("正在同步 GitHub 反馈列表", "首次打开反馈页时会从仓库 Issue 列表加载真实反馈状态。")
                ])
            ]);
        }

        _ = RefreshFeedbackSectionsAsync(forceRefresh: false);
    }

    private void ApplyFeedbackSnapshot(FrontendSetupFeedbackSnapshot snapshot)
    {
        ReplaceItems(
            FeedbackSections,
            snapshot.Sections.Select(section =>
                CreateFeedbackSection(
                    section.Title,
                    section.DefaultExpanded,
                    section.Entries.Select(entry =>
                        CreateSimpleEntry(
                            entry.Title,
                            entry.Summary,
                            CreateOpenTargetCommand($"查看反馈: #{entry.Number}", entry.Url, entry.Url)))
                        .ToArray())));
    }
}
