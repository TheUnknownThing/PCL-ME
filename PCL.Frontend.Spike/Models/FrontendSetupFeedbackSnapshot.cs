namespace PCL.Frontend.Spike.Models;

internal sealed record FrontendSetupFeedbackSnapshot(
    IReadOnlyList<FrontendSetupFeedbackSectionSnapshot> Sections,
    DateTimeOffset FetchedAtUtc);

internal sealed record FrontendSetupFeedbackSectionSnapshot(
    string Key,
    string Title,
    bool DefaultExpanded,
    IReadOnlyList<FrontendSetupFeedbackEntrySnapshot> Entries);

internal sealed record FrontendSetupFeedbackEntrySnapshot(
    int Number,
    string Title,
    string Summary,
    string Url);
