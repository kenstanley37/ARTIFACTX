using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in GuidePage's topic list - pairs a curated
/// ArtifactX.Almanac.Guide.GuideTopicInfo entry with whether the currently
/// loaded account has it Unlocked/Seen (NmsAccountData.GuideUnlockedPath/
/// GuideSeenPath). GuidePage is the only thing that writes these and is
/// responsible for keeping AccountSessionManager's staged edits in sync.</summary>
public partial class GuideTopicRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool isUnlocked;

    [ObservableProperty]
    private bool isSeen;
}
