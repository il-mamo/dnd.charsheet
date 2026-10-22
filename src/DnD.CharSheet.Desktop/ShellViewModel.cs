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
    private readonly IDraftRecovery? recovery;
    private bool recoveryLoaded;
    private bool libraryLoaded;
    private bool restoring;
    private bool closing;
    private CharacterId? submittedId;
    private Task recoveryWrites = Task.CompletedTask;
    private long recoveryVersion;
    [ObservableProperty] private string recoveryStatus = "Loading draft recovery…";
    [ObservableProperty] private bool hasRecoveryError;
    public bool CanEditDraftName => recoveryLoaded && !IsCreating && !closing && submittedId is null;
    public string CreateDraftLabel => submittedId is null ? "Create draft" : "Retry create";
    partial void OnIsCreatingChanged(bool value) => OnPropertyChanged(nameof(CanEditDraftName));

    partial void OnDraftNameInputChanged(string value)
    {
        if (restoring || recovery is null || !recoveryLoaded) return;
        QueueRecoveryWrite(new DraftRecovery(value));
    }

    private void QueueRecoveryWrite(DraftRecovery snapshot)
    {
        var version = ++recoveryVersion;
        RecoveryStatus = "Saving name…";
        HasRecoveryError = false;
        recoveryWrites = PersistNameAsync(recoveryWrites, snapshot, version);
    }

    private async Task PersistNameAsync(Task previous, DraftRecovery snapshot, long version)
    {
        await previous;
        try
        {
            await recovery!.SaveAsync(snapshot);
            if (version == recoveryVersion)
            {
                RecoveryStatus = "Name saved for recovery.";
                HasRecoveryError = false;
            }
        }
        catch (Exception)
        {
            if (version == recoveryVersion)
            {
                RecoveryStatus = "Name could not be saved for recovery.";
                HasRecoveryError = true;
            }
        }
    }

    private async Task LoadRecoveryAsync()
    {
        if (recoveryLoaded) return;
        try
        {
            var snapshot = recovery is null ? null : await recovery.LoadAsync();
            restoring = true;
            DraftNameInput = snapshot?.Name ?? "";
            restoring = false;
            submittedId = snapshot?.SubmittedId;
            recoveryLoaded = true;
            HasRecoveryError = false;
            RecoveryStatus = snapshot is null ? "" : "Recovered your unfinished draft name.";
            if (submittedId is not null)
                CreationStatus = "Draft creation could not be confirmed. Retry to check and finish.";
        }
        catch (Exception)
        {
            RecoveryStatus = "Draft recovery could not be opened.";
            HasRecoveryError = true;
        }
        finally
        {
            restoring = false;
            OnPropertyChanged(nameof(CanEditDraftName));
            OnPropertyChanged(nameof(CreateDraftLabel));
            CanCreate = recoveryLoaded && libraryLoaded;
        }
    }

    [RelayCommand]
    private async Task RetryRecoveryAsync()
    {
        if (IsCreating || closing) return;
        if (!recoveryLoaded) await LoadRecoveryAsync();
        else if (submittedId is not null && CanCreate) await CreateDraftCommand.ExecuteAsync(null);
        else if (submittedId is null)
        {
            QueueRecoveryWrite(new DraftRecovery(DraftNameInput));
            await recoveryWrites;
        }
    }

    public async Task<bool> PrepareToCloseAsync()
    {
        closing = true;
        CanCreate = false;
        OnPropertyChanged(nameof(CanEditDraftName));
        if (!recoveryWrites.IsCompleted) RecoveryStatus = "Saving name before closing…";
        if (CreateDraftCommand.ExecutionTask is { } creation) await creation;
        await recoveryWrites;
        // A failed load never modified the journal; it is safe to leave it untouched.
        var safe = !recoveryLoaded || !HasRecoveryError;
        closing = false;
        CanCreate = recoveryLoaded && libraryLoaded;
        OnPropertyChanged(nameof(CanEditDraftName));
        return safe;
    }

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
    public void AnnouncePortraitSaved(string name) => LibraryStatus = $"Portrait for {name} saved.";
    private bool running;
    public ObservableCollection<CharacterDraft> Drafts { get; } = [];

    public ShellViewModel(Func<Task> initialize, ICharacterLibrary library, IDraftRecovery? recovery = null)
    {
        this.initialize = initialize;
        this.library = library;
        this.recovery = recovery;
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
            await LoadRecoveryAsync();
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
            libraryLoaded = true;
            CanCreate = recoveryLoaded;
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
            submittedId ??= CharacterId.New();
            OnPropertyChanged(nameof(CanEditDraftName));
            OnPropertyChanged(nameof(CreateDraftLabel));
            CharacterDraft? saved = null;
            var intentPersisted = false;
            try
            {
                // All prior input writes finish before the durable submission replaces them.
                await recoveryWrites;
                if (recovery is not null)
                    await recovery.SaveAsync(new DraftRecovery(rawName, submittedId));
                intentPersisted = true;
                RecoveryStatus = "Name saved for recovery.";
                HasRecoveryError = false;
                // A previous attempt may have committed before its result was observed.
                saved = (await library.ListAsync()).FirstOrDefault(draft => draft.Id == submittedId);
                if (saved is not null && saved.Name != DraftName.Normalize(rawName))
                    throw new InvalidOperationException("Submitted identity has different metadata.");
                saved ??= await library.CreateDraftAsync(submittedId, rawName);
                if (!Drafts.Any(draft => draft.Id == saved.Id)) Drafts.Add(saved);
                LibraryStatus = $"{Drafts.Count} saved incomplete drafts.";
                CreationStatus = "Character saved. Finishing recovery…";
                if (recovery is not null) await recovery.SaveAsync(null);
                restoring = true;
                DraftNameInput = "";
                restoring = false;
                submittedId = null;
                RecoveryStatus = "";
                HasCreationSucceeded = true;
                CreationStatus = $"Draft “{saved.Name}” saved.";
            }
            catch (Exception)
            {
                if (!intentPersisted)
                {
                    HasRecoveryError = true;
                    RecoveryStatus = "Name could not be saved for recovery.";
                }
                HasCreationError = true;
                CreationStatus = saved is null
                    ? "Draft creation could not be confirmed. Retry to check and finish."
                    : "Character saved. Retry to finish recovery.";
            }
            finally
            {
                IsCreating = false;
                CanCreate = recoveryLoaded && libraryLoaded;
                OnPropertyChanged(nameof(CanEditDraftName));
                OnPropertyChanged(nameof(CreateDraftLabel));
            }
            return;
        }
        HasNameError = true;
        HasCreationError = true;
        CreationStatus = "Draft not created. Check the character name.";
    }
}
