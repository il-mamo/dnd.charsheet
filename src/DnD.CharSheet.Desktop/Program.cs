using System.Windows;
using DnD.CharSheet.Infrastructure;
using DnD.CharSheet.Desktop.Presentation.Design;

namespace DnD.CharSheet.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new System.Windows.Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml", UriKind.Relative)
        });
        Theme.Apply(app.Resources);
        SystemParameters.StaticPropertyChanged += (_, _) => Theme.Apply(app.Resources);
        var database = new CharacterDatabase(CharacterDatabase.DefaultPath);
        app.Run(new MainWindow(() => Task.Run(() => { database.Initialize(); })));
    }
}
