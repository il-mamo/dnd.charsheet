using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using System.Collections.ObjectModel;

namespace DnD.CharSheet.Desktop;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Func<Task> initialize;
    private readonly ICharacterLibrary library;
    private CharacterDraft? activeDraft;
    public CharacterDraft? ActiveDraft
    {
        get => activeDraft;
        private set
        {
            if (!SetProperty(ref activeDraft, value)) return;
            OnPropertyChanged(nameof(ActiveCharacterName));
            OnPropertyChanged(nameof(ActiveCharacterId));
            OnPropertyChanged(nameof(ActiveCharacterStatus));
            OnPropertyChanged(nameof(ActiveCharacterAnnouncement));
        }
    }
    public string ActiveCharacterName => ActiveDraft is { } draft ? $"Active character: {draft.Name}" : "No character selected";
    public string ActiveCharacterId => ActiveDraft is { } draft ? $"Character ID: {draft.Id}" : "";
    public string ActiveCharacterStatus => ActiveDraft is null ? "Activate a saved draft below." : "Incomplete draft";
    public string ActiveCharacterAnnouncement => $"{ActiveCharacterName}. {ActiveCharacterStatus}. {ActiveCharacterId}";
    public bool ShowStartupStatus => IsInitializing || HasFailed;
    partial void OnIsInitializingChanged(bool value) => OnPropertyChanged(nameof(ShowStartupStatus));
    partial void OnHasFailedChanged(bool value) => OnPropertyChanged(nameof(ShowStartupStatus));
    [ObservableProperty] private string databaseStatus = "Loading characters…";
    [ObservableProperty] private bool hasFailed;
    [ObservableProperty] private bool isInitializing = true;
    [ObservableProperty] private bool canCreate;
    [ObservableProperty] private bool canRetryLibrary;
    [ObservableProperty] private bool isCreating;
    [ObservableProperty] private bool hasNameError;
    [ObservableProperty] private bool hasCreationError;
    [ObservableProperty] private bool hasCreationSucceeded;
    [ObservableProperty] private string draftNameInput = "";
    [ObservableProperty] private string nameFeedback = "Use 1–80 characters. Duplicate names are allowed.";
    [ObservableProperty] private string creationStatus = "";
    [ObservableProperty] private string libraryStatus = "Loading characters…";
    private bool running;
    public ObservableCollection<CharacterDraft> Drafts { get; } = [];

    public ShellViewModel(Func<Task> initialize, ICharacterLibrary library)
    {
        this.initialize = initialize;
        this.library = library;
    }

    [RelayCommand]
    private void ActivateDraft(CharacterId? id)
    {
        // Resolve the canonical loaded snapshot, never a caller-supplied name/object.
        var draft = Drafts.FirstOrDefault(candidate => candidate.Id == id);
        if (draft is not null) ActiveDraft = draft;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (running) return;
        running = true;
        CanCreate = false;
        CanRetryLibrary = false;
        HasFailed = false;
        IsInitializing = true;
        DatabaseStatus = "Loading characters…";
        LibraryStatus = "Loading characters…";
        try
        {
            await initialize();
            DatabaseStatus = "";
            await LoadLibraryAsync();
        }
        catch (Exception)
        {
            DatabaseStatus = "Characters could not be loaded. Retry.";
            LibraryStatus = "Characters unavailable.";
            HasFailed = true;
        }
        finally
        {
            IsInitializing = false;
            running = false;
        }
    }

    private async Task LoadLibraryAsync()
    {
        CanCreate = false;
        CanRetryLibrary = false;
        LibraryStatus = "Loading saved characters…";
        try
        {
            var drafts = await library.ListAsync();
            Drafts.Clear();
            foreach (var draft in drafts) Drafts.Add(draft);
            LibraryStatus = Drafts.Count == 0 ? "No characters" : $"{Drafts.Count} saved incomplete drafts.";
            CanCreate = true;
        }
        catch (Exception)
        {
            LibraryStatus = "Saved characters could not be loaded. Retry the library.";
            CanRetryLibrary = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRetryLibrary))]
    private async Task RetryLibraryAsync() => await LoadLibraryAsync();

    partial void OnCanRetryLibraryChanged(bool value) => RetryLibraryCommand.NotifyCanExecuteChanged();
    partial void OnCanCreateChanged(bool value) => CreateDraftCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateDraftAsync()
    {
        if (!CanCreate) return;
        HasNameError = false;
        HasCreationError = false;
        HasCreationSucceeded = false;
        var rawName = DraftNameInput;
        if (rawName.Any(char.IsControl)) NameFeedback = "Use a single line without control characters.";
        else if (string.IsNullOrWhiteSpace(rawName)) NameFeedback = "Enter a character name.";
        else if (rawName.Trim().Length > DraftName.MaximumLength) NameFeedback = "Use 80 characters or fewer.";
        else
        {
            NameFeedback = "Use 1–80 characters. Duplicate names are allowed.";
            CanCreate = false;
            IsCreating = true;
            CreationStatus = "Saving draft…";
            try
            {
                var saved = await library.CreateDraftAsync(CharacterId.New(), rawName);
                DraftNameInput = "";
                HasCreationSucceeded = true;
                CreationStatus = $"Draft “{saved.Name}” saved.";
                // The durable result is shown even if a later library reload fails.
                Drafts.Add(saved);
                LibraryStatus = $"{Drafts.Count} saved incomplete drafts.";
            }
            catch (Exception)
            {
                HasCreationError = true;
                CreationStatus = "Draft could not be saved. Your name is kept; try Create draft again.";
            }
            finally
            {
                IsCreating = false;
                CanCreate = true;
            }
            return;
        }
        HasNameError = true;
        HasCreationError = true;
        CreationStatus = "Draft not created. Check the character name.";
    }
}
