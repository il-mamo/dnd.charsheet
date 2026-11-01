using System.Reflection;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DnD.CharSheet.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using DnD.CharSheet.Application;
using DnD.CharSheet.Desktop;
using DnD.CharSheet.Domain;

static class Program
{
 static int checks;
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 [STAThread] static void Main()
 {
  var app=new System.Windows.Application();
  app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml",UriKind.Relative)});
  typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.Presentation.Design.Theme")!.GetMethod("Apply")!.Invoke(null,[app.Resources]);
  var library=new Library();var recovery=new Recovery();var vm=new ShellViewModel(()=>Task.CompletedTask,library,recovery);
  vm.InitializeAsync().GetAwaiter().GetResult();
  vm.ActivateDraftCommand.Execute(library.Items[0].Id);vm.DraftNameInput="Previous unfinished";
  Check(vm.ActiveDraft is not null&&recovery.Writes==1,"old active identity and input present before restore");
  library.Items=[new(CharacterId.New(),new(0),"Restored")];recovery.Value=new("Incoming unfinished");
  vm.ReloadAfterRestoreAsync().GetAwaiter().GetResult();
  Check(vm.ActiveDraft is null&&vm.ActiveCharacterName=="No character selected","restore clears old active identity");
  Check(vm.Drafts.Count==1&&vm.Drafts[0].Name=="Restored","restore reloads replacement library");
  Check(vm.DraftNameInput=="Incoming unfinished"&&recovery.Writes==1,"restore loads incoming recovery without overwriting it");
  recovery.Value=null;awaitless(vm.ReloadAfterRestoreAsync());
  Check(vm.DraftNameInput==""&&vm.CanEditDraftName&&recovery.Writes==1,"absent incoming recovery clears old input without persistence write");
  PngRoundTrip();
  var window=new BackupWindow(new Backups(),null);
  var restore=(Button)window.FindName("Restore");var cancel=(Button)window.FindName("Cancel");
  Check(!restore.IsEnabled&&!restore.IsDefault&&cancel.IsCancel,"restore initially disabled and never default; Escape has cancel target");
  var content=(FrameworkElement)window.Content;content.Measure(new Size(800,640));content.Arrange(new Rect(0,0,800,640));content.UpdateLayout();
  var bitmap=new RenderTargetBitmap(800,680,96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()){context.DrawRectangle((Brush)window.FindResource("Brush.Surface"),null,new Rect(0,0,800,680));}bitmap.Render(drawing);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var image=File.Create("artifacts/task25-test/initial.png"))encoder.Save(image);
  Check(window.Width==800&&window.Height==680&&window.ResizeMode==ResizeMode.NoResize,"backup supported window dimensions");
  typeof(BackupWindow).GetMethod("SetBusy",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[true]);
  bool closed=false;window.Closed+=(_,_)=>closed=true;window.Close();
  Check(!closed&&!cancel.IsEnabled&&!((Button)window.FindName("Create")).IsEnabled,"busy operation blocks native close and conflicting controls");
  typeof(BackupWindow).GetMethod("SetBusy",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(window,[false]);window.Close();
  Check(closed,"idle cancel closes dialog");
  Console.WriteLine($"{checks} checks passed; STA object checks only, no rendered interaction acceptance.");app.Shutdown();
 }
 static void PngRoundTrip(){var path=Path.Combine(Path.GetTempPath(),"charsheet-wpf-png-"+Guid.NewGuid().ToString("N"));try{var data=Path.Combine(path,"data");var db=new CharacterDatabase(Path.Combine(data,"characters.db"));db.Initialize();var id=CharacterId.New();awaitless(new SqliteCharacterLibrary(db).CreateDraftAsync(id,"WPF portrait"));var pixels=new byte[]{0,0,255,255};var source=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,pixels,4);var png=(byte[])typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.PortraitCodec")!.GetMethod("Normalize")!.Invoke(null,[source,false])!;awaitless(new ManagedCharacterPortraits(db,Path.Combine(data,"portraits")).SaveAsync(id,null,png));var service=new FileBackupService(data);var archive=Path.Combine(path,"backup.zip");awaitless(service.CreateAsync(archive));using var preview=service.PreviewAsync(archive).GetAwaiter().GetResult();awaitless(service.RestoreAsync(preview));Check(preview.PortraitCount==1,"real WPF normalized PNG passes backup validation and restore");}finally{if(Directory.Exists(path))Directory.Delete(path,true);}}
 static void awaitless(Task task)=>task.GetAwaiter().GetResult();
}
sealed class Library:ICharacterLibrary
{
 public List<CharacterDraft> Items=[new(CharacterId.New(),new(0),"Previous")];
 public Task<IReadOnlyList<CharacterDraft>> ListAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<CharacterDraft>>(Items.ToArray());
 public Task<CharacterDraft> CreateDraftAsync(CharacterId id,string name,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
}
sealed class Recovery:IDraftRecovery
{
 public DraftRecovery? Value;public int Writes;
 public Task<DraftRecovery?> LoadAsync()=>Task.FromResult(Value);
 public Task SaveAsync(DraftRecovery? value){Writes++;Value=value;return Task.CompletedTask;}
}
sealed class Backups:IBackupService
{
 public Task CreateAsync(string path)=>Task.CompletedTask;
 public Task<BackupPreview> PreviewAsync(string path)=>throw new NotSupportedException();
 public Task<string> RestoreAsync(BackupPreview preview)=>throw new NotSupportedException();
}



