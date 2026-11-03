using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>Additional file edits deliberately enlisted in the loaded save's
/// Save/Reset workflow. Validation runs before either file is written.</summary>
internal interface IStagedSaveCompanion
{
    bool HasChanges { get; }
    int PendingCount { get; }
    Task ValidateAsync();
    Task CommitAsync();
    void Discard();
}
