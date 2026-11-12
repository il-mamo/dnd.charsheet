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
 static readonly string Root=Path.Combine(Path.GetTempPath(),"charsheet-27-independent-"+Guid.NewGuid().ToString("N"));
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static void Run(Task task)=>task.GetAwaiter().GetResult();
 static T Get<T>(Task<T> task)=>task.GetAwaiter().GetResult();
 [STAThread] static void Main()
 {
  Directory.CreateDirectory(Root);
  try
  {
   var app=new System.Windows.Application();
   app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml",UriKind.Relative)});
   typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.Presentation.Design.Theme")!.GetMethod("Apply")!.Invoke(null,[app.Resources]);
   var db=new CharacterDatabase(Path.Combine(Root,"characters.db"));db.Initialize();
   var library=new SqliteCharacterLibrary(db);var adventures=new SqliteAdventureRepository(db);var disk=new FileDraftRecovery(Path.Combine(Root,"draft-recovery.json"));
   var a=AdventureId.New();var b=AdventureId.New();var aid=CharacterId.New();var bid=CharacterId.New();var orphan=CharacterId.New();
   Run(adventures.CreateWithDraftAsync(a,"Alpha",aid,"Alpha hero"));Run(library.CreateDraftAsync(orphan,"Retained orphan"));
   Run(disk.SaveAllAsync([new("Legacy retained"),new("Alpha recovered",null,a)]));
   var vm=new ShellViewModel(()=>Task.CompletedTask,library,disk,adventures);Run(vm.InitializeAsync());
   Check(vm.Adventures.Count==1&&vm.SelectedAdventure is null&&vm.ActiveDraft is null&&vm.Drafts.Count==0&&!vm.CanCreate,"one Adventure does not auto-select or expose roster");
   Check(vm.RetainedRecoveryText.Contains("Legacy retained")&&vm.DraftNameInput=="","legacy recovery remains unbound and separate from session input");
   Run(vm.ActivateDraftCommand.ExecuteAsync(orphan));Check(vm.ActiveDraft is null,"unassigned activation denied before selection");
   vm.ManagementSelection=vm.Adventures.Single();Check(vm.SelectedAdventure is null&&vm.PreviewMembers.Single().Id==aid,"management selection previews without opening session");
   Run(vm.OpenAdventureCommand.ExecuteAsync(vm.ManagementSelection));
   Check(vm.SelectedAdventure?.Id==a&&vm.Drafts.Count==1&&vm.Drafts[0].Id==aid&&vm.DraftNameInput=="Alpha recovered","explicit open scopes roster and matching recovery");
   Run(vm.ActivateDraftCommand.ExecuteAsync(orphan));Check(vm.ActiveDraft is null,"unassigned cannot activate through selected Adventure");
   Run(vm.ActivateDraftCommand.ExecuteAsync(aid));Check(vm.ActiveDraft?.Id==aid,"selected member activation permitted");
   vm.DraftNameInput="Alpha pending";Run(vm.ReturnToSelectionCommand.ExecuteAsync(null));
   Check(vm.SelectedAdventure is null&&vm.ActiveDraft is null&&vm.Drafts.Count==0,"return to selection clears active context and roster");
   Run(adventures.CreateWithDraftAsync(b,"Beta",bid,"Beta hero"));Run(vm.RefreshAdventuresCommand.ExecuteAsync(null));
   Run(vm.OpenAdventureCommand.ExecuteAsync(vm.Adventures.Single(x=>x.Id==b)));
   Check(vm.Drafts.Single().Id==bid&&vm.DraftNameInput=="","Beta roster and input exclude Alpha");
   vm.DraftNameInput="Beta pending";Run(vm.ReturnToSelectionCommand.ExecuteAsync(null));Run(vm.OpenAdventureCommand.ExecuteAsync(vm.Adventures.Single(x=>x.Id==a)));
   Check(vm.DraftNameInput=="Alpha pending","Adventure switching retains original Alpha pending input");
   var restart=new ShellViewModel(()=>Task.CompletedTask,library,disk,adventures);Run(restart.InitializeAsync());
   Check(restart.SelectedAdventure is null&&restart.DraftNameInput=="","restart requires explicit Adventure selection before recovery");
   Run(restart.OpenAdventureCommand.ExecuteAsync(restart.Adventures.Single(x=>x.Id==b)));Check(restart.DraftNameInput=="Beta pending","Beta recovery survives restart in own Adventure");
   restart.DraftNameInput="New Beta";Run(restart.CreateDraftCommand.ExecuteAsync(null));
   var created=Get(library.ListAsync()).Single(x=>x.Name=="New Beta");
   Check(restart.AllCharacterCount==Get(library.ListAsync()).Count,"backup preview count includes newly created draft before management refresh");
   Check(Get(adventures.GetMembershipAsync(created.Id)).AdventureId==b&&restart.Drafts.Any(x=>x.Id==created.Id),"draft creation atomically joins selected Adventure");
   Check(Get(disk.LoadAllAsync()).Any(x=>x.AdventureId==a&&x.Name=="Alpha pending")&&!Get(disk.LoadAllAsync()).Any(x=>x.AdventureId==b),"successful create clears own recovery only");
   Run(restart.ReturnToSelectionCommand.ExecuteAsync(null));restart.ManagementSelection=restart.Adventures.Single(x=>x.Id==a);restart.SelectedUnassigned=restart.UnassignedDrafts.Single(x=>x.Id==orphan);
   restart.RequestAssignCharacterCommand.Execute(null);Check(restart.HasAdventureConfirmation&&restart.ConfirmationText.Contains("Retained orphan")&&restart.ConfirmationText.Contains("Alpha"),"assignment previews named character and destination");
   restart.CancelAdventureActionCommand.Execute(null);Check(Get(adventures.GetMembershipAsync(orphan)).AdventureId is null,"assignment cancellation leaves membership unchanged");
   restart.RequestAssignCharacterCommand.Execute(null);Run(restart.ConfirmAdventureActionCommand.ExecuteAsync(null));
   Check(Get(adventures.GetMembershipAsync(orphan)).AdventureId==a&&!restart.UnassignedDrafts.Any(x=>x.Id==orphan),"confirmed assignment changes membership and removes unassigned row");
   Check(restart.PreviewMembers.Any(x=>x.Id==orphan),"assignment refreshes selected Adventure member preview");
   restart.ManagementSelection=restart.Adventures.Single(x=>x.Id==a);restart.AdventureNameInput="Alpha renamed";Run(restart.RenameAdventureCommand.ExecuteAsync(null));
   Check(Get(adventures.ListAsync()).Single(x=>x.Id==a).Name=="Alpha renamed"&&Get(adventures.GetMembershipAsync(aid)).AdventureId==a,"rename preserves membership");
   restart.ManagementSelection=restart.Adventures.Single(x=>x.Id==a);restart.RequestDeleteAdventureCommand.Execute(null);
   Check(restart.ConfirmationText.Contains("2")&&restart.ConfirmationText.Contains("unassigned"),"delete previews member count and retention consequence");
   restart.CancelAdventureActionCommand.Execute(null);Check(Get(adventures.ListAsync()).Any(x=>x.Id==a),"delete cancellation preserves Adventure");
   restart.RequestDeleteAdventureCommand.Execute(null);Run(restart.ConfirmAdventureActionCommand.ExecuteAsync(null));
   Check(!Get(adventures.ListAsync()).Any(x=>x.Id==a)&&Get(adventures.GetMembershipAsync(aid)).AdventureId is null&&Get(library.ListAsync()).Any(x=>x.Id==aid),"delete retains characters as unassigned");
   Check(Get(disk.LoadAllAsync()).Any(x=>x.AdventureId==a)&&restart.RetainedRecoveryText.Contains("Alpha pending"),"deleted Adventure recovery remains retained without reassignment");
   Run(restart.OpenAdventureCommand.ExecuteAsync(restart.Adventures.Single(x=>x.Id==b)));Run(restart.ActivateDraftCommand.ExecuteAsync(bid));Run(restart.ReloadAfterRestoreAsync());
   Check(restart.SelectedAdventure is null&&restart.ActiveDraft is null&&restart.Drafts.Count==0,"restore reload resets Adventure and active character selection");
   FaultCases(library,adventures,disk,b);
   ManagementCases(library,adventures,disk);
   RenderLayout(library,adventures,disk,b);
   Console.WriteLine($"{checks} checks passed. Native interaction/accessibility not covered.");app.Shutdown();
  }
  finally {Directory.Delete(Root,true);}
 }
 static void ManagementCases(ICharacterLibrary library,IAdventureRepository adventures,IDraftRecovery disk)
 {
  var fault=new FaultAdventures(adventures);var vm=new ShellViewModel(()=>Task.CompletedTask,library,disk,fault);Run(vm.InitializeAsync());
  var count=Get(adventures.ListAsync()).Count;vm.NewAdventureNameInput="";vm.FirstCharacterName="First";Run(vm.CreateAdventureCommand.ExecuteAsync(null));
  Check(Get(adventures.ListAsync()).Count==count,"invalid first-member form makes no Adventure");
  vm.NewAdventureNameInput="Ambiguous create";fault.ThrowAfterCreate=true;Run(vm.CreateAdventureCommand.ExecuteAsync(null));
  Check(Get(adventures.ListAsync()).Count(x=>x.Name=="Ambiguous create")==1,"post-commit ambiguous management create saves one Adventure");
  Run(vm.CreateAdventureCommand.ExecuteAsync(null));
  Check(Get(adventures.ListAsync()).Count(x=>x.Name=="Ambiguous create")==1&&vm.ManagementSelection?.Name=="Ambiguous create"&&vm.SelectedAdventure is null,"management retry reconciles stable IDs without duplicate or implicit opening");
  var orphan=Get(library.CreateDraftAsync(CharacterId.New(),"Existing first"));Run(vm.RefreshAdventuresCommand.ExecuteAsync(null));
  vm.NewAdventureNameInput="Existing-member create";vm.SelectedUnassigned=vm.UnassignedDrafts.Single(x=>x.Id==orphan.Id);Run(vm.CreateAdventureCommand.ExecuteAsync(null));
  var target=Get(adventures.ListAsync()).Single(x=>x.Name=="Existing-member create");
  Check(Get(adventures.GetMembershipAsync(orphan.Id)).AdventureId==target.Id&&Get(library.ListAsync()).Count(x=>x.Id==orphan.Id)==1,"create with existing unassigned first member preserves character identity");
  var stale=Get(library.CreateDraftAsync(CharacterId.New(),"Stale assignment"));Run(vm.RefreshAdventuresCommand.ExecuteAsync(null));vm.ManagementSelection=vm.Adventures.Single(x=>x.Id==target.Id);vm.SelectedUnassigned=vm.UnassignedDrafts.Single(x=>x.Id==stale.Id);vm.RequestAssignCharacterCommand.Execute(null);
  var competingDestination=vm.Adventures.First(x=>x.Id!=target.Id).Id;Run(adventures.AssignAsync(stale.Id,competingDestination));Run(vm.ConfirmAdventureActionCommand.ExecuteAsync(null));
  Check(Get(adventures.GetMembershipAsync(stale.Id)).AdventureId==competingDestination&&vm.HasAdventureConfirmation,"stale assignment confirmation cannot overwrite competing membership");vm.CancelAdventureActionCommand.Execute(null);
  var pending=new DelayedRecovery(disk);var concurrent=new ShellViewModel(()=>Task.CompletedTask,library,pending,adventures);Run(concurrent.InitializeAsync());Run(concurrent.OpenAdventureCommand.ExecuteAsync(concurrent.Adventures.First()));
  var context=concurrent.SelectedAdventure!.Id;pending.Delay=true;concurrent.DraftNameInput="Delayed pending";
  var returning=concurrent.ReturnToSelectionCommand.ExecuteAsync(null);
  Check(concurrent.IsAdventureBusy&&!concurrent.CanEditDraftName,"navigation owns context while recovery write drains");
  var competing=concurrent.OpenAdventureCommand.ExecuteAsync(concurrent.Adventures.First(x=>x.Id!=context));Run(competing);
  Check(concurrent.SelectedAdventure?.Id==context,"competing transition cannot change context during recovery drain");
  pending.Release();Run(returning);Check(concurrent.SelectedAdventure is null&&Get(disk.LoadAllAsync()).Any(x=>x.AdventureId==context&&x.Name=="Delayed pending"),"drained navigation retains captured original Adventure input");
 }
 static void RenderLayout(ICharacterLibrary library,IAdventureRepository adventures,IDraftRecovery disk,AdventureId b)
 {
  Run(adventures.RenameAsync(b,new string('W',80)));Run(library.CreateDraftInAdventureAsync(CharacterId.New(),new string('M',80),b));
  for(int i=0;i<12;i++){Run(adventures.CreateWithDraftAsync(AdventureId.New(),$"Scrolling Adventure {i:00}",CharacterId.New(),$"Member {i:00}"));Run(library.CreateDraftAsync(CharacterId.New(),$"Unassigned scroll {i:00}"));}
  var vm=new ShellViewModel(()=>Task.CompletedTask,library,disk,adventures);Run(vm.InitializeAsync());
  var libraryModel=new ShellViewModel(()=>Task.CompletedTask,library,disk,adventures);Run(libraryModel.InitializeAsync());Run(libraryModel.OpenAdventureCommand.ExecuteAsync(libraryModel.Adventures.Single(x=>x.Id==b)));
  var window=new MainWindow(()=>Task.CompletedTask,library,recovery:disk,adventures:adventures){DataContext=vm};
  var content=(FrameworkElement)window.Content;content.Measure(new Size(1920,1080));content.Arrange(new Rect(0,0,1920,1080));content.UpdateLayout();Pump();content.UpdateLayout();
  var panel=Find<AdventurePanel>(content) ?? throw new Exception("missing AdventurePanel");Check(panel.ActualWidth<=1872&&panel.ActualHeight>600,"actual WPF selection panels fit supported display");
  var search=(TextBox)panel.FindName("AdventureSearch");Check(search.ActualHeight>=40,"Adventure search meets shared input height");
  Check(((Label)FindLabel(panel,"Search Adventures")!).Foreground==System.Windows.Application.Current.FindResource("Brush.Text"),"Adventure labels use accessible theme foreground");
  Check(Nodes(panel).OfType<Label>().All(label=>label.Foreground==System.Windows.Application.Current.FindResource("Brush.Text")),"all Adventure panel field labels use theme text color");
  Check(Nodes(panel).OfType<Button>().Where(button=>new[]{"Open Adventure","Rename","Delete Adventure"+(char)0x2026,"Assign Adventure"+(char)0x2026}.Contains(button.Content?.ToString())).All(button=>!button.IsEnabled),"missing-selection actions are disabled");
  SaveRender(content,"selection.png");
  vm.ManagementSelection=vm.Adventures.Single(x=>x.Id==b);vm.RequestDeleteAdventureCommand.Execute(null);content.UpdateLayout();Pump();content.UpdateLayout();
  var cancel=(Button)panel.FindName("CancelConfirmation");Check(cancel.Visibility==Visibility.Visible&&!cancel.IsDefault,"delete confirmation has explicit safe cancel and no default submit");SaveRender(content,"confirmation.png");vm.CancelAdventureActionCommand.Execute(null);
  vm.NewAdventureNameInput="New Adventure preview";vm.FirstCharacterName="First new draft";vm.SelectedUnassigned=null;
  Nodes(panel).OfType<Button>().Single(button=>button.Content?.ToString()?.StartsWith("Create Adventure")==true&&button.Content.ToString()!="Create Adventure").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));content.UpdateLayout();Pump();
  var form=(FrameworkElement)panel.FindName("CreateForm");form.BringIntoView();content.UpdateLayout();Pump();content.UpdateLayout();
  var create=Nodes(form).OfType<Button>().Single(button=>Equals(button.Content,"Create Adventure"));
  Check(form.Visibility==Visibility.Visible&&((TextBox)panel.FindName("FirstCharacterName")).IsEnabled&&create.ActualHeight>=40,"expanded create form exposes new draft input and explicit submit");SaveRender(content,"create.png");
  vm.SelectedUnassigned=vm.UnassignedDrafts.First();content.UpdateLayout();form.BringIntoView();Pump();content.UpdateLayout();
  Check(!((TextBox)panel.FindName("FirstCharacterName")).IsEnabled,"existing unassigned first-member choice disables alternative new draft field");SaveRender(content,"create-existing.png");
  Nodes(form).OfType<Button>().Single(button=>Equals(button.Content,"Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(form.Visibility==Visibility.Collapsed&&vm.NewAdventureNameInput=="New Adventure preview"&&vm.FirstCharacterName=="First new draft","cancel create closes form while retaining unsent input");
  window.DataContext=libraryModel;content.UpdateLayout();Pump();content.UpdateLayout();
  Check(!libraryModel.ShowAdventurePanel&&((FrameworkElement)window.FindName("SavedCharacters")).ActualWidth>900,"library mode hides management and preserves scoped card area");SaveRender(content,"library.png");
 }
 static void Pump(){var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);}
 static IEnumerable<DependencyObject> Nodes(DependencyObject node){yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Nodes(VisualTreeHelper.GetChild(node,i)))yield return child;}
 static Label? FindLabel(DependencyObject node,string content){if(node is Label found&&Equals(found.Content,content))return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(FindLabel(VisualTreeHelper.GetChild(node,i),content) is Label child)return child;return null;}
 static T? Find<T>(DependencyObject node) where T:DependencyObject {if(node is T found)return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(Find<T>(VisualTreeHelper.GetChild(node,i)) is T child)return child;return null;}
 static void SaveRender(FrameworkElement content,string filename){var bmp=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle((Brush)System.Windows.Application.Current.FindResource("Brush.Canvas"),null,new Rect(0,0,1920,1080));bmp.Render(drawing);bmp.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(Path.Combine("artifacts/task27-test",filename));png.Save(file);}
 static void FaultCases(ICharacterLibrary library,IAdventureRepository adventures,IDraftRecovery disk,AdventureId b)
 {
  var fault=new FaultRecovery(disk);var vm=new ShellViewModel(()=>Task.CompletedTask,library,fault,adventures);Run(vm.InitializeAsync());Run(vm.OpenAdventureCommand.ExecuteAsync(vm.Adventures.Single(x=>x.Id==b)));
  fault.FailWrites=true;vm.DraftNameInput="Failed recovery";Run(vm.ReturnToSelectionCommand.ExecuteAsync(null));
  Check(vm.HasRecoveryError&&vm.SelectedAdventure?.Id==b&&vm.DraftNameInput=="Failed recovery","failed recovery blocks navigation and preserves original input");
  fault.FailWrites=false;Run(vm.RetryRecoveryCommand.ExecuteAsync(null));Run(vm.ReturnToSelectionCommand.ExecuteAsync(null));Check(vm.SelectedAdventure is null,"successful recovery retry permits navigation");
  var cleanup=new FaultRecovery(disk);var retry=new ShellViewModel(()=>Task.CompletedTask,library,cleanup,adventures);Run(retry.InitializeAsync());Run(retry.OpenAdventureCommand.ExecuteAsync(retry.Adventures.Single(x=>x.Id==b)));
  retry.DraftNameInput="Cleanup retry";cleanup.FailCleanup=true;Run(retry.CreateDraftCommand.ExecuteAsync(null));var before=Get(library.ListAsync()).Count(x=>x.Name=="Cleanup retry");
  Check(before==1&&retry.HasCreationError,"post-commit cleanup failure is visible with one saved draft");
  cleanup.FailCleanup=false;Run(retry.CreateDraftCommand.ExecuteAsync(null));Check(Get(library.ListAsync()).Count(x=>x.Name=="Cleanup retry")==1&&retry.HasCreationSucceeded,"cleanup retry reconciles saved ID without duplicate draft");
 }
}
sealed class FaultRecovery(IDraftRecovery inner):IDraftRecovery
{
 public bool FailWrites,FailCleanup;
 public Task<DraftRecovery?> LoadAsync()=>inner.LoadAsync();
 public Task SaveAsync(DraftRecovery? draft)=>inner.SaveAsync(draft);
 public Task<IReadOnlyList<DraftRecovery>> LoadAllAsync()=>inner.LoadAllAsync();
 public Task SaveAllAsync(IReadOnlyList<DraftRecovery> drafts)=>FailWrites||(FailCleanup&&!drafts.Any(x=>x.Name=="Cleanup retry"))?Task.FromException(new IOException("injected recovery failure")):inner.SaveAllAsync(drafts);
}


sealed class FaultAdventures(IAdventureRepository inner):IAdventureRepository
{
 public bool ThrowAfterCreate;
 public Task<IReadOnlyList<Adventure>> ListAsync(CancellationToken token=default)=>inner.ListAsync(token);
 public Task<CharacterMembership> GetMembershipAsync(CharacterId id,CancellationToken token=default)=>inner.GetMembershipAsync(id,token);
 public async Task CreateWithDraftAsync(AdventureId id,string name,CharacterId first,string characterName,CancellationToken token=default){await inner.CreateWithDraftAsync(id,name,first,characterName,token);if(ThrowAfterCreate){ThrowAfterCreate=false;throw new IOException("injected lost commit result");}}
 public Task CreateWithUnassignedAsync(AdventureId id,string name,CharacterId first,CancellationToken token=default)=>inner.CreateWithUnassignedAsync(id,name,first,token);
 public Task AssignAsync(CharacterId id,AdventureId destination,CancellationToken token=default)=>inner.AssignAsync(id,destination,token);
 public Task RenameAsync(AdventureId id,string name,CancellationToken token=default)=>inner.RenameAsync(id,name,token);
 public Task DeleteAsync(AdventureId id,CancellationToken token=default)=>inner.DeleteAsync(id,token);
}
sealed class DelayedRecovery(IDraftRecovery inner):IDraftRecovery
{
 public bool Delay;private readonly TaskCompletionSource completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public void Release()=>completion.TrySetResult();
 public Task<DraftRecovery?> LoadAsync()=>inner.LoadAsync();public Task SaveAsync(DraftRecovery? draft)=>inner.SaveAsync(draft);
 public Task<IReadOnlyList<DraftRecovery>> LoadAllAsync()=>inner.LoadAllAsync();
 public async Task SaveAllAsync(IReadOnlyList<DraftRecovery> drafts){var snapshot=drafts.ToArray();if(Delay)await completion.Task;await inner.SaveAllAsync(snapshot);}
}

