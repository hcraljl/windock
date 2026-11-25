using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace SuperDock;
internal sealed partial class AppearanceWindow
{
    List<ActivityEvent> ReadHistory()
    {
        try{return ActivityStore.Read();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){content.Children.Add(Text("历史读取失败："+ex.Message));return [];}
    }
    void Logs()
    {
        var entries=ReadHistory();string level="全部";int index=0;var list=new StackPanel();var query=new TextBox{Height=36,ToolTip="搜索应用、错误或日志内容",Margin=new Thickness(0,8,0,12)};
        var info=Text("",11);var pager=new StackPanel{Orientation=Orientation.Horizontal};
        var toolbar=new DockPanel{LastChildFill=true,Margin=new Thickness(0,0,0,10)};
        var errors=new CheckBox{Content="仅看错误",Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,12,0)};DockPanel.SetDock(errors,Dock.Right);toolbar.Children.Add(errors);
        var refresh=Button("刷新",()=>Navigate(page));refresh.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(refresh,Dock.Right);toolbar.Children.Add(refresh);
        query.ToolTip="搜索日志";query.Padding=new Thickness(10,4,10,4);query.Margin=new Thickness(0);toolbar.Children.Add(query);content.Children.Add(toolbar);
        errors.Checked+=(_,_)=>{level="错误";index=0;Refresh();};errors.Unchecked+=(_,_)=>{level="全部";index=0;Refresh();};        content.Children.Add(info);content.Children.Add(list);
        pager.Children.Add(Button("上一页",()=>{index=Math.Max(0,index-1);Refresh();}));pager.Children.Add(Button("下一页",()=>{index++;Refresh();}));content.Children.Add(pager);
        content.Children.Add(Button("导出全部日志…",()=>{var save=new Microsoft.Win32.SaveFileDialog{Filter="日志压缩包|*.zip",FileName="windock-日志-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip"};if(save.ShowDialog(this)!=true)return;try{if(!Directory.Exists(ActivityStore.Folder))Directory.CreateDirectory(ActivityStore.Folder);using var stream=new FileStream(save.FileName,FileMode.Create);using var archive=new ZipArchive(stream,ZipArchiveMode.Create);foreach(var file in Directory.EnumerateFiles(ActivityStore.Folder,"*.jsonl"))archive.CreateEntryFromFile(file,Path.GetFileName(file));}catch(IOException ex){GlassDialog.Show("导出失败："+ex.Message);}}));
        content.Children.Add(Text("日志仅保存在本机，按月归档，不自动清理。",11));
        void Refresh()
        {
            var matches=entries.Where(e=>(level=="全部"||e.Level==level||e.Kind==level)&&($"{e.Name} {e.Kind} {e.Target} {e.Message} {e.Details}").Contains(query.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(e=>e.Time).ToArray();int pages=Math.Max(1,(matches.Length+29)/30);index=Math.Clamp(index,0,pages-1);info.Text=$"{matches.Length} 条记录";pager.Visibility=pages>1?Visibility.Visible:Visibility.Collapsed;list.Children.Clear();
            foreach(var entry in matches.Skip(index*30).Take(30))
            {
                var labels=new StackPanel();labels.Children.Add(new TextBlock{Text=$"{entry.Time.LocalDateTime:MM-dd HH:mm:ss}   {entry.Kind}   {entry.Name}",Foreground=entry.Level=="错误"?Theme.Get("ThemeDanger"):Theme.Text,FontSize=12,TextWrapping=TextWrapping.Wrap});if(entry.Level=="错误")labels.Children.Add(new TextBlock{Text=entry.Message,Foreground=Theme.Muted,FontSize=11,Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap});
                var detail=new TextBox{Text=$"时间：{entry.Time:O}\n目标：{entry.Target}\n{entry.Details}",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};if(entry.Level=="错误")labels.Children.Add(new Expander{Header="错误详情",Content=detail,Foreground=Theme.Muted,FontSize=11,Margin=new Thickness(0,6,0,0)});list.Children.Add(new Border{Child=labels,Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,0,0,2),CornerRadius=new CornerRadius(6),Background=SettingsCard});
            }
            if(matches.Length==0)list.Children.Add(Text("暂无匹配日志"));
        }
        query.TextChanged+=(_,_)=>{index=0;Refresh();};Refresh();
    }
    void Analytics()
    {
        var entries=ReadHistory();var range=Text("",12);var body=new StackPanel();string period="按天";var date=new DatePicker{SelectedDate=DateTime.Today};
        var dateLabel=new TextBlock{Foreground=Theme.Text,FontSize=13,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var picker=new Popup{StaysOpen=false,AllowsTransparency=true,Placement=PlacementMode.Bottom};
        var calendar=new Calendar{SelectedDate=DateTime.Today,DisplayDate=DateTime.Today};picker.Child=new Border{Child=calendar,Background=Theme.Fill,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(8)};
        var toolbar=new Grid{Margin=new Thickness(0,0,0,8)};toolbar.ColumnDefinitions.Add(new ColumnDefinition());toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var dates=new StackPanel{Orientation=Orientation.Horizontal};
        var previous=Button("‹",()=>Shift(-1));previous.Width=34;previous.Height=36;previous.Padding=new Thickness(0);previous.HorizontalContentAlignment=HorizontalAlignment.Center;dates.Children.Add(previous);
        var dateButton=Button("",()=>{calendar.DisplayDate=date.SelectedDate??DateTime.Today;picker.IsOpen=true;});dateButton.Content=dateLabel;dateButton.Width=170;dateButton.Height=36;dateButton.Margin=new Thickness(5,0,5,0);dateButton.Padding=new Thickness(8,0,8,0);picker.PlacementTarget=dateButton;dates.Children.Add(dateButton);
        var next=Button("›",()=>Shift(1));next.Width=34;next.Height=36;next.Padding=new Thickness(0);next.HorizontalContentAlignment=HorizontalAlignment.Center;dates.Children.Add(next);toolbar.Children.Add(dates);
        var modes=new UniformGrid{Columns=3,Width=180};var modeButtons=new Dictionary<string,Button>();foreach(var label in new[]{"按天","按周","按月"}){var button=Button(label,()=>{});button.Height=36;button.Padding=new Thickness(8,0,8,0);button.Margin=new Thickness(2,0,2,0);button.HorizontalContentAlignment=HorizontalAlignment.Center;button.Click+=(_,_)=>{period=label;Refresh();};modeButtons[label]=button;modes.Children.Add(button);}Grid.SetColumn(modes,1);toolbar.Children.Add(modes);content.Children.Add(new Border{Child=toolbar,Padding=new Thickness(0,8,0,8),CornerRadius=new CornerRadius(6),Background=SettingsCard});
        var shortcuts=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};shortcuts.Children.Add(Button("回到今天",()=>date.SelectedDate=DateTime.Today));shortcuts.Children.Add(Button("刷新数据",()=>Navigate(page)));content.Children.Add(shortcuts);
        calendar.SelectedDatesChanged+=(_,_)=>{if(calendar.SelectedDate is DateTime selected){date.SelectedDate=selected;picker.IsOpen=false;}};
        void Shift(int direction){var selected=date.SelectedDate??DateTime.Today;date.SelectedDate=period switch{"按月"=>selected.AddMonths(direction),"按周"=>selected.AddDays(7*direction),_=>selected.AddDays(direction)};}        range.Margin=new Thickness(0,14,0,14);content.Children.Add(range);content.Children.Add(body);
        content.Children.Add(Text("仅统计通过windock 提交成功的应用启动请求；窗口切换、启动失败和系统外部启动不计入。历史从此版本开始记录。",11));
        void Refresh()
        {
            dateLabel.Text=(date.SelectedDate??DateTime.Today).ToString("yyyy年 M月 d日")+"  ▾";foreach(var pair in modeButtons)pair.Value.Background=pair.Key==period?Theme.Selected:Theme.Card;
            DateTime anchor=(date.SelectedDate??DateTime.Today).Date;DateTime start=period switch{"按周"=>anchor.AddDays(-((int)anchor.DayOfWeek+6)%7),"按月"=>new DateTime(anchor.Year,anchor.Month,1),_=>anchor};DateTime end=period switch{"按周"=>start.AddDays(7),"按月"=>start.AddMonths(1),_=>start.AddDays(1)};
            var launches=entries.Where(e=>e.Kind=="应用启动"&&e.Time.LocalDateTime>=start&&e.Time.LocalDateTime<end).ToArray();var ranked=ActivityStore.Statistics(entries,start,end).ToArray();
            range.Text=$"{start:yyyy-MM-dd} 至 {end.AddDays(-1):yyyy-MM-dd} · {launches.Length} 次启动 · {ranked.Length} 个应用";body.Children.Clear();body.Children.Add(new TextBlock{Text="热门应用",FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,Margin=new Thickness(0,4,0,12)});
            foreach(var app in ranked)
            {
                var row=new Grid{Margin=new Thickness(0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(65)});
                var icon=new Image{Source=ShellIcons.Load(app.Target),Width=28,Height=28};row.Children.Add(icon);var label=new StackPanel{Margin=new Thickness(10,0,10,0)};label.Children.Add(new TextBlock{Text=app.Name,FontSize=13,Foreground=Theme.Text});label.Children.Add(new Border{Height=4,CornerRadius=new CornerRadius(2),Background=Theme.Selected,HorizontalAlignment=HorizontalAlignment.Left,Width=200d*app.Count/Math.Max(1,ranked[0].Count),Margin=new Thickness(0,8,0,0)});Grid.SetColumn(label,1);row.Children.Add(label);var count=new TextBlock{Text=app.Count+" 次",Foreground=Theme.Muted,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(count,2);row.Children.Add(count);body.Children.Add(new Border{Child=row,Padding=new Thickness(10,8,10,8),Background=SettingsCard,CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,0,3)});
            }
            if(ranked.Length==0)body.Children.Add(Text("这个时间段还没有启动记录"));
            if(period!="按天"&&launches.Length>0){body.Children.Add(new TextBlock{Text="每日启动",FontSize=14,Foreground=Theme.Text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,12)});for(var day=start;day<end;day=day.AddDays(1)){int count=launches.Count(e=>e.Time.LocalDateTime.Date==day);body.Children.Add(new TextBlock{Text=$"{day:MM-dd ddd}     {count} 次",Foreground=Theme.Muted,Margin=new Thickness(0,4,0,4)});}}
        }
        date.SelectedDateChanged+=(_,_)=>Refresh();Refresh();
    }
}





