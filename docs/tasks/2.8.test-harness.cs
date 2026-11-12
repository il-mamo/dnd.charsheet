using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DnD.CharSheet.Application;
using DnD.CharSheet.Desktop;
using DnD.CharSheet.Domain;
using DnD.CharSheet.Infrastructure;

static class Program
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name); checks++;
    }
    static void Run(Task task) => task.GetAwaiter().GetResult();
    static T Get<T>(Task<T> task) => task.GetAwaiter().GetResult();
    static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);

    [STAThread]
    static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "charsheet-28-independent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app = new System.Windows.Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml", UriKind.Relative) });
        typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.Presentation.Design.Theme")!.GetMethod("Apply")!.Invoke(null, [app.Resources]);
        try
        {
            var database = new CharacterDatabase(Path.Combine(root, "characters.db")); database.Initialize();
            var library = new SqliteCharacterLibrary(database);
            var repository = new SqliteAdventureRepository(database);
            var guarded = new GuardedRepository(repository);
            var recovery = new FileDraftRecovery(Path.Combine(root, "draft-recovery.json"));
            var a = AdventureId.New(); var b = AdventureId.New(); var c = AdventureId.New();
            var moving = CharacterId.New(); var retained = CharacterId.New();
            Run(repository.CreateWithDraftAsync(a, new string('A', 80), moving, new string('H', 80)));
            Run(library.CreateDraftAsync(retained, "Retained")); Run(repository.AssignAsync(retained, a));
            Run(repository.CreateWithDraftAsync(b, new string('B', 80), CharacterId.New(), "Destination"));
            Run(repository.CreateWithDraftAsync(c, "Other", CharacterId.New(), "Other member"));
            var shell = new ShellViewModel(() => Task.CompletedTask, library, recovery, guarded);
            Run(shell.InitializeAsync()); Run(shell.OpenAdventureCommand.ExecuteAsync(shell.Adventures.Single(x => x.Id == a)));
            Run(shell.ActivateDraftCommand.ExecuteAsync(moving)); shell.DraftNameInput = "Pending source input";
            var character = shell.Drafts.Single(x => x.Id == moving);
            var preview = Get(shell.BeginTransferAsync(character))!;
            Check(shell.IsAdventureBusy && preview.Character.Id == moving && preview.Source.Id == a, "preview captures character/source and owns navigation guard");
            Check(Get(recovery.LoadAllAsync()).Any(x => x.AdventureId == a && x.Name == "Pending source input"), "pending recovery drains into original Adventure before transfer");
            Check(preview.SourceCount == 2 && preview.Destinations.Count == 2 && preview.Destinations.All(x => x.Id != a), "preview counts members and excludes source by identity");
            Run(shell.ReturnToSelectionCommand.ExecuteAsync(null));
            Check(shell.SelectedAdventure?.Id == a && shell.ActiveDraft?.Id == moving, "navigation cannot change session during preview");
            var window = new TransferWindow(shell, preview, null);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(944, 601)); content.Arrange(new Rect(0, 0, 944, 601)); content.UpdateLayout();
            var destinations = Control<ListBox>(window, "Destinations"); var confirm = Control<Button>(window, "Confirm");
            Check(window.Width == 960 && window.Height == 640 && destinations.SelectedItem is null && !confirm.IsEnabled, "960x640 modal initially has no destination and cannot confirm");
            Control<TextBox>(window, "Search").Text = "no such Adventure";
            Check(destinations.Items.Count == 0 && Control<TextBlock>(window, "Status").Text == "No matching Adventures.", "search distinguishes no matches");
            Control<TextBox>(window, "Search").Text = ""; destinations.SelectedIndex = 0;
            Check(confirm.IsEnabled && Control<TextBlock>(window, "Preview").Text.Contains("2 → 1") && Control<TextBlock>(window, "Preview").Text.Contains("1 → 2"), "valid selection previews both counts and enables explicit confirmation");
            content.Measure(new Size(944, 601)); content.Arrange(new Rect(0, 0, 944, 601)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(944, 601, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(Path.Combine(Environment.CurrentDirectory, "transfer-long-names.png"))) encoder.Save(output);
            Check(confirm.TranslatePoint(new Point(), content).Y + confirm.ActualHeight <= 601 && confirm.ActualHeight >= 40, "long-name layout keeps confirmation footer within modal bounds");
            Check(Get(repository.GetMembershipAsync(moving)).AdventureId == a, "destination selection writes no membership");
            Control<TextBox>(window, "Search").Text = "no such Adventure";
            Check(!confirm.IsEnabled && destinations.SelectedItem is null, "filtering selected destination out removes hidden confirmation");
            window.Close(); shell.EndTransfer(preview, false);
            Check(Get(repository.GetMembershipAsync(moving)).AdventureId == a && shell.ActiveDraft?.Id == moving && !shell.IsAdventureBusy, "cancel leaves membership/active identity intact and releases guard");

            preview = Get(shell.BeginTransferAsync(character))!;
            var destination = preview.Destinations.Single(x => x.Id == b);
            guarded.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            guarded.FailReadAfterTransfer = true;
            var pending = shell.ConfirmTransferAsync(preview, destination);
            Check(!pending.IsCompleted && shell.IsAdventureBusy, "pending transfer keeps navigation guarded");
            Check(Get(shell.ConfirmTransferAsync(preview, destination)) is not null && guarded.TransferCalls == 1, "duplicate confirm cannot submit second mutation");
            Check(!Get(shell.PrepareToCloseAsync()), "pending transfer prevents parent close");
            guarded.Gate.SetResult(); Check(Get(pending) is null, "confirmed move reports success after commit");
            shell.EndTransfer(preview, false);
            Check(shell.SelectedAdventure?.Id == a && shell.ActiveDraft is null && shell.Drafts.All(x => x.Id != moving), "success clears moved active character and source row without opening destination");
            Check(shell.LibraryStatus.Contains("Moved") && shell.LibraryStatus.Contains("Refresh Adventures"), "committed transfer remains reported as success when list refresh fails");
            Check(Get(shell.ConfirmTransferAsync(preview, destination)) is not null && guarded.TransferCalls == 1, "refresh failure cannot resubmit consumed transfer intent");
            Run(shell.RefreshAdventuresCommand.ExecuteAsync(null));
            Check(shell.AdventureMemberCounts[a] == 1 && shell.AdventureMemberCounts[b] == 2 && shell.LibraryStatus.Contains("Moved"), "success refreshes both counts and reports named move");
            Check(shell.DraftNameInput == "Pending source input", "transfer does not redirect or clear unfinished source input");

            preview = Get(shell.BeginTransferAsync(shell.Drafts.Single()))!;
            window = new TransferWindow(shell, preview, null);
            Control<ListBox>(window, "Destinations").SelectedIndex = 0;
            Check(!Control<Button>(window, "Confirm").IsEnabled && Control<Button>(window, "Manage").Visibility == Visibility.Visible && Control<TextBlock>(window, "Status").Text.Contains("last character"), "last-member modal blocks confirmation and offers management route");
            window.Close(); shell.EndTransfer(preview, true);
            Check(shell.ShowAdventurePanel && shell.ManagementSelection?.Id == a && Get(repository.GetMembershipAsync(retained)).AdventureId == a, "management route previews source without deleting or unassigning");

            shell.ManagementSelection = shell.Adventures.Single(x => x.Id == b);
            preview = Get(shell.BeginTransferAsync(shell.PreviewMembers.Single(x => x.Id == moving)))!;
            Check(preview.Source.Id == b && shell.SelectedAdventure?.Id == a, "management entry captures management source independently of session Adventure");
            Run(repository.TransferAsync(moving, b, c));
            Check(Get(shell.ConfirmTransferAsync(preview, preview.Destinations.Single(x => x.Id == a))) is not null, "stale source membership fails confirmation");
            shell.EndTransfer(preview, false);
            Check(Get(repository.GetMembershipAsync(moving)).AdventureId == c && shell.SelectedAdventure?.Id == a, "stale confirmation does not overwrite current membership or session");

            Run(shell.RefreshAdventuresCommand.ExecuteAsync(null)); shell.ManagementSelection = shell.Adventures.Single(x => x.Id == c);
            preview = Get(shell.BeginTransferAsync(shell.PreviewMembers.Single(x => x.Id == moving)))!;
            Run(repository.DeleteAsync(b));
            Check(Get(shell.ConfirmTransferAsync(preview, preview.Destinations.Single(x => x.Id == b))) is not null, "deleted destination fails confirmation");
            shell.EndTransfer(preview, false);
            Check(Get(repository.GetMembershipAsync(moving)).AdventureId == c, "deleted destination leaves source membership intact");

            var restart = new ShellViewModel(() => Task.CompletedTask, library, recovery, repository); Run(restart.InitializeAsync());
            Check(restart.SelectedAdventure is null && restart.ActiveDraft is null, "restart requires explicit Adventure selection after move");
            Run(restart.OpenAdventureCommand.ExecuteAsync(restart.Adventures.Single(x => x.Id == c)));
            Check(restart.Drafts.Any(x => x.Id == moving), "restart destination roster includes transferred identity");
            Console.WriteLine($"{checks} independent checks passed. Native keyboard/assistive/high-contrast checks unrun.");
        }
        finally { app.Shutdown(); Directory.Delete(root, true); }
    }

    sealed class GuardedRepository(IAdventureRepository inner) : IAdventureRepository
    {
        public TaskCompletionSource? Gate { get; set; }
        public int TransferCalls { get; private set; }
        public bool FailReadAfterTransfer { get; set; }
        private bool failNextRead;
        public async Task TransferAsync(CharacterId character, AdventureId source, AdventureId destination, CancellationToken cancellationToken = default)
        {
            TransferCalls++; if (Gate is not null) await Gate.Task;
            await inner.TransferAsync(character, source, destination, cancellationToken);
            if (FailReadAfterTransfer) { failNextRead = true; FailReadAfterTransfer = false; }
        }
        public Task<IReadOnlyList<Adventure>> ListAsync(CancellationToken token = default)
        {
            if (failNextRead) { failNextRead = false; throw new IOException("Injected refresh failure after commit"); }
            return inner.ListAsync(token);
        }
        public Task<CharacterMembership> GetMembershipAsync(CharacterId id, CancellationToken token = default) => inner.GetMembershipAsync(id, token);
        public Task CreateWithUnassignedAsync(AdventureId id, string name, CharacterId first, CancellationToken token = default) => inner.CreateWithUnassignedAsync(id, name, first, token);
        public Task CreateWithDraftAsync(AdventureId id, string name, CharacterId first, string characterName, CancellationToken token = default) => inner.CreateWithDraftAsync(id, name, first, characterName, token);
        public Task AssignAsync(CharacterId character, AdventureId destination, CancellationToken token = default) => inner.AssignAsync(character, destination, token);
        public Task RenameAsync(AdventureId id, string name, CancellationToken token = default) => inner.RenameAsync(id, name, token);
        public Task DeleteAsync(AdventureId id, CancellationToken token = default) => inner.DeleteAsync(id, token);
    }
}
