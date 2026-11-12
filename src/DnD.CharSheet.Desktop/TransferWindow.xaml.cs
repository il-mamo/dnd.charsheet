using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using DnD.CharSheet.Application;

namespace DnD.CharSheet.Desktop;

public partial class TransferWindow : Window
{
    private readonly ShellViewModel shell;
    private readonly TransferPreview preview;
    private bool busy;
    private bool submitted;
    public bool ManageSource { get; private set; }
    private sealed record Destination(Adventure Adventure, string Label);

    public TransferWindow(ShellViewModel shell, TransferPreview preview, ICharacterPortraits? portraits)
    {
        this.shell = shell;
        this.preview = preview;
        InitializeComponent();
        CharacterName.Text = preview.Character.Name;
        Portrait.CharacterId = preview.Character.Id;
        Portrait.Portraits = portraits;
        Source.Text = $"From Adventure: {preview.Source.Name} · {preview.SourceCount} characters";
        Loaded += (_, _) => Cancel.Focus();
        Closing += (_, e) => e.Cancel = busy;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) { Close(); e.Handled = true; } };
        UpdateOptions();
    }

    private void Say(string text)
    {
        Status.Text = text;
        UIElementAutomationPeer.CreatePeerForElement(Status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (preview is not null && Destinations is not null) UpdateOptions(); }
    private void UpdateOptions()
    {
        var selected = (Destinations.SelectedItem as Destination)?.Adventure.Id;
        var options = preview.Destinations.Where(a => a.Name.Contains(Search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(a => new Destination(a, $"{a.Name} · {preview.Counts.GetValueOrDefault(a.Id)} characters")).ToArray();
        Destinations.ItemsSource = options;
        Destinations.SelectedItem = options.FirstOrDefault(a => a.Adventure.Id == selected);
        UpdatePreview();
    }
    private void DestinationChanged(object sender, SelectionChangedEventArgs e) { if (preview is not null) UpdatePreview(); }
    private void UpdatePreview()
    {
        Confirm.IsEnabled = !submitted && !busy && preview.SourceCount > 1 && Destinations.SelectedItem is Destination;
        if (preview.SourceCount <= 1)
        {
            Manage.Visibility = Visibility.Visible;
            Say("This is the last character in this Adventure. Delete the Adventure in management to keep its character unassigned, then assign it elsewhere.");
        }
        else if (Destinations.SelectedItem is Destination destination)
        {
            Preview.Text = $"Preview: {preview.Source.Name}: {preview.SourceCount} → {preview.SourceCount - 1} characters\n{destination.Adventure.Name}: {preview.Counts.GetValueOrDefault(destination.Adventure.Id)} → {preview.Counts.GetValueOrDefault(destination.Adventure.Id) + 1} characters\nMoves this character to the selected Adventure.";
            Say("Review the Adventures, then choose Transfer character.");
        }
        else
        {
            Preview.Text = "";
            Say(preview.Destinations.Count == 0 ? "No other Adventures. Create one in Adventure management first." : Destinations.Items.Count == 0 ? "No matching Adventures." : "Choose a destination Adventure.");
        }
    }
    private async void ConfirmClicked(object sender, RoutedEventArgs e)
    {
        if (!Confirm.IsEnabled || busy || submitted || Destinations.SelectedItem is not Destination destination) return;
        busy = submitted = true;
        Picker.IsEnabled = Cancel.IsEnabled = Manage.IsEnabled = Confirm.IsEnabled = false;
        Say($"Transferring {preview.Character.Name}…");
        var error = await shell.ConfirmTransferAsync(preview, destination.Adventure);
        busy = false;
        if (error is null) Close();
        else { Say(error); Cancel.IsEnabled = true; Cancel.Focus(); }
    }
    private void CancelClicked(object sender, RoutedEventArgs e) { if (!busy) Close(); }
    private void ManageClicked(object sender, RoutedEventArgs e) { if (!busy) { ManageSource = true; Close(); } }
}
