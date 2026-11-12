using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Desktop;

public sealed partial class ShellViewModel
{
    private readonly IAdventureRepository? adventures;
    private List<DraftRecovery> recoveryEntries = [];
    private readonly Dictionary<CharacterId, AdventureId?> memberships = [];
    private IReadOnlyList<CharacterDraft> allDrafts = [];
    private Adventure? confirmationAdventure;
    private CharacterDraft? confirmationCharacter;
    private (AdventureId Id, CharacterId Character, string Name, string CharacterName, CharacterId? Existing)? pendingAdventure;
    public IReadOnlyDictionary<AdventureId, int> AdventureMemberCounts => memberships.Values.Where(id => id is not null).GroupBy(id => id!).ToDictionary(group => group.Key, group => group.Count());
    public string UnassignedStatus => UnassignedDrafts.Count == 0 ? "No unassigned characters." : $"{UnassignedDrafts.Count} unassigned characters.";
    public string ConfirmationActionLabel => confirmationCharacter is null ? "Delete Adventure" : "Assign Adventure";
    [ObservableProperty] private Adventure? selectedAdventure;
    [ObservableProperty] private Adventure? managementSelection;
    [ObservableProperty] private CharacterDraft? selectedUnassigned;
    [ObservableProperty] private string adventureNameInput = "";
    [ObservableProperty] private string newAdventureNameInput = "";
    [ObservableProperty] private string firstCharacterName = "";
    [ObservableProperty] private string adventureStatus = "Loading Adventures…";
    [ObservableProperty] private bool isAdventureBusy;
    [ObservableProperty] private bool showAdventurePanel = true;
    [ObservableProperty] private string confirmationText = "";
    [ObservableProperty] private bool hasAdventureConfirmation;
    public ObservableCollection<Adventure> Adventures { get; } = [];
    public ObservableCollection<CharacterDraft> UnassignedDrafts { get; } = [];
    public ObservableCollection<CharacterDraft> PreviewMembers { get; } = [];
    public int AllCharacterCount => allDrafts.Count + Drafts.Count(draft => !allDrafts.Any(saved => saved.Id == draft.Id));
    public string AdventureHeading => SelectedAdventure?.Name ?? "Select Adventure";
    public bool ShowCharacterLibrary => SelectedAdventure is not null && !ShowAdventurePanel;
    public string RetainedRecoveryText => string.Join("\n", recoveryEntries
        .Where(entry => entry.AdventureId is null || !Adventures.Any(a => a.Id == entry.AdventureId))
        .Select(entry => $"Recovered name (no available Adventure): {entry.Name}" +
            (entry.SubmittedId is null ? "" : " — creation requires review; it will not be resubmitted here.")));

    partial void OnSelectedAdventureChanged(Adventure? value)
    {
        OnPropertyChanged(nameof(AdventureHeading));
        OnPropertyChanged(nameof(ShowCharacterLibrary));
        OnPropertyChanged(nameof(CanEditDraftName));
    }
    partial void OnShowAdventurePanelChanged(bool value) => OnPropertyChanged(nameof(ShowCharacterLibrary));
    partial void OnIsAdventureBusyChanged(bool value)
    {
        CanCreate = !value && recoveryLoaded && libraryLoaded && SelectedAdventure is not null;
        OnPropertyChanged(nameof(CanEditDraftName));
    }
    partial void OnManagementSelectionChanged(Adventure? value)
    {
        AdventureNameInput = value?.Name ?? "";
        PreviewMembers.Clear();
        if (value is not null)
            foreach (var draft in allDrafts.Where(d => memberships.GetValueOrDefault(d.Id) == value.Id)) PreviewMembers.Add(draft);
        CancelAdventureAction();
    }
    private IReadOnlyList<DraftRecovery> UpdateRecovery(DraftRecovery snapshot)
    {
        recoveryEntries.RemoveAll(entry => entry.AdventureId == snapshot.AdventureId);
        recoveryEntries.Add(snapshot);
        return recoveryEntries.ToArray();
    }
    private void RestoreContextInput()
    {
        var snapshot = recoveryEntries.FirstOrDefault(entry => entry.AdventureId == SelectedAdventure?.Id);
        restoring = true;
        DraftNameInput = snapshot?.Name ?? "";
        restoring = false;
        submittedId = snapshot?.SubmittedId;
        HasNameError = HasCreationError = HasCreationSucceeded = false;
        CreationStatus = submittedId is null ? "" : "Retry to check and finish the submitted draft in this Adventure.";
        RecoveryStatus = snapshot is null ? "" : "Recovered your unfinished draft name.";
        OnPropertyChanged(nameof(CreateDraftLabel));
        OnPropertyChanged(nameof(CanEditDraftName));
    }
    private async Task<bool> DrainForNavigationAsync()
    {
        if (IsCreating || closing || running || IsAdventureBusy) return false;
        // Acquire navigation ownership before yielding, so input and other transitions cannot
        // append writes or change context while the captured journal chain drains.
        IsAdventureBusy = true;
        await recoveryWrites;
        if (HasRecoveryError)
        {
            AdventureStatus = "Save recovery could not finish. Retry before changing Adventure.";
            IsAdventureBusy = false;
            return false;
        }
        return true;
    }
    [RelayCommand]
    private async Task RefreshAdventuresAsync()
    {
        if (adventures is null) { AdventureStatus = "Adventure management is unavailable."; return; }
        if (IsAdventureBusy) return;
        IsAdventureBusy = true;
        try
        {
            await ReadManagementAsync();
            // An explicit refresh establishes whether a previously ambiguous creation exists.
            // It permits a new form intent without ever blindly repeating the old insertion.
            if (pendingAdventure is { } pending)
            {
                var saved = Adventures.FirstOrDefault(a => a.Id == pending.Id);
                pendingAdventure = null;
                if (saved is not null)
                {
                    ManagementSelection = saved;
                    NewAdventureNameInput = FirstCharacterName = "";
                    SelectedUnassigned = null;
                    AdventureStatus = "The previous Adventure creation was saved. Choose Open Adventure to enter it.";
                }
            }
        }
        catch (Exception) { AdventureStatus = "Adventures could not be opened. Retry."; }
        finally { IsAdventureBusy = false; }
    }
    private async Task ReadManagementAsync()
    {
        var list = await adventures!.ListAsync();
        var characters = await library.ListAsync();
        var map = new Dictionary<CharacterId, AdventureId?>();
        foreach (var draft in characters) map.Add(draft.Id, (await adventures.GetMembershipAsync(draft.Id)).AdventureId);
        var previous = ManagementSelection?.Id;
        allDrafts = characters;
        memberships.Clear();
        foreach (var pair in map) memberships.Add(pair.Key, pair.Value);
        Adventures.Clear();
        foreach (var item in list) Adventures.Add(item);
        UnassignedDrafts.Clear();
        foreach (var draft in characters.Where(d => map[d.Id] is null)) UnassignedDrafts.Add(draft);
        var refreshedSelection = list.FirstOrDefault(a => a.Id == previous);
        if (ManagementSelection == refreshedSelection) OnManagementSelectionChanged(refreshedSelection);
        else ManagementSelection = refreshedSelection;
        if (SelectedAdventure is { } current)
        {
            SelectedAdventure = list.FirstOrDefault(a => a.Id == current.Id);
            if (SelectedAdventure is null) { ActiveDraft = null; Drafts.Clear(); ShowAdventurePanel = true; }
        }
        AdventureStatus = list.Count == 0 ? "No Adventures. Create one with its first character." : $"{list.Count} Adventures. Select one and choose Open Adventure.";
        OnPropertyChanged(nameof(AllCharacterCount));
        OnPropertyChanged(nameof(AdventureMemberCounts));
        OnPropertyChanged(nameof(UnassignedStatus));
        OnPropertyChanged(nameof(RetainedRecoveryText));
    }
    [RelayCommand]
    private async Task OpenAdventureAsync(Adventure? adventure)
    {
        if (adventure is null || !await DrainForNavigationAsync()) return;
        IsAdventureBusy = true;
        try
        {
            await ReadManagementAsync();
            var current = Adventures.FirstOrDefault(a => a.Id == adventure.Id);
            if (current is null) { AdventureStatus = "Adventure no longer exists. Choose another."; return; }
            ActiveDraft = null;
            Drafts.Clear();
            SelectedAdventure = current;
            RestoreContextInput();
            await LoadLibraryAsync();
            ShowAdventurePanel = false;
        }
        catch (Exception) { AdventureStatus = "Adventure could not be opened. Retry."; }
        finally { IsAdventureBusy = false; }
    }
    [RelayCommand]
    private async Task ReturnToSelectionAsync()
    {
        if (!await DrainForNavigationAsync()) return;
        try
        {
            ActiveDraft = null;
            SelectedAdventure = null;
            Drafts.Clear();
            libraryLoaded = false;
            restoring = true;
            DraftNameInput = "";
            restoring = false;
            submittedId = null;
            CanCreate = false;
            ShowAdventurePanel = true;
            await ReadManagementAsync();
        }
        catch (Exception) { AdventureStatus = "Adventures could not be opened. Retry."; }
        finally { IsAdventureBusy = false; }
    }
    private async Task<bool> ManageAsync(Func<Task> action, string success)
    {
        if (!await DrainForNavigationAsync()) return false;
        IsAdventureBusy = true;
        try
        {
            await action();
            CancelAdventureAction();
            await ReadManagementAsync();
            AdventureStatus = success;
            return true;
        }
        catch (ArgumentException) { AdventureStatus = "Use names of 1–80 characters without control characters."; return false; }
        catch (Exception) { AdventureStatus = "Change could not be confirmed. Refresh and check before retrying. Your input has been retained."; return false; }
        finally { IsAdventureBusy = false; }
    }
    [RelayCommand]
    private async Task CreateAdventureAsync()
    {
        var name = NewAdventureNameInput;
        var first = SelectedUnassigned;
        var characterName = FirstCharacterName;
        try
        {
            DraftName.Normalize(name);
            if (first is null) DraftName.Normalize(characterName);
        }
        catch (ArgumentException) { AdventureStatus = "Enter an Adventure name and a first character, each using 1–80 characters without control characters."; return; }
        if (pendingAdventure is { } pending && (pending.Name != name || pending.CharacterName != characterName || pending.Existing != first?.Id))
        {
            AdventureStatus = "Retry the previous Adventure creation with its original names and first character before starting another.";
            return;
        }
        pendingAdventure ??= (AdventureId.New(), first?.Id ?? CharacterId.New(), name, characterName, first?.Id);
        var intent = pendingAdventure.Value;
        var id = intent.Id;
        var character = intent.Character;
        var succeeded = await ManageAsync(async () =>
        {
            var existing = (await adventures!.ListAsync()).FirstOrDefault(a => a.Id == id);
            if (existing is null)
            {
                if (first is null) await adventures.CreateWithDraftAsync(id, name, character, characterName);
                else await adventures.CreateWithUnassignedAsync(id, name, character);
            }
            else
            {
                if (existing.Name != DraftName.Normalize(name)) throw new InvalidOperationException("Adventure metadata changed.");
                (await adventures.GetMembershipAsync(character)).RequireUsableIn(id);
            }
        }, "Adventure created. Choose Open Adventure to enter it.");
        if (succeeded)
        {
            pendingAdventure = null;
            NewAdventureNameInput = FirstCharacterName = "";
            SelectedUnassigned = null;
            ManagementSelection = Adventures.FirstOrDefault(a => a.Id == id);
        }
    }
    [RelayCommand]
    private async Task RenameAdventureAsync()
    {
        if (ManagementSelection is not { } target) return;
        var name = AdventureNameInput;
        await ManageAsync(() => adventures!.RenameAsync(target.Id, name), "Adventure renamed.");
    }
    [RelayCommand]
    private void RequestDeleteAdventure()
    {
        if (IsAdventureBusy || ManagementSelection is not { } target) return;
        confirmationAdventure = target;
        confirmationCharacter = null;
        ConfirmationText = $"Delete Adventure “{target.Name}”? Its {PreviewMembers.Count} characters will be kept unassigned and cannot be used until assigned.";
        OnPropertyChanged(nameof(ConfirmationActionLabel));
        HasAdventureConfirmation = true;
    }
    [RelayCommand]
    private void RequestAssignCharacter()
    {
        if (IsAdventureBusy || ManagementSelection is not { } target || SelectedUnassigned is not { } character) return;
        confirmationAdventure = target;
        confirmationCharacter = character;
        ConfirmationText = $"Assign “{character.Name}” to Adventure “{target.Name}”? Character data will be retained.";
        OnPropertyChanged(nameof(ConfirmationActionLabel));
        HasAdventureConfirmation = true;
    }
    [RelayCommand]
    private async Task ConfirmAdventureActionAsync()
    {
        if (!HasAdventureConfirmation || confirmationAdventure is not { } target) return;
        var character = confirmationCharacter;
        await ManageAsync(async () =>
        {
            if (character is not null) await adventures!.AssignAsync(character.Id, target.Id);
            else
            {
                await adventures!.DeleteAsync(target.Id);
                if (SelectedAdventure?.Id == target.Id)
                {
                    ActiveDraft = null;
                    SelectedAdventure = null;
                    Drafts.Clear();
                    ShowAdventurePanel = true;
                    libraryLoaded = false;
                }
            }
        }, character is null ? "Adventure deleted. Its characters have been retained unassigned." : "Character assigned.");
    }
    [RelayCommand]
    private void CancelAdventureAction()
    {
        HasAdventureConfirmation = false;
        ConfirmationText = "";
        confirmationAdventure = null;
        confirmationCharacter = null;
    }
}
