using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using DnD.CharSheet.Application;
using Microsoft.Win32;

namespace DnD.CharSheet.Desktop;

public partial class BackupWindow : Window
{
    private readonly IBackupService backups;
    private readonly int? currentCharacters;
    private BackupPreview? preview;
    private bool busy;
    public bool WasRestored { get; private set; }

    public BackupWindow(IBackupService backups, int? currentCharacters)
    {
        this.backups = backups;
        this.currentCharacters = currentCharacters;
        InitializeComponent();
        Loaded += (_, _) => Cancel.Focus();
        Closing += (_, args) => args.Cancel = busy;
        Closed += (_, _) => ClearPreview();
        PreviewKeyDown += (_, args) =>
        {
            if (busy && args.Key == Key.Escape) args.Handled = true;
        };
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Create.IsEnabled = Choose.IsEnabled = !value && !WasRestored;
        Cancel.IsEnabled = !value;
        Restore.IsEnabled = !value && preview is not null && !WasRestored;
    }

    private void Announce(string message, bool error = false)
    {
        Status.Text = message;
        Status.SetResourceReference(ForegroundProperty, error ? "Brush.Danger" : "Brush.Text");
        (UIElementAutomationPeer.FromElement(Status) ?? UIElementAutomationPeer.CreatePeerForElement(Status))?
            .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void ClearPreview()
    {
        preview?.Dispose();
        preview = null;
        PreviewPanel.Visibility = Visibility.Collapsed;
        Restore.IsEnabled = false;
    }

    private async void CreateBackup(object sender, RoutedEventArgs args)
    {
        if (busy || WasRestored) return;
        var chooser = new SaveFileDialog
        {
            Title = "Create backup", Filter = "Character backup (*.zip)|*.zip", DefaultExt = ".zip",
            FileName = $"DnD-CharSheet-{DateTime.Now:yyyy-MM-dd-HHmmss}.zip"
        };
        if (chooser.ShowDialog(this) != true) { Create.Focus(); return; }
        SetBusy(true);
        Announce("Creating backup…");
        try
        {
            await backups.CreateAsync(chooser.FileName);
            Announce($"Backup saved to {chooser.FileName}");
        }
        catch (Exception) { Announce("Backup could not be saved. Choose a writable location and try again. Current data is unchanged.", true); }
        finally { SetBusy(false); Cancel.Focus(); }
    }

    private async void ChooseBackup(object sender, RoutedEventArgs args)
    {
        if (busy || WasRestored) return;
        var chooser = new OpenFileDialog { Title = "Choose backup", Filter = "Character backup (*.zip)|*.zip", CheckFileExists = true };
        if (chooser.ShowDialog(this) != true) { Choose.Focus(); return; }
        ClearPreview();
        SetBusy(true);
        Announce("Checking backup…");
        try
        {
            preview = await backups.PreviewAsync(chooser.FileName);
            var current = currentCharacters is { } count ? count.ToString(CultureInfo.CurrentCulture) : "unavailable";
            PreviewText.Text = $"{Path.GetFileName(chooser.FileName)}\nCreated: {preview.CreatedUtc.ToLocalTime():f} ({TimeZoneInfo.Local.DisplayName})\n\nCurrent characters: {current}\nBackup characters: {preview.CharacterCount}\nBackup portraits: {preview.PortraitCount}\nDraft recovery: {(preview.HasDraftRecovery ? "Included" : "None")}\n\nCharacters: {string.Join(", ", preview.CharacterNames)}";
            PreviewPanel.Visibility = Visibility.Visible;
            Announce("Backup checked. Review the replacement details before restoring.");
        }
        catch (Exception) { Announce("This backup could not be opened or is incompatible. Choose a supported backup. Current data is unchanged.", true); }
        finally { SetBusy(false); Cancel.Focus(); }
    }

    private async void RestoreBackup(object sender, RoutedEventArgs args)
    {
        if (busy || preview is null || WasRestored) return;
        SetBusy(true);
        Announce("Saving a safety backup and restoring…");
        try
        {
            var safety = await backups.RestoreAsync(preview);
            WasRestored = true;
            Cancel.Content = "Close";
            Announce($"Backup restored. Close to reload your characters. Safety backup saved to {safety}");
        }
        catch (Exception) { Announce("Backup could not be restored. Your previous data is retained. Check free space, permissions and current data, then try again.", true); }
        finally { SetBusy(false); Cancel.Focus(); }
    }

    private void CancelBackup(object sender, RoutedEventArgs args)
    {
        if (!busy) Close();
    }
}
