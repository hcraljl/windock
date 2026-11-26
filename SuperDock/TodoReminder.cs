using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
// 今日待办提醒弹窗：到达提醒时间时在屏幕右下角列出仍未完成的事项及内容，支持逐条标记完成、稍后提醒。
sealed class TodoReminder : Window
{
    readonly MainWindow.Settings settings;
    readonly List<MainWindow.TodoItem> due;
    readonly Action changed;
    readonly StackPanel list=new();
    TodoReminder(MainWindow.Settings settings,List<MainWindow.TodoItem> due,Action changed)
    {
        this.settings=settings;this.due=due;this.changed=changed;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 今日代办提醒";
        SizeToContent=SizeToContent.Height;Width=360;WindowStartupLocation=WindowStartupLocation.Manual;
        var panel=new StackPanel();
        panel.Children.Add(new DockPanel{LastChildFill=true,Children={new TextBlock{Text="\uE7BF",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=16,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center},new TextBlock{Text="今日代办 · 待完成提醒",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center}}});
        panel.Children.Add(list);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        var snooze=Button("稍后提醒",()=>{foreach(var item in due){item.Notified=false;item.RemindAt=DateTime.Now.AddMinutes(5);}changed();Close();});
        snooze.Background=GlassWindow.Surface;actions.Children.Add(snooze);
        var close=Button("关闭",Close);close.Margin=new Thickness(8,0,0,0);actions.Children.Add(close);
        panel.Children.Add(actions);
        Content=new Border{CornerRadius=new CornerRadius(18),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(18,14,18,14),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=20,ShadowDepth=4,Opacity=.32}};
        Refresh();
        KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
    }
    static Button Button(string label,Action action)=>new(){Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=30,Padding=new Thickness(12,2,12,2),FontSize=12.5,Foreground=Theme.Text,Cursor=System.Windows.Input.Cursors.Hand,Background=Theme.Selected};
    void Refresh()
    {
        list.Children.Clear();
        foreach(var item in due.ToList())
        {
            if(item.Done)continue;
            var row=new DockPanel{Margin=new Thickness(0,10,0,0)};
            var check=Button("\uE73E",()=>{item.Done=true;changed();Refresh();});
            check.Width=30;check.Height=30;check.Padding=new Thickness(0);check.FontFamily=new FontFamily("Segoe Fluent Icons");check.FontSize=13;check.Background=GlassWindow.Surface;check.ToolTip="标记完成";DockPanel.SetDock(check,Dock.Left);
            row.Children.Add(check);
            var body=new StackPanel{Margin=new Thickness(10,0,8,0)};
            body.Children.Add(new TextBlock{Text=item.Text,FontSize=13.5,Foreground=Theme.Text,TextWrapping=TextWrapping.Wrap,MaxWidth=230,TextAlignment=TextAlignment.Left});
            body.Children.Add(new TextBlock{Text="提醒时间 "+item.RemindAt.ToString("MM/dd HH:mm"),FontSize=11,Foreground=Theme.Muted,Margin=new Thickness(0,3,0,0)});
            row.Children.Add(body);
            var remove=Button("\uE711",()=>{settings.Todos.Remove(item);due.Remove(item);changed();Refresh();if(due.All(d=>d.Done))Close();});
            remove.Width=28;remove.Height=28;remove.Padding=new Thickness(0);remove.FontFamily=new FontFamily("Segoe Fluent Icons");remove.FontSize=12;remove.Background=GlassWindow.Surface;remove.ToolTip="删除该待办";DockPanel.SetDock(remove,Dock.Right);
            row.Children.Add(remove);
            list.Children.Add(row);
        }
        if(list.Children.Count==0)Close();
    }
    // 到达时间未完成的待办弹窗提醒；仅主 Dock 触发一次，避免多屏重复弹窗。
    internal static void CheckAndShow(MainWindow owner,MainWindow.Settings settings,Action changed)
    {
        var due=settings.Todos.Where(item=>item!=null&&!item.Done&&!item.Notified&&item.RemindAt<=DateTime.Now).ToList();
        if(due.Count==0)return;
        foreach(var item in due)item.Notified=true;
        changed();
        if(owner.TodoReminderWindow is {IsVisible:true})return;
        var window=new TodoReminder(settings,due,changed);
        owner.TodoReminderWindow=window;
        window.Loaded+=(_,_)=>{
            var work=SystemParameters.WorkArea;
            window.Left=Math.Max(work.Left+8,work.Right-window.ActualWidth-24);
            window.Top=Math.Max(work.Top+8,work.Bottom-window.ActualHeight-24);
        };
        window.Show();
    }
}
