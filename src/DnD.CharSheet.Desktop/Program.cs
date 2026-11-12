using System.Windows;
using DnD.CharSheet.Infrastructure;
using DnD.CharSheet.Desktop.Presentation.Design;

namespace DnD.CharSheet.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var dataDirectory = System.IO.Path.GetDirectoryName(CharacterDatabase.DefaultPath)!;
        System.IO.FileStream session;
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dataDirectory)!);
            session = new System.IO.FileStream(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dataDirectory)!, "session.lock"),
                System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("DnD.CharSheet is already open, or its data folder is unavailable. Close the other window or check folder access and try again.", "DnD.CharSheet");
            return;
        }
        using var sessionOwnership = session;
        var app = new System.Windows.Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml", UriKind.Relative)
        });
        Theme.Apply(app.Resources);
        SystemParameters.StaticPropertyChanged += (_, _) => Theme.Apply(app.Resources);
        var database = new CharacterDatabase(CharacterDatabase.DefaultPath);
        var backups = new FileBackupService(dataDirectory);
        app.Run(new MainWindow(() => Task.Run(() => { backups.RecoverInterruptedRestore(); database.Initialize(); }), new SqliteCharacterLibrary(database), new ManagedCharacterPortraits(database, System.IO.Path.Combine(dataDirectory, "portraits")), new FileDraftRecovery(System.IO.Path.Combine(dataDirectory, "draft-recovery.json")), backups, new SqliteAdventureRepository(database)));
    }
}
