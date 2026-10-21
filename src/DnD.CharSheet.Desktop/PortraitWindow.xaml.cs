using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;
using Microsoft.Win32;

namespace DnD.CharSheet.Desktop;

public partial class PortraitWindow : Window
{
    private readonly ICharacterPortraits portraits;
    private readonly CharacterId characterId;
    private string? token;
    private BitmapSource? source;
    private byte[]? pending;
    private bool busy = true;
    private bool saving;
    private bool dirty;
    private bool ready;

    public PortraitWindow(ICharacterPortraits portraits, CharacterDraft character)
    {
        this.portraits = portraits;
        characterId = character.Id;
        InitializeComponent();
        Heading.Text = $"Portrait — {character.Name}";
        Title = $"Portrait — {character.Name}";
        System.Windows.Automation.AutomationProperties.SetName(Square, $"Portrait preview for {character.Name}");
        Loaded += async (_, _) => await LoadPortrait();
        Closing += PreventCloseDuringSave;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !saving) { e.Handled = true; DialogResult = false; } };
        UpdateControls();
    }

    private async Task LoadPortrait()
    {
        SetStatus("Loading portrait…");
        try
        {
            var saved = await portraits.ReadAsync(characterId);
            token = saved.Token;
            if (saved.Png is not null)
            {
                try
                {
                    source = await Task.Run(() => PortraitCodec.Decode(saved.Png));
                    Original.Source = source;
                    Square.Source = Circle.Source = source;
                    Empty.Visibility = Visibility.Collapsed;
                }
                catch (Exception) { Empty.Text = "Portrait unavailable"; }
            }
            else Empty.Text = token is null ? "No portrait" : "Portrait unavailable";
            ready = true;
            SetStatus(Empty.Visibility == Visibility.Visible ? Empty.Text : "Choose an image to replace this portrait.");
        }
        catch (Exception) { SetStatus("Portrait could not be loaded. Close and try again."); }
        finally { busy = false; UpdateControls(); Choose.Focus(); }
    }

    private async void ChooseImage(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Choose portrait image", Filter = "Portrait images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        busy = true;
        UpdateControls();
        SetStatus("Reading image…");
        try
        {
            var decoded = await Task.Run(() => PortraitCodec.Decode(PortraitCodec.ReadImport(picker.FileName)));
            // Complete validation/normalization before replacing a prior valid pending choice.
            var normalized = PortraitCodec.Normalize(decoded, crop: false);
            source = decoded;
            Fit.IsChecked = true;
            pending = normalized;
            dirty = true;
            Original.Source = source;
            Empty.Visibility = Visibility.Collapsed;
            Filename.Text = System.IO.Path.GetFileName(picker.FileName);
            Filename.ToolTip = Filename.Text;
            System.Windows.Automation.AutomationProperties.SetName(Filename, Filename.Text);
            Validation.Text = "";
            ShowPending();
            SetStatus("Preview ready. Apply portrait to save.");
        }
        catch (Exception ex)
        {
            Validation.Text = ex is System.IO.InvalidDataException ? ex.Message : "Image could not be read. Choose a valid JPEG or PNG within the limits above.";
            SetStatus(Validation.Text);
        }
        finally { busy = false; UpdateControls(); }
    }

    private void FramingChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || busy || source is null || !dirty) return;
        pending = PortraitCodec.Normalize(source, Crop.IsChecked == true);
        ShowPending();
    }
    private void ShowPending()
    {
        Square.Source = Circle.Source = pending is null ? (ImageSource)FindResource("PortraitFallback") : PortraitCodec.Decode(pending);
    }
    private void RemovePortrait(object sender, RoutedEventArgs e)
    {
        pending = null;
        source = null;
        Original.Source = Square.Source = Circle.Source = (ImageSource)FindResource("PortraitFallback");
        Empty.Visibility = Visibility.Visible;
        Empty.Text = "No portrait";
        dirty = true;
        SetStatus("Portrait will be removed.");
        UpdateControls();
    }
    private async void ApplyPortrait(object sender, RoutedEventArgs e)
    {
        if (busy || !dirty || !ready) return;
        busy = saving = true;
        UpdateControls();
        SetStatus("Saving portrait…");
        try
        {
            await portraits.SaveAsync(characterId, token, pending);
            saving = false;
            DialogResult = true;
        }
        catch (CharacterConflictException)
        {
            ready = false;
            SetStatus("Portrait changed elsewhere. Cancel and reopen before trying again.");
        }
        catch (Exception) { SetStatus("Portrait could not be saved. Try again."); }
        finally { busy = saving = false; UpdateControls(); }
    }
    private void CancelPortrait(object sender, RoutedEventArgs e) { if (!saving) DialogResult = false; }
    private void PreventCloseDuringSave(object? sender, CancelEventArgs e) { if (saving) e.Cancel = true; }
    private void UpdateControls()
    {
        Choose.IsEnabled = ready && !busy;
        Fit.IsEnabled = Crop.IsEnabled = !busy && dirty && source is not null;
        Remove.IsEnabled = ready && !busy && (token is not null || pending is not null);
        Apply.IsEnabled = ready && !busy && dirty;
        Cancel.IsEnabled = !saving;
    }
    private void SetStatus(string text)
    {
        Status.Text = text;
        (UIElementAutomationPeer.FromElement(Status) ?? UIElementAutomationPeer.CreatePeerForElement(Status))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}


