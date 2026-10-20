using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DnD.CharSheet.Desktop;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly Func<Task> initialize;
    [ObservableProperty] private string databaseStatus = "Preparing local database…";
    [ObservableProperty] private bool hasFailed;
    [ObservableProperty] private bool isInitializing = true;
    private bool running;

    public ShellViewModel(Func<Task> initialize) => this.initialize = initialize;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (running) return;
        running = true;
        HasFailed = false;
        IsInitializing = true;
        DatabaseStatus = "Preparing local database…";
        try
        {
            await initialize();
            DatabaseStatus = "Local database ready.";
        }
        catch (Exception)
        {
            // Startup failures never reset or replace user data. Retrying uses the same initializer.
            DatabaseStatus = "Local database could not be initialized.";
            HasFailed = true;
        }
        finally
        {
            IsInitializing = false;
            running = false;
        }
    }
}
