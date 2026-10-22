using System.IO;
using DnD.CharSheet.Application;
using DnD.CharSheet.Desktop;
using DnD.CharSheet.Domain;

static class Program
{
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static async Task Main()
 {
  var r=new Recovery();var l=new Library();var vm=new ShellViewModel(()=>Task.CompletedTask,l,r);await vm.InitializeAsync();
  Check(vm.CanEditDraftName&&vm.ActiveDraft is null,"empty startup editable and unselected");
  r.Pending=new();vm.DraftNameInput="First";vm.DraftNameInput="Last";
  Check(r.Writes.Count==1,"name writes serialize");var close=vm.PrepareToCloseAsync();Check(!close.IsCompleted&&!vm.CanEditDraftName,"close drains pending edit");
  r.Pending.SetResult();r.Pending=null;Check(await close&&r.Value!.Name=="Last","latest edit durable before close");
  r.Fail=true;vm.DraftNameInput="Retained";Check(vm.HasRecoveryError&&vm.DraftNameInput=="Retained"&&r.Value!.Name=="Last","failed write retains input and durable predecessor");
  Check(!await vm.PrepareToCloseAsync()&&vm.CanEditDraftName,"failed drain blocks close restores editing");
  r.Fail=false;await vm.RetryRecoveryCommand.ExecuteAsync(null);Check(!vm.HasRecoveryError&&r.Value!.Name=="Retained","retry persists latest input");
  var restart=new ShellViewModel(()=>Task.CompletedTask,l,r);await restart.InitializeAsync();Check(restart.DraftNameInput=="Retained"&&restart.ActiveDraft is null&&l.Creates==0,"restart recovers without create or activate");
  var a=new CharacterDraft(CharacterId.New(),new(0),"Other");l.Items.Add(a);await restart.InitializeAsync();restart.ActivateDraftCommand.Execute(a.Id);
  l.Ambiguous=true;await restart.CreateDraftCommand.ExecuteAsync(null);var id=r.Value!.SubmittedId;
  Check(l.Creates==1&&id is not null&&!restart.CanEditDraftName&&restart.HasCreationError,"ambiguous commit freezes stable intent");
  var recovered=new ShellViewModel(()=>Task.CompletedTask,l,r);await recovered.InitializeAsync();recovered.ActivateDraftCommand.Execute(a.Id);await recovered.CreateDraftCommand.ExecuteAsync(null);
  Check(l.Creates==1&&r.Value is null&&recovered.Drafts.Count==2&&recovered.ActiveDraft==a,"restart reconciles committed ID without duplicate or switch retarget");
  recovered.DraftNameInput="Retained";r.FailClear=true;await recovered.CreateDraftCommand.ExecuteAsync(null);
  Check(l.Creates==2&&!recovered.CanEditDraftName&&recovered.CreationStatus.Contains("Character saved"),"cleanup failure distinguishes saved character");
  r.FailClear=false;await recovered.CreateDraftCommand.ExecuteAsync(null);Check(l.Creates==2&&r.Value is null&&recovered.CanEditDraftName,"cleanup retry does not duplicate");
  recovered.DraftNameInput="Intent failure";r.Fail=true;await recovered.CreateDraftCommand.ExecuteAsync(null);
  Check(l.Creates==2&&recovered.HasRecoveryError&&!recovered.CanEditDraftName,"failed durable intent prevents create");
  r.Fail=false;await recovered.RetryRecoveryCommand.ExecuteAsync(null);Check(l.Creates==3&&r.Value is null,"recovery Retry resolves frozen intent");
  r.LoadFail=true;var blocked=new ShellViewModel(()=>Task.CompletedTask,l,r);await blocked.InitializeAsync();Check(!blocked.CanEditDraftName&&!blocked.CanCreate&&blocked.HasRecoveryError,"load error blocks overwrite");Check(await blocked.PrepareToCloseAsync(),"untouched unreadable recovery allows close");
  r.LoadFail=false;await blocked.RetryRecoveryCommand.ExecuteAsync(null);Check(blocked.CanEditDraftName,"load retry unlocks safely");
  CloseWindow();Console.WriteLine($"{checks} checks passed");
 }
 static void CloseWindow()
 {
  Exception? error=null;var thread=new Thread(()=>{try{
   var app=new System.Windows.Application();app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary{Source=new Uri("/DnD.CharSheet.Desktop;component/Presentation/Design/Tokens.xaml",UriKind.Relative)});
   typeof(MainWindow).Assembly.GetType("DnD.CharSheet.Desktop.Presentation.Design.Theme")!.GetMethod("Apply")!.Invoke(null,[app.Resources]);
   var w=new MainWindow(()=>Task.CompletedTask,new Library(),recovery:new Recovery());bool closed=false;w.Closed+=(_,_)=>closed=true;w.Close();
   var frame=new System.Windows.Threading.DispatcherFrame();System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);
   Check(closed,"STA synchronous safe close completes outside Closing handler");app.Shutdown();
  }catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(20)))throw new Exception("STA close timeout");if(error is not null)throw error;
 }
}
sealed class Recovery:IDraftRecovery
{
 public DraftRecovery? Value;public bool Fail,FailClear,LoadFail;public TaskCompletionSource? Pending;public List<DraftRecovery?> Writes=[];
 public Task<DraftRecovery?> LoadAsync()=>LoadFail?Task.FromException<DraftRecovery?>(new IOException()):Task.FromResult(Value);
 public async Task SaveAsync(DraftRecovery? value){Writes.Add(value);if(Pending is {} pending)await pending.Task;if(Fail||(value is null&&FailClear))throw new IOException();Value=value;}
}
sealed class Library:ICharacterLibrary
{
 public List<CharacterDraft> Items=[];public int Creates;public bool Ambiguous;
 public Task<IReadOnlyList<CharacterDraft>> ListAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<CharacterDraft>>(Items.ToArray());
 public Task<CharacterDraft> CreateDraftAsync(CharacterId id,string name,CancellationToken cancellationToken=default){Creates++;var d=new CharacterDraft(id,new(0),DraftName.Normalize(name));Items.Add(d);if(Ambiguous){Ambiguous=false;throw new IOException();}return Task.FromResult(d);}
}

