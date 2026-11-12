using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace DnD.CharSheet.Desktop;

public partial class MainWindow : Window
{
    private readonly DnD.CharSheet.Application.IBackupService? backups;
    private bool backupOpen;
    private bool transferOpen;
    public static RoutedUICommand SwitchCharacterCommand { get; } = new("Switch character", nameof(SwitchCharacterCommand), typeof(MainWindow));
    public DnD.CharSheet.Application.ICharacterPortraits? Portraits { get; }
    public static readonly DependencyProperty PortraitRefreshProperty = DependencyProperty.Register(nameof(PortraitRefresh), typeof(int), typeof(MainWindow), new PropertyMetadata(0));
    public int PortraitRefresh { get => (int)GetValue(PortraitRefreshProperty); set => SetValue(PortraitRefreshProperty, value); }
    public MainWindow(Func<Task> initialize, DnD.CharSheet.Application.ICharacterLibrary library, DnD.CharSheet.Application.ICharacterPortraits? portraits = null, DnD.CharSheet.Application.IDraftRecovery? recovery = null, DnD.CharSheet.Application.IBackupService? backups = null, DnD.CharSheet.Application.IAdventureRepository? adventures = null)
    {
        this.backups = backups;
        Portraits = portraits;
        InitializeComponent();
        BackupsButton.IsEnabled = backups is not null;
        var model = new ShellViewModel(initialize, library, recovery, adventures);
        DataContext = model;
        var closing = false;
        var closeReady = false;
        Closing += async (_, args) =>
        {
            if (backupOpen || transferOpen) { args.Cancel = true; return; }
            if (closeReady) return;
            args.Cancel = true;
            if (closing) return;
            closing = true;
            try
            {
                if (await model.PrepareToCloseAsync())
                {
                    closeReady = true;
                    // The drain can complete synchronously inside WPF's Closing event.
                    // Re-enter Close only after that event has returned.
                    await Dispatcher.InvokeAsync(new Action(Close));
                }
            }
            finally { closing = false; }
        };
        Loaded += async (_, _) => await model.InitializeCommand.ExecuteAsync(null);
        var focusedName = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.CanCreate) && model.CanCreate && !focusedName)
            {
                focusedName = true;
                DraftNameBox.Focus();
            }
            if (args.PropertyName == nameof(ShellViewModel.IsCreating) && !model.IsCreating && NewDraftPanel.IsKeyboardFocusWithin)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (NewDraftPanel.IsKeyboardFocusWithin) DraftNameBox.Focus();
                }));
            if (args.PropertyName == nameof(ShellViewModel.ActiveDraft))
                Dispatcher.BeginInvoke(new Action(() =>
                    (UIElementAutomationPeer.FromElement(ActiveCharacterText) ?? UIElementAutomationPeer.CreatePeerForElement(ActiveCharacterText))?
                        .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)));
            if (args.PropertyName is nameof(ShellViewModel.CreationStatus) or nameof(ShellViewModel.NameFeedback) or nameof(ShellViewModel.LibraryStatus) or nameof(ShellViewModel.RecoveryStatus))
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var element in new UIElement[] { CreationStatusText, NameFeedbackText, LibraryStatusText, RecoveryStatusText })
                        (UIElementAutomationPeer.FromElement(element) ?? UIElementAutomationPeer.CreatePeerForElement(element))?
                            .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }));
        };
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.DatabaseStatus))
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    (UIElementAutomationPeer.FromElement(DatabaseStatusText) ??
                     UIElementAutomationPeer.CreatePeerForElement(DatabaseStatusText))?
                    .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)));
            if (args.PropertyName == nameof(ShellViewModel.IsInitializing) && model.IsInitializing)
                Focus();
        };
    }

    private void MinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();

    private async void OpenBackups(object sender, RoutedEventArgs e)
    {
        if (backupOpen || transferOpen || backups is null || DataContext is not ShellViewModel model) return;
        backupOpen = true;
        IsEnabled = false;
        try
        {
            // The owner remains unavailable while pending operations finish and throughout the modal.
            foreach (var operation in new Task?[] { model.InitializeCommand.ExecutionTask, model.RetryLibraryCommand.ExecutionTask, model.RetryRecoveryCommand.ExecutionTask })
                if (operation is not null) await operation;
            if (!await model.PrepareToCloseAsync()) return;
            var dialog = new BackupWindow(backups, model.HasFailed || model.CanRetryLibrary ? null : model.AllCharacterCount) { Owner = this };
            dialog.ShowDialog();
            if (dialog.WasRestored)
            {
                await model.ReloadAfterRestoreAsync();
                PortraitRefresh++;
            }
        }
        catch (Exception) { model.AnnounceBackupFailure(); }
        finally
        {
            IsEnabled = true;
            backupOpen = false;
            BackupsButton.Focus();
        }
    }

    public async void TransferCharacter(object sender, RoutedEventArgs e)
    {
        if (transferOpen || backupOpen || DataContext is not ShellViewModel shell ||
            (sender as FrameworkElement)?.DataContext is not DnD.CharSheet.Application.CharacterDraft character) return;
        transferOpen = true;
        TransferPreview? preview = null;
        try
        {
            preview = await shell.BeginTransferAsync(character);
            if (preview is null) return;
            var dialog = new TransferWindow(shell, preview, Portraits) { Owner = this };
            dialog.ShowDialog();
            shell.EndTransfer(preview, dialog.ManageSource);
            preview = null;
            if (sender is not UIElement { IsVisible: true } element || !element.Focus())
            {
                if (shell.ShowAdventurePanel) AdventureRoster.Focus();
                else SavedCharactersHeading.Focus();
            }
        }
        finally
        {
            if (preview is not null) shell.EndTransfer(preview, false);
            transferOpen = false;
        }
    }

    private void ChangePortrait(object sender, RoutedEventArgs e)
    {
        if (Portraits is null || DataContext is not ShellViewModel model) return;
        var character = (sender as FrameworkElement)?.DataContext as DnD.CharSheet.Application.CharacterDraft ?? model.ActiveDraft;
        if (character is null || model.ShowAdventurePanel || !model.Drafts.Any(d => d.Id == character.Id) || model.IsAdventureBusy) return;
        var dialog = new PortraitWindow(Portraits, character) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            PortraitRefresh++;
            model.AnnouncePortraitSaved(character.Name);
        }
        (sender as UIElement)?.Focus();
    }

    private void SwitchCharacterExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel model || !model.ShowCharacterLibrary) { e.Handled = true; return; }
        SavedCharacters.UpdateLayout();
        var target = FindActivationButton(SavedCharacters);
        FrameworkElement focusTarget = target is null ? DraftNameBox : target;
        focusTarget.BringIntoView();
        focusTarget.Focus();
        e.Handled = true;
    }

    private static Button? FindActivationButton(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button button) return button;
            if (FindActivationButton(child) is { } found) return found;
        }
        return null;
    }

    private async void DraftNameKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is ShellViewModel model && model.CreateDraftCommand.CanExecute(null))
        {
            e.Handled = true;
            await model.CreateDraftCommand.ExecuteAsync(null);
        }
    }
}
