using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace DnD.CharSheet.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(Func<Task> initialize, DnD.CharSheet.Application.ICharacterLibrary library)
    {
        InitializeComponent();
        var model = new ShellViewModel(initialize, library);
        DataContext = model;
        Width = Math.Min(1280, SystemParameters.WorkArea.Width);
        Height = Math.Min(720, SystemParameters.WorkArea.Height);
        Loaded += async (_, _) => await model.InitializeCommand.ExecuteAsync(null);
        var focusedName = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.CanCreate) && model.CanCreate && !focusedName)
            {
                focusedName = true;
                DraftNameBox.Focus();
            }
            if (args.PropertyName == nameof(ShellViewModel.IsCreating) && !model.IsCreating)
                Dispatcher.BeginInvoke(new Action(() => DraftNameBox.Focus()));
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

    private async void DraftNameKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is ShellViewModel model && model.CreateDraftCommand.CanExecute(null))
        {
            e.Handled = true;
            await model.CreateDraftCommand.ExecuteAsync(null);
        }
    }
}
