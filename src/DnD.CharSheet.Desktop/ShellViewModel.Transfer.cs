using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Desktop;

public sealed record TransferPreview(CharacterDraft Character, Adventure Source, int SourceCount,
    IReadOnlyList<Adventure> Destinations, IReadOnlyDictionary<AdventureId, int> Counts);

public sealed partial class ShellViewModel
{
    private TransferPreview? transferPreview;

    public async Task<TransferPreview?> BeginTransferAsync(CharacterDraft character)
    {
        if (adventures is null || !await DrainForNavigationAsync()) return null;
        try
        {
            var sourceId = ShowAdventurePanel ? ManagementSelection?.Id : SelectedAdventure?.Id;
            await ReadManagementAsync();
            var source = Adventures.FirstOrDefault(a => a.Id == sourceId);
            if (source is null || memberships.GetValueOrDefault(character.Id) != source.Id)
                throw new InvalidOperationException("Membership changed.");
            transferPreview = new(character, source, AdventureMemberCounts[source.Id],
                Adventures.Where(a => a.Id != source.Id).ToArray(), AdventureMemberCounts);
            return transferPreview;
        }
        catch (Exception)
        {
            AdventureStatus = LibraryStatus = "Transfer could not be opened. Refresh and check the Adventures before trying again.";
            IsAdventureBusy = false;
            return null;
        }
    }

    public async Task<string?> ConfirmTransferAsync(TransferPreview preview, Adventure destination)
    {
        if (!ReferenceEquals(transferPreview, preview) || !IsAdventureBusy ||
            !preview.Destinations.Any(a => a.Id == destination.Id)) return "Reopen transfer to check the Adventures.";
        // Consume before awaiting: duplicate calls cannot submit the same intent.
        transferPreview = null;
        try
        {
            await adventures!.TransferAsync(preview.Character.Id, preview.Source.Id, destination.Id);
        }
        catch (Exception)
        {
            return "Transfer could not be confirmed. Close this window, refresh and check the Adventures before trying again.";
        }
        if (ActiveDraft?.Id == preview.Character.Id) ActiveDraft = null;
        // Remove immediately after commit, even if subsequent reads fail.
        var removed = Drafts.FirstOrDefault(d => d.Id == preview.Character.Id);
        if (removed is not null) Drafts.Remove(removed);
        var message = $"Moved {preview.Character.Name} from {preview.Source.Name} to {destination.Name}.";
        try
        {
            await ReadManagementAsync();
            if (SelectedAdventure is not null) await LoadLibraryAsync();
        }
        catch (Exception) { message += " Refresh Adventures to reload the lists."; }
        AdventureStatus = LibraryStatus = message;
        return null;
    }

    public void EndTransfer(TransferPreview preview, bool manageSource)
    {
        transferPreview = null;
        if (manageSource)
        {
            ManagementSelection = Adventures.FirstOrDefault(a => a.Id == preview.Source.Id);
            ShowAdventurePanel = true;
        }
        IsAdventureBusy = false;
    }
}
