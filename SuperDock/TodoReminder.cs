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
    readonly TextBlock countText=new(){FontSize=11.5,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};
    TodoReminder(MainWindow.Settings settings,List<MainWindow.TodoItem> due,Action changed)
    {
        this.settings=settings;this.due=due;this.changed=changed;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 今日代办提醒";
        SizeToContent=SizeToContent.Height;Width=364;WindowStartupLocation=WindowStartupLocation.Manual;
        var panel=new StackPanel();
        // 头部：图标 + 标题 + 待完成计数
        var header=new DockPanel{LastChildFill=false};
        var badge=new Border{Width=30,Height=30,CornerRadius=new CornerRadius(15),Background=Theme.Selected,Child=new TextBlock{Text="\uE7BF",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=14,Foreground=Theme.Text,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        header.Children.Add(badge);
        header.Children.Add(new TextBlock{Text="今日代办提醒",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)});
        header.Children.Add(countText);
        DockPanel.SetDock(countText,Dock.Right);
        panel.Children.Add(header);
        panel.Children.Add(list);
        // 底部操作：稍后提醒 / 关闭
        var actions=new DockPanel{LastChildFill=false,Margin=new Thickness(0,14,0,0)};
        var hint=new TextBlock{Text="标记完成后不再提醒",FontSize=11,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};
        actions.Children.Add(hint);
        var close=ActionButton("关闭",Close,Theme.Selected);
        DockPanel.SetDock(close,Dock.Right);
        var snooze=ActionButton("稍后 5 分钟提醒",()=>{foreach(var item in due){item.Notified=false;item.RemindAt=DateTime.Now.AddMinutes(5);}changed();Close();},GlassWindow.Surface);
        DockPanel.SetDock(snooze,Dock.Right);snooze.Margin=new Thickness(8,0,8,0);
        actions.Children.Add(snooze);actions.Children.Add(close);
        panel.Children.Add(actions);
        Content=new Border{CornerRadius=new CornerRadius(18),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(18,15,18,15),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=20,ShadowDepth=4,Opacity=.32}};
        Refresh();
        KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
    }
    Button ActionButton(string label,Action action,Brush background)
    {
        var button=new Button{Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=30,Padding=new Thickness(13,2,13,2),FontSize=12.5,Foreground=Theme.Text,Cursor=System.Windows.Input.Cursors.Hand,Background=background};
        button.Click+=(_,_)=>action();return button;
    }
    void Refresh()
    {
        var pending=due.Where(item=>!item.Done).ToList();
        countText.Text=pending.Count==0?"全部完成":$"{pending.Count} 项待完成";
        list.Children.Clear();
        foreach(var item in pending)
        {
            // 事项卡片：完成按钮 + 内容/时间 + 悬停反馈
            var body=new StackPanel{Margin=new Thickness(10,0,8,0)};
            body.Children.Add(new TextBlock{Text=item.Text,FontSize=13.5,Foreground=Theme.Text,TextWrapping=TextWrapping.Wrap,MaxWidth=250,TextAlignment=TextAlignment.Left});
            bool overdue=item.RemindAt<DateTime.Now;
            var chip=new Border{CornerRadius=new CornerRadius(7),Padding=new Thickness(7,2,7,2),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,5,0,0),Background=overdue?Theme.Get("ThemeDanger"):Theme.Card};
            chip.Child=new TextBlock{Text=(overdue?"已到提醒时间 · ":"提醒 ")+item.RemindAt.ToString("MM/dd HH:mm"),FontSize=10.5,Foreground=overdue?Theme.Get("ThemeDanger"):Theme.Muted};
            body.Children.Add(chip);
            var check=ActionButton("\uE73E",()=>{
                item.Done=true;changed();Refresh();
                if(due.All(d=>d.Done))Close();
            },GlassWindow.Surface);
            check.Width=28;check.Height=28;check.Padding=new Thickness(0);check.FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");check.FontSize=13;check.ToolTip="标记完成";check.Margin=new Thickness(0,1,0,0);
            var row=new DockPanel{LastChildFill=true};
            row.Children.Add(check);row.Children.Add(body);
            var card=new Border{Background=Theme.Card,CornerRadius=new CornerRadius(12),Padding=new Thickness(11,10,11,10),Margin=new Thickness(0,8,0,0),Child=row};
            card.MouseEnter+=(_,_)=>card.Background=Theme.Get("ThemeHover");
            card.MouseLeave+=(_,_)=>card.Background=Theme.Card;
            list.Children.Add(card);
        }
        if(pending.Count==0)Close();
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
