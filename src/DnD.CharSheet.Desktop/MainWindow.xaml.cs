using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace DnD.CharSheet.Desktop;

public partial class MainWindow : Window
{
    public static RoutedUICommand SwitchCharacterCommand { get; } = new("Switch character", nameof(SwitchCharacterCommand), typeof(MainWindow));
    public MainWindow(Func<Task> initialize, DnD.CharSheet.Application.ICharacterLibrary library)
    {
        InitializeComponent();
        var model = new ShellViewModel(initialize, library);
        DataContext = model;

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
            if (args.PropertyName is nameof(ShellViewModel.CreationStatus) or nameof(ShellViewModel.NameFeedback) or nameof(ShellViewModel.LibraryStatus))
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var element in new UIElement[] { CreationStatusText, NameFeedbackText, LibraryStatusText })
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

    private void SwitchCharacterExecuted(object sender, ExecutedRoutedEventArgs e)
    {
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
