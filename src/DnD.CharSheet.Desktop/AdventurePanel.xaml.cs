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
    private FrameworkElement? createInvoker;
    private FrameworkElement? managementInvoker;
    private string selectionSearch = "";

    public AdventurePanel()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) return;
            WelcomeRoot.Visibility = Visibility.Visible;
            ManagementRoot.Visibility = Visibility.Collapsed;
            CreateOverlay.Visibility = Visibility.Collapsed;
            Dispatcher.BeginInvoke(new Action(() => AdventureSearch.Focus()));
        };
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
                else if (CreateOverlay.IsVisible)
                {
                    HideCreateForm(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (ManagementRoot.IsVisible)
                {
                    BackToWelcome(this, new RoutedEventArgs());
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
        {
            Announce(AdventureStatusText);
            if (DataContext is ShellViewModel { HasFailed: false } shell && shell.Adventures.Count == 0 && WelcomeRoot.IsVisible)
                Dispatcher.BeginInvoke(new Action(() => WelcomeCreateButton.Focus()));
        }
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

    private void TransferMember(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow window) window.TransferCharacter(sender, e);
    }

    private void ClearUnassigned(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell) shell.SelectedUnassigned = null;
        FirstCharacterName.Focus();
    }

    private void ShowCreateForm(object sender, RoutedEventArgs e)
    {
        createInvoker = sender as FrameworkElement;
        CreateOverlay.Visibility = Visibility.Visible;
        NewAdventureName.Focus();
    }

    private void HideCreateForm(object sender, RoutedEventArgs e)
    {
        CreateOverlay.Visibility = Visibility.Collapsed;
        if (createInvoker?.IsVisible == true) createInvoker.Focus();
        else if (ManagementRoot.IsVisible) ManagementBackButton.Focus();
        else AdventureSearch.Focus();
    }

    private void ShowManagement(object sender, RoutedEventArgs e)
    {
        managementInvoker = sender as FrameworkElement;
        selectionSearch = AdventureSearch.Text;
        AdventureSearch.Clear();
        UpdateSearch();
        WelcomeRoot.Visibility = Visibility.Collapsed;
        ManagementRoot.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() => ManagementBackButton.Focus()));
    }

    private void BackToWelcome(object sender, RoutedEventArgs e)
    {
        ManagementRoot.Visibility = Visibility.Collapsed;
        WelcomeRoot.Visibility = Visibility.Visible;
        AdventureSearch.Text = selectionSearch;
        UpdateSearch();
        if (managementInvoker?.IsVisible == true) managementInvoker.Focus();
        else AdventureSearch.Focus();
    }
}



