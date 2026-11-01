namespace DnD.CharSheet.Application;

public interface IBackupService
{
    Task CreateAsync(string destinationPath);
    Task<BackupPreview> PreviewAsync(string archivePath);
    Task<string> RestoreAsync(BackupPreview preview);
}

/// <summary>Validated immutable staged restore; dispose when the preview dialog closes.</summary>
public abstract class BackupPreview : IDisposable
{
    public abstract DateTimeOffset CreatedUtc { get; }
    public abstract int CharacterCount { get; }
    public abstract int PortraitCount { get; }
    public abstract bool HasDraftRecovery { get; }
    public abstract IReadOnlyList<string> CharacterNames { get; }
    public abstract void Dispose();
}
