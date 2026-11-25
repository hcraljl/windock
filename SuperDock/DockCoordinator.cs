using System.Windows;
using System.Windows.Threading;
namespace SuperDock;

internal sealed class DockCoordinator : IDisposable
{
    readonly MainWindow.Settings settings;
    readonly Func<List<(string Id,Rect Area)>> monitors;
    readonly List<MainWindow> docks=[];
    readonly HashSet<MainWindow> changed=[];
    readonly Dispatcher dispatcher;
    readonly DispatcherTimer displayTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    string displaySignature="";
    bool pending,stopping,manuallyHidden;
    public MainWindow Primary {get;}
    public IReadOnlyList<MainWindow> Docks=>docks;
    public DockCoordinator(MainWindow.Settings settings,Func<List<(string Id,Rect Area)>>? monitors=null)
    {
        this.settings=settings;this.monitors=monitors??DesktopIntegration.Monitors;dispatcher=Application.Current.Dispatcher;
        Primary=new MainWindow(settings,true){Coordinator=this};docks.Add(Primary);
        Primary.Closed+=(_,_)=>{if(!stopping)Exit();};
        displayTimer.Tick+=(_,_)=>{var signature=string.Join(";",this.monitors().Select(m=>$"{m.Id}:{m.Area}"));if(signature!=displaySignature)Synchronize();};
    }
    public static List<(string Id,Rect Area)> Select(MainWindow.Settings settings,List<(string Id,Rect Area)> available)
    {
        if(settings.ShowOnAllMonitors)return available;
        var ids=settings.MonitorIds.Count>0?settings.MonitorIds:(string.IsNullOrEmpty(settings.MonitorId)?[]:new List<string>{settings.MonitorId});
        var selected=available.Where(m=>ids.Contains(m.Id,StringComparer.OrdinalIgnoreCase)).ToList();
        return selected.Count>0?selected:available.Take(1).ToList();
    }
    public void Start(){Synchronize();displayTimer.Start();}
    public void SettingsChanged(MainWindow source)
    {
        if(stopping)return;changed.Add(source);if(pending)return;pending=true;
        dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{pending=false;if(!stopping)Synchronize();}));
    }
    internal void Synchronize()
    {
        if(stopping)return;var available=monitors();displaySignature=string.Join(";",available.Select(m=>$"{m.Id}:{m.Area}"));
        var selected=Select(settings,available);
        if(selected.Count==0)selected.Add(("",SystemParameters.WorkArea));
        Primary.AssignedMonitor=selected[0];
        var wanted=selected.Skip(1).Select(m=>m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var dock in docks.Skip(1).Where(d=>!wanted.Contains(d.AssignedMonitor?.Id??"")).ToArray()){docks.Remove(dock);dock.Close();}
        foreach(var monitor in selected.Skip(1)){
            var dock=docks.Skip(1).FirstOrDefault(d=>d.AssignedMonitor?.Id==monitor.Id);
            if(dock==null){dock=new MainWindow(settings,false){Coordinator=this,AssignedMonitor=monitor};docks.Add(dock);}
            else dock.AssignedMonitor=monitor;
        }
        foreach(var dock in docks){if(!changed.Contains(dock))dock.RefreshDock();if(!dock.IsLoaded)dock.Show();dock.SetManualHidden(manuallyHidden);}
        changed.Clear();
    }
    public void Reveal(){manuallyHidden=false;foreach(var dock in docks)dock.SetManualHidden(false);}
    public void Toggle(){manuallyHidden=!manuallyHidden&&!docks.Any(d=>d.IsLayerLowered);foreach(var dock in docks)dock.SetManualHidden(manuallyHidden);}
    public void OpenSettings(MainWindow source)
    {
        Primary.OpenSettingsFor(source);
    }
    public void Exit(){Dispose();Application.Current.Shutdown();}
    public void Dispose(){if(stopping)return;stopping=true;displayTimer.Stop();foreach(var dock in docks.ToArray())dock.Close();docks.Clear();}
}
