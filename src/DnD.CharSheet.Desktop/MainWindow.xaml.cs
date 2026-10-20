using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace DnD.CharSheet.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(Func<Task> initialize)
    {
        InitializeComponent();
        var model = new ShellViewModel(initialize);
        DataContext = model;
        Width = Math.Min(1280, SystemParameters.WorkArea.Width);
        Height = Math.Min(720, SystemParameters.WorkArea.Height);
        Loaded += async (_, _) => await model.InitializeCommand.ExecuteAsync(null);
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
}

