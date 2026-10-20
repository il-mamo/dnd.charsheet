using System.IO;
using DnD.CharSheet.Application;
using DnD.CharSheet.Desktop;
using DnD.CharSheet.Domain;

static class Program
{
 static int checks;
 static void Check(bool value,string label) { if(!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
 static void Layout()
 {
  Exception? failure=null;
  var thread=new Thread(()=>{try {
   var app=new System.Windows.Application();
   app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary{Source=new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml",UriKind.Relative)});
   typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.Presentation.Design.Theme")!.GetMethod("Apply")!.Invoke(null,[app.Resources]);
   var longDraft=new CharacterDraft(CharacterId.New(),new CharacterRevision(0),new string('W',80));
   var window=new MainWindow(()=>Task.CompletedTask,new FakeLibrary([longDraft]));
   var vm=(ShellViewModel)window.DataContext; vm.Drafts.Add(longDraft);vm.ActivateDraftCommand.Execute(longDraft.Id);
   foreach(var size in new[]{new System.Windows.Size(600,360),new System.Windows.Size(1280,720),new System.Windows.Size(1920,1080)}) {
    window.Width=size.Width;window.Height=size.Height;var content=(System.Windows.FrameworkElement)window.Content;content.Measure(size);content.Arrange(new System.Windows.Rect(size));content.UpdateLayout();
    Check(content.IsMeasureValid && content.IsArrangeValid,"STA real-theme long-name construction/layout "+size);
   }
   window.Close();app.Shutdown();
  }catch(Exception ex){failure=ex;}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(20)))throw new Exception("STA layout timed out");if(failure is not null)throw failure;
 }
 static async Task Main()
 {
  var a = new CharacterDraft(CharacterId.New(),new CharacterRevision(0),"Same name");
  var b = new CharacterDraft(CharacterId.New(),new CharacterRevision(0),"Same name");
  var fake = new FakeLibrary([a,b]);
  var vm = new ShellViewModel(()=>Task.CompletedTask,fake);
  Check(vm.ActiveDraft is null,"initial state unselected");
  await vm.InitializeCommand.ExecuteAsync(null);
  Check(vm.ActiveDraft is null && vm.Drafts.Count==2,"loading persisted drafts does not select");
  vm.DraftNameInput="Keep me";
  vm.ActivateDraftCommand.Execute(a.Id);
  Check(ReferenceEquals(vm.ActiveDraft,a) && vm.ActiveCharacterName.Contains(a.Name) && vm.ActiveCharacterId.Contains(a.Id.ToString()) && vm.ActiveCharacterStatus=="Incomplete draft","A canonical identity fields agree");
  vm.ActivateDraftCommand.Execute(b.Id);
  Check(ReferenceEquals(vm.ActiveDraft,b) && vm.ActiveCharacterId.Contains(b.Id.ToString()),"duplicate names resolve by B ID");
  for(int i=0;i<1000;i++){vm.ActivateDraftCommand.Execute(a.Id);vm.ActivateDraftCommand.Execute(b.Id);vm.ActivateDraftCommand.Execute(a.Id);}
  Check(ReferenceEquals(vm.ActiveDraft,a) && fake.Creates==0 && fake.Lists==1,"rapid A/B/A leaves A without storage calls");
  vm.ActivateDraftCommand.Execute(CharacterId.New()); vm.ActivateDraftCommand.Execute(null);
  Check(ReferenceEquals(vm.ActiveDraft,a),"unknown and null targets preserve selection");
  Check(vm.DraftNameInput=="Keep me" && vm.CreationStatus=="","switch preserves input and never claims saved");
  var notifications=new List<string?>();
  vm.PropertyChanged+=(_,e)=> {notifications.Add(e.PropertyName); if(e.PropertyName==nameof(vm.ActiveDraft)) Check(vm.ActiveCharacterId.Contains(vm.ActiveDraft!.Id.ToString()) && vm.ActiveCharacterName.Contains(vm.ActiveDraft.Name),"active notification observes coherent fields");};
  vm.ActivateDraftCommand.Execute(b.Id);
  Check(new[]{"ActiveDraft","ActiveCharacterName","ActiveCharacterId","ActiveCharacterStatus","ActiveCharacterAnnouncement"}.All(notifications.Contains),"dependent identity bindings notified");
  Check(vm.ActiveCharacterAnnouncement.Contains(b.Id.ToString()) && vm.ActiveCharacterAnnouncement.Contains(b.Name) && vm.ActiveCharacterAnnouncement.Contains("Incomplete draft"),"announcement distinguishes duplicate name by B ID/status"); fake.Pending = new(); vm.DraftNameInput="Create C";
  var create=vm.CreateDraftCommand.ExecuteAsync(null);
  var captured=fake.Requests.Single();
  vm.ActivateDraftCommand.Execute(a.Id); vm.ActivateDraftCommand.Execute(b.Id);
  Check(vm.IsCreating && fake.Creates==1 && captured.Name=="Create C" && captured.Id!=a.Id && captured.Id!=b.Id,"pending creation captured independent identity/name");
  var c=new CharacterDraft(captured.Id,new CharacterRevision(0),captured.Name);
  fake.Pending.SetResult(c);await create;
  Check(ReferenceEquals(vm.ActiveDraft,b) && vm.Drafts.Contains(c) && fake.Creates==1,"pending success adds C and retains B");
  fake.Pending=new();vm.DraftNameInput="Failed D";create=vm.CreateDraftCommand.ExecuteAsync(null);
  vm.ActivateDraftCommand.Execute(a.Id); fake.Pending.SetException(new IOException());await create;
  Check(ReferenceEquals(vm.ActiveDraft,a) && vm.DraftNameInput=="Failed D" && vm.HasCreationError && vm.Drafts.Count==3,"pending failure preserves A/input and adds no row");
  Check(a.Name=="Same name" && b.Name=="Same name" && fake.Requests.All(r=>r.Id!=a.Id && r.Id!=b.Id),"creation never targets saved A/B");
  var restarted=new ShellViewModel(()=>Task.CompletedTask,fake);await restarted.InitializeCommand.ExecuteAsync(null);
  Check(restarted.ActiveDraft is null && restarted.DraftNameInput=="" && restarted.Drafts.SequenceEqual(new[]{a,b}),"fresh session loads saved library without active or input state");
  Layout(); Console.WriteLine($"{checks} checks passed");
 }
}
sealed class FakeLibrary(IReadOnlyList<CharacterDraft> drafts):ICharacterLibrary
{
 public int Lists,Creates;
 public List<(CharacterId Id,string Name)> Requests=[];
 public TaskCompletionSource<CharacterDraft>? Pending;
 public Task<IReadOnlyList<CharacterDraft>> ListAsync(CancellationToken cancellationToken=default){Lists++;return Task.FromResult(drafts);}
 public Task<CharacterDraft> CreateDraftAsync(CharacterId id,string name,CancellationToken cancellationToken=default){Creates++;Requests.Add((id,name));return Pending!.Task;}
}



