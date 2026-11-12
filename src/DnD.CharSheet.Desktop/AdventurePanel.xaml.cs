using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DnD.CharSheet.Application;

namespace DnD.CharSheet.Desktop;

public partial class AdventurePanel : UserControl
{
    private INotifyPropertyChanged? observed;
    private INotifyCollectionChanged? observedAdventures;

    public AdventurePanel()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => { if (IsVisible) Dispatcher.BeginInvoke(new Action(() => AdventureSearch.Focus())); };
        DataContextChanged += (_, _) =>
        {
            if (observed is not null) observed.PropertyChanged -= ViewModelChanged;
            if (observedAdventures is not null) observedAdventures.CollectionChanged -= AdventuresChanged;
            observed = DataContext as INotifyPropertyChanged;
            observedAdventures = (DataContext as ShellViewModel)?.Adventures;
            if (observed is not null) observed.PropertyChanged += ViewModelChanged;
            if (observedAdventures is not null) observedAdventures.CollectionChanged += AdventuresChanged;
            Dispatcher.BeginInvoke(new Action(UpdateSearch));
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && DataContext is ShellViewModel { IsAdventureBusy: false } shell)
            {
                if (shell.HasAdventureConfirmation)
                {
                    shell.CancelAdventureActionCommand.Execute(null);
                    AdventureSearch.Focus();
                    e.Handled = true;
                }
                else if (CreateForm.IsVisible)
                {
                    CreateForm.Visibility = Visibility.Collapsed;
                    AdventureSearch.Focus();
                    e.Handled = true;
                }
            }
        };
    }

    private void AdventuresChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(UpdateSearch));

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.HasAdventureConfirmation) &&
            DataContext is ShellViewModel { HasAdventureConfirmation: true })
            Dispatcher.BeginInvoke(new Action(() => { CancelConfirmation.Focus(); CancelConfirmation.BringIntoView(); }));
        if (e.PropertyName == nameof(ShellViewModel.AdventureStatus))
            Announce(AdventureStatusText);
        if (e.PropertyName == nameof(ShellViewModel.ConfirmationText))
            Announce(ConfirmationStatusText);
    }

    private static void Announce(FrameworkElement element) =>
        element.Dispatcher.BeginInvoke(new Action(() => UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)));

    private void SearchChanged(object sender, TextChangedEventArgs e) => UpdateSearch();

    private void UpdateSearch()
    {
        if (AdventureList?.ItemsSource is null) return;
        var query = AdventureSearch.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(AdventureList.ItemsSource);
        view.Filter = item => item is Adventure adventure && adventure.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        var text = query.Length > 0 && view.IsEmpty ? "No matching Adventures." : "";
        if (SearchStatus.Text != text)
        {
            SearchStatus.Text = text;
            Announce(SearchStatus);
        }
    }

    private void ClearUnassigned(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell) shell.SelectedUnassigned = null;
        FirstCharacterName.Focus();
    }

    private void ShowCreateForm(object sender, RoutedEventArgs e)
    {
        CreateForm.Visibility = Visibility.Visible;
        NewAdventureName.Focus();
    }

    private void HideCreateForm(object sender, RoutedEventArgs e)
    {
        CreateForm.Visibility = Visibility.Collapsed;
        AdventureSearch.Focus();
    }
}



