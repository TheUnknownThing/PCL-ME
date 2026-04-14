using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

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
                CreateFeedbackSection(SetupText.Feedback.LoadingSectionTitle, true,
                [
                    CreateSimpleEntry(SetupText.Feedback.LoadingEntryTitle, SetupText.Feedback.LoadingEntrySummary)
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
                    LocalizeFeedbackSectionTitle(section.Key),
                    section.DefaultExpanded,
                    section.Entries.Select(entry =>
                        CreateSimpleEntry(
                            entry.Title,
                            entry.Summary,
                            CreateOpenTargetCommand(
                                _i18n.T(
                                    "setup.feedback.actions.open_issue",
                                    new Dictionary<string, object?>(StringComparer.Ordinal)
                                    {
                                        ["number"] = entry.Number
                                    }),
                                entry.Url,
                                entry.Url)))
                        .ToArray())));
    }

    private string LocalizeFeedbackSectionTitle(string key)
    {
        return key switch
        {
            "processing" => _i18n.T("setup.feedback.sections.processing"),
            "waiting-process" => _i18n.T("setup.feedback.sections.waiting_process"),
            "wait" => _i18n.T("setup.feedback.sections.waiting"),
            "pause" => _i18n.T("setup.feedback.sections.paused"),
            "upnext" => _i18n.T("setup.feedback.sections.up_next"),
            "completed" => _i18n.T("setup.feedback.sections.completed"),
            "decline" => _i18n.T("setup.feedback.sections.declined"),
            "ignored" => _i18n.T("setup.feedback.sections.ignored"),
            "duplicate" => _i18n.T("setup.feedback.sections.duplicate"),
            "other" => _i18n.T("setup.feedback.sections.other"),
            _ => key
        };
    }
}
