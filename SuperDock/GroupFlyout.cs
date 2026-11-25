using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
namespace SuperDock;
internal sealed class GroupFlyout : Popup
{
    readonly Border panel;
    readonly ScaleTransform scale=new(1,1);
    bool closing,busy;int contextMenus;
    internal bool HasContextMenu => contextMenus>0;
    readonly DispatcherTimer dismissTimer=new(){Interval=TimeSpan.FromMilliseconds(80)};
    readonly Dictionary<Border,MainWindow.Entry> indicators=[];
    readonly DispatcherTimer stateTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};
    IntPtr foreground=RunningWindows.ExternalForeground();
    public GroupFlyout(FrameworkElement anchor,MainWindow.AppGroup group,string edge,Action changed,Action removeGroupAction,Action<MainWindow.Entry,bool> removeEntry,bool hover=false)
    {
        PlacementTarget=anchor;Placement=edge switch{"Top"=>PlacementMode.Bottom,"Left"=>PlacementMode.Right,"Right"=>PlacementMode.Left,_=>PlacementMode.Top};AllowsTransparency=true;StaysOpen=hover;Focusable=true;PopupAnimation=PopupAnimation.None;
        var body=new DockPanel();var header=new DockPanel{Margin=new Thickness(4,0,4,12)};
        var more=new Button{Content="···",Style=(Style)Application.Current.FindResource("GlassButton"),Padding=new Thickness(9,3,9,3)};DockPanel.SetDock(more,Dock.Right);header.Children.Add(more);header.Children.Add(new TextBlock{Text=group.Name,Foreground=Theme.Text,FontSize=14,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});DockPanel.SetDock(header,Dock.Top);body.Children.Add(header);
        var menu=new ContextMenu();var removeGroup=new MenuItem{Header="移除分组"};removeGroup.Click+=(_,_)=>Collapse(removeGroupAction);menu.Items.Add(removeGroup);menu.Opened+=(_,_)=>contextMenus++;menu.Closed+=(_,_)=>contextMenus=Math.Max(0,contextMenus-1);more.Click+=(_,_)=>{menu.PlacementTarget=more;menu.IsOpen=true;};
        // The caller handles group deletion separately from individual entry edits.

        var grid=new UniformGrid{Columns=Math.Clamp(group.Entries.Count,2,4),VerticalAlignment=VerticalAlignment.Top};
        foreach(var app in group.Entries.ToArray())
        {
            var face=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};var icon=ShellIcons.Load(app.Target);face.Children.Add(new Image{Source=icon,Width=40,Height=40,Margin=new Thickness(0,0,0,9)});face.Children.Add(new TextBlock{Text=app.Name,Foreground=Theme.Text,FontSize=11,MaxWidth=86,TextTrimming=TextTrimming.CharacterEllipsis,HorizontalAlignment=HorizontalAlignment.Center});
            var button=new Button{Content=face,Width=100,Height=88,Background=Brushes.Transparent,Style=(Style)Application.Current.FindResource("GlassButton"),Padding=new Thickness(6),Margin=new Thickness(2),ToolTip=app.Name};
            var indicator=new Border{Width=18,Height=2,CornerRadius=new CornerRadius(1),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,0),Visibility=Visibility.Hidden};face.Children.Add(indicator);indicators[indicator]=app;
            bool held=false;Point start=default;var hold=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(550)};
            button.Click+=(_,_)=>{if(held)return;var previous=foreground;Collapse(()=>{var windows=RunningWindows.Find(app.Target);if(windows.Count>0){RunningWindows.Toggle(windows,previous);ActivityStore.Write("窗口切换",app.Name,app.Target);}else ActivityStore.Launch(app.Target,app.Name);});};
            var actions=new ContextMenu();
            void Populate()
            {
                actions.Items.Clear();var target=RunningWindows.WindowTarget(app.Target);var windows=RunningWindows.Find(target);bool running=windows.Count>0||RunningWindows.RunningTargets([target]).Contains(target);
                void Add(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>Collapse(action);actions.Items.Add(item);}
                if(!running)Add("打开应用",()=>ActivityStore.Launch(app.Target,app.Name));
                if(windows.Count>0){Add("显示窗口",()=>RunningWindows.Activate(windows[0].Handle));Add("最大化窗口",()=>RunningWindows.Maximize(windows[0].Handle));Add("最小化窗口",()=>RunningWindows.Minimize(windows[0].Handle));Add("退出程序",()=>{RunningWindows.RequestExit(app.Target);ActivityStore.Write("请求退出",app.Name,app.Target);});}
                if(running)Add("强制退出…",async()=>{if(!GlassDialog.Confirm($"将结束「{app.Name}」的所有匹配进程。\n未保存的内容可能丢失。","强制退出","强制退出",true))return;var result=await System.Threading.Tasks.Task.Run(()=>RunningWindows.ForceExit(app.Target));ActivityStore.Write("强制退出",app.Name,app.Target,$"终止 {result.Ended}，失败 {result.Failed}");if(result.Failed>0)GlassDialog.Show("部分进程无法结束，可能需要管理员权限。");});
                actions.Items.Add(new Separator());Add("移出分组，回到 Dock",()=>removeEntry(app,false));Add("彻底移除入口",()=>removeEntry(app,true));
            }
            button.ContextMenu=actions;
            button.PreviewMouseRightButtonDown+=(_,_)=>{hold.Stop();StaysOpen=true;Populate();};
            button.ContextMenuOpening+=(_,_)=>{StaysOpen=true;Populate();};
            actions.Opened+=(_,_)=>{contextMenus++;StaysOpen=true;hold.Stop();};actions.Closed+=(_,_)=>{contextMenus=Math.Max(0,contextMenus-1);if(IsOpen&&!closing)StaysOpen=hover;};
            button.PreviewMouseLeftButtonDown+=(_,e)=>{held=false;start=e.GetPosition(button);hold.Start();};
            button.PreviewMouseMove+=(_,e)=>{var point=e.GetPosition(button);if(e.LeftButton!=MouseButtonState.Pressed||Math.Abs(point.X-start.X)>SystemParameters.MinimumHorizontalDragDistance||Math.Abs(point.Y-start.Y)>SystemParameters.MinimumVerticalDragDistance)hold.Stop();};
            button.MouseLeave+=(_,_)=>hold.Stop();button.PreviewMouseLeftButtonUp+=(_,e)=>{hold.Stop();if(held)e.Handled=true;};button.Unloaded+=(_,_)=>hold.Stop();
            hold.Tick+=(_,_)=>{hold.Stop();if(Mouse.LeftButton!=MouseButtonState.Pressed||!button.IsMouseOver)return;held=true;StaysOpen=true;Populate();button.ReleaseMouseCapture();actions.PlacementTarget=button;actions.Placement=PlacementMode.MousePoint;actions.IsOpen=true;};
            grid.Children.Add(button);
        }
        if(group.Entries.Count==0)body.Children.Add(new TextBlock{Text="暂无应用",Foreground=Theme.Muted,Margin=new Thickness(20)});
        else body.Children.Add(new ScrollViewer{Content=grid,MaxHeight=Math.Max(140,Math.Min(380,SystemParameters.WorkArea.Height-180)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        panel=new Border{Background=new LinearGradientBrush(Theme.Dark?Color.FromArgb(235,49,58,74):Color.FromArgb(235,247,250,255),Theme.Dark?Color.FromArgb(220,26,35,49):Color.FromArgb(220,217,230,247),60),BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(14),MinWidth=220,Child=body,RenderTransform=scale,RenderTransformOrigin=edge switch{"Top"=>new Point(.5,0),"Left"=>new Point(0,.5),"Right"=>new Point(1,.5),_=>new Point(.5,1)},Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=20,ShadowDepth=5,Opacity=.28}};
        RoundClip.SetRadius(panel,20);Child=new Border{Padding=new Thickness(14),Child=panel};
        Opened+=(_,_)=>{var duration=TimeSpan.FromMilliseconds(240);var animation=new DoubleAnimation(.78,1,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};scale.BeginAnimation(ScaleTransform.ScaleXProperty,animation);scale.BeginAnimation(ScaleTransform.ScaleYProperty,animation);panel.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(170)));};
        stateTimer.Tick+=async(_,_)=>await RefreshStates();Opened+=async(_,_)=>{stateTimer.Start();await RefreshStates();};Closed+=(_,_)=>stateTimer.Stop();
        DateTime openedAt=DateTime.UtcNow;DateTime? outsideSince=null;bool entered=false;
        Opened+=(_,_)=>{openedAt=DateTime.UtcNow;dismissTimer.Start();};Closed+=(_,_)=>dismissTimer.Stop();
        dismissTimer.Tick+=(_,_)=>
        {
            if(!IsOpen||closing){dismissTimer.Stop();return;}if(contextMenus>0){outsideSince=null;return;}
            if(!DesktopIntegration.Cursor(out var cursor))return;
            try{var origin=panel.PointToScreen(new Point());var end=panel.PointToScreen(new Point(panel.ActualWidth,panel.ActualHeight));bool inside=cursor.X>=origin.X&&cursor.X<=end.X&&cursor.Y>=origin.Y&&cursor.Y<=end.Y;
                var anchorStart=anchor.PointToScreen(new Point());var anchorEnd=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight));bool onAnchor=cursor.X>=anchorStart.X&&cursor.X<=anchorEnd.X&&cursor.Y>=anchorStart.Y&&cursor.Y<=anchorEnd.Y;if(inside||onAnchor){entered=true;outsideSince=null;return;}if(!entered&&(DateTime.UtcNow-openedAt).TotalMilliseconds<350)return;
                outsideSince??=DateTime.UtcNow;if((DateTime.UtcNow-outsideSince.Value).TotalMilliseconds>400)Collapse();
            }catch(InvalidOperationException){dismissTimer.Stop();}
        };
        // Popup owns mouse capture, so outside clicks dismiss it like a normal menu.
        panel.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Collapse();e.Handled=true;}};
    }
    async System.Threading.Tasks.Task RefreshStates()
    {
        if(busy||!IsOpen||closing)return;busy=true;
        try
        {
            var current=RunningWindows.ExternalForeground();if(current!=IntPtr.Zero)foreground=current;
            var targets=indicators.Values.Select(e=>RunningWindows.WindowTarget(e.Target)).ToArray();var focus=foreground;
            var states=await System.Threading.Tasks.Task.Run(()=>RunningWindows.States(targets,focus));if(!IsOpen)return;
            foreach(var (line,app) in indicators){var state=states.GetValueOrDefault(RunningWindows.WindowTarget(app.Target),AppState.Stopped);line.Visibility=state==AppState.Stopped?Visibility.Hidden:Visibility.Visible;line.Background=new SolidColorBrush(state switch{AppState.Foreground=>Color.FromRgb(77,222,135),AppState.Minimized=>Color.FromRgb(84,163,255),AppState.BackgroundProcess=>Color.FromRgb(232,182,94),_=>Color.FromRgb(179,189,204)});line.ToolTip=state switch{AppState.Foreground=>"前台窗口",AppState.Minimized=>"已最小化",AppState.BackgroundProcess=>"后台进程",_=>"后台窗口"};}
        }
        catch(Exception ex){ActivityStore.Write("分组状态失败",message:ex.Message,error:ex);}
        finally{busy=false;}
    }
    Action? afterCollapse;
    public void Collapse(Action? after=null)
    {
        if(after!=null)afterCollapse+=after;if(closing)return;closing=true;var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(130));fade.Completed+=(_,_)=>{IsOpen=false;var action=afterCollapse;afterCollapse=null;action?.Invoke();};panel.BeginAnimation(UIElement.OpacityProperty,fade);var shrink=new DoubleAnimation(.9,TimeSpan.FromMilliseconds(130));scale.BeginAnimation(ScaleTransform.ScaleXProperty,shrink);scale.BeginAnimation(ScaleTransform.ScaleYProperty,shrink);
    }
}








