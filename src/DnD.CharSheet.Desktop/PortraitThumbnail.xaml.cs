using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DnD.CharSheet.Application;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Desktop;

public partial class PortraitThumbnail : UserControl
{
    public static readonly DependencyProperty CharacterIdProperty = DependencyProperty.Register(nameof(CharacterId), typeof(CharacterId), typeof(PortraitThumbnail), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty PortraitsProperty = DependencyProperty.Register(nameof(Portraits), typeof(ICharacterPortraits), typeof(PortraitThumbnail), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty RefreshProperty = DependencyProperty.Register(nameof(Refresh), typeof(int), typeof(PortraitThumbnail), new PropertyMetadata(0, Changed));
    public CharacterId? CharacterId { get => (CharacterId?)GetValue(CharacterIdProperty); set => SetValue(CharacterIdProperty, value); }
    public ICharacterPortraits? Portraits { get => (ICharacterPortraits?)GetValue(PortraitsProperty); set => SetValue(PortraitsProperty, value); }
    public int Refresh { get => (int)GetValue(RefreshProperty); set => SetValue(RefreshProperty, value); }
    private int generation;
    public PortraitThumbnail()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        SizeChanged += (_, _) => ((Grid)Content).Clip = new EllipseGeometry(new Point(ActualWidth / 2, ActualHeight / 2), ActualWidth / 2, ActualHeight / 2);
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((PortraitThumbnail)sender).Reload();
    private async void Reload()
    {
        if (!IsLoaded) return;
        var request = ++generation;
        Picture.Source = null;
        Fallback.Visibility = Visibility.Visible;
        ToolTip = "No portrait";
        if (CharacterId is not { } id || Portraits is not { } store) return;
        try
        {
            var saved = await store.ReadAsync(id);
            var image = saved.Png is null ? null : await Task.Run(() => PortraitCodec.Decode(saved.Png));
            if (request != generation) return;
            Picture.Source = image;
            Fallback.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
            ToolTip = image is not null ? "Character portrait" : saved.Token is null ? "No portrait" : "Portrait unavailable";
        }
        catch (Exception)
        {
            if (request == generation) ToolTip = "Portrait unavailable";
        }
    }
}
