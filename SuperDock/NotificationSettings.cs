using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;
internal sealed partial class AppearanceWindow
{
    void NotificationPreferences()
    {
        Heading("提醒设置");Toggle("启用消息提醒","图标旁显示消息气泡提醒",settings.MessageRemindersEnabled,value=>settings.MessageRemindersEnabled=value);
        Range("提醒透明度","仅改变提醒背景，文字与图标保持清晰",0,80,(1-(double.IsFinite(settings.MessageReminderOpacity)?Math.Clamp(settings.MessageReminderOpacity,.2,1):.65))*100,"%",v=>{settings.MessageReminderOpacity=1-v/100;MessageReminders.UpdateOpacity(settings.MessageReminderOpacity);});
        ActionCard("预览提醒",settings.MessagePreviewSeconds<=0?"图标旁持续展示消息气泡，点击气泡查看会话后收起":$"图标旁展示约 {settings.MessagePreviewSeconds} 秒的消息气泡，点击气泡查看会话","\uE7F4",()=>{if(Owner is MainWindow dock)MessageReminders.Display(dock,settings,"微信","微信\n这是一条示例消息，用于预览气泡样式。",IntPtr.Zero);});
        var duration=new StackPanel{ToolTip="消息气泡在图标旁展示的时长；选择一直显示时，气泡保持展示直到点击查看"};
        var durationHeader=new DockPanel();var durationLabel=new TextBlock{Text=settings.MessagePreviewSeconds<=0?"一直显示":$"{settings.MessagePreviewSeconds} 秒",FontSize=12,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(durationLabel,Dock.Right);durationHeader.Children.Add(durationLabel);durationHeader.Children.Add(new TextBlock{Text="预览时长",FontSize=13});duration.Children.Add(durationHeader);
        var sliderRow=new DockPanel{Margin=new Thickness(0,10,0,0)};
        var slider=new Slider{Minimum=1,Maximum=5,Value=settings.MessagePreviewSeconds>0?settings.MessagePreviewSeconds:3,TickFrequency=1,IsSnapToTickEnabled=true,SmallChange=1,Margin=new Thickness(0,2,12,0),IsEnabled=settings.MessagePreviewSeconds>0};
        if(settings.MessagePreviewSeconds<=0)slider.Opacity=.45;
        slider.ValueChanged+=(_,_)=>{if(settings.MessagePreviewSeconds>0)settings.MessagePreviewSeconds=(int)slider.Value;durationLabel.Text=settings.MessagePreviewSeconds<=0?"一直显示":$"{(int)slider.Value} 秒";changed();};
        sliderRow.Children.Add(slider);
        var always=new Button{Content="一直显示",Padding=new Thickness(12,6,12,6),MinHeight=28,FontSize=11.5,ToolTip="开启后气泡保持展示，直到点击查看；拖动条暂停生效"};
        if(settings.MessagePreviewSeconds<=0){always.Background=Theme.Selected;always.BorderBrush=(Brush)Resources["SettingsAccent"];always.Foreground=(Brush)Resources["SettingsAccent"];}
        always.Click+=(_,_)=>{
            if(settings.MessagePreviewSeconds>0){settings.MessagePreviewLastSeconds=settings.MessagePreviewSeconds;settings.MessagePreviewSeconds=0;}
            else settings.MessagePreviewSeconds=settings.MessagePreviewLastSeconds>0?settings.MessagePreviewLastSeconds:3;
            changed();Navigate(page);
        };
        DockPanel.SetDock(always,Dock.Right);sliderRow.Children.Add(always);duration.Children.Add(sliderRow);Card(duration);
        var labels=new Grid{Margin=new Thickness(12,16,12,8)};labels.ColumnDefinitions.Add(new ColumnDefinition());labels.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(68)});labels.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(68)});
        labels.Children.Add(new TextBlock{Text="应用",FontSize=11,Foreground=Theme.Muted});
        foreach(var (caption,column) in new[]{("提醒",1),("详情",2)}){var label=new TextBlock{Text=caption,FontSize=11,Foreground=Theme.Muted,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetColumn(label,column);labels.Children.Add(label);}content.Children.Add(labels);
        foreach(var client in MessageReminders.Clients){
            string name=client;bool enabled=settings.MessageReminderClients.Contains(name),readable=true;
            var row=new Grid{MinHeight=36};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(44)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(68)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(68)});
            var image=new Image{Source=NotificationLogo.Create(name),Width=30,Height=30,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);row.Children.Add(image);_=LoadNotificationIcon(image,name);
            string status=name switch{"QQ"=>MessageContentSources.QqListening?"系统通知已连接":"需授权通知读取","微信"=>MessageContentSources.WechatStatus,"企业微信"=>WecomContentReader.Status,_=>"新消息图标提醒"};
            var identity=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};identity.Children.Add(new TextBlock{Text=name,FontSize=13,Foreground=Theme.Text});identity.Children.Add(new TextBlock{Text=status,FontSize=11,Foreground=Theme.Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,0),ToolTip=status});Grid.SetColumn(identity,1);row.Children.Add(identity);
            var toggle=new ToggleButton{Style=(Style)FindResource("SettingsSwitch"),IsChecked=enabled,IsEnabled=settings.MessageRemindersEnabled,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,ToolTip=name+"消息提醒"};System.Windows.Automation.AutomationProperties.SetName(toggle,name+"消息提醒");toggle.Click+=(_,_)=>{if(toggle.IsChecked==true){if(!settings.MessageReminderClients.Contains(name))settings.MessageReminderClients.Add(name);}else settings.MessageReminderClients.Remove(name);changed();Navigate(page);};Grid.SetColumn(toggle,2);row.Children.Add(toggle);
            var details=new ToggleButton{Style=(Style)FindResource("SettingsSwitch"),IsChecked=readable&&MessageContentSources.Details(settings,name),IsEnabled=enabled&&settings.MessageRemindersEnabled,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,ToolTip="显示发送人与消息摘要（读取该应用的系统通知）"};System.Windows.Automation.AutomationProperties.SetName(details,name+"消息详情");details.Click+=(_,_)=>{settings.MessageDetailOverrides[name]=details.IsChecked==true;changed();};Grid.SetColumn(details,3);row.Children.Add(details);PreferenceRow(row);
        }
        var actions=new WrapPanel{Margin=new Thickness(0,10,0,0)};
        var permission=Button("授权通知读取",async()=>{await MessageContentSources.RequestPermission();if(!lifetime.IsCancellationRequested)Navigate(page);});permission.Margin=new Thickness(0,0,8,8);actions.Children.Add(permission);
        var check=Button("检测微信详情",async()=>{var handle=await Task.Run(()=>MessageContentSources.FindClientWindow("微信"));await MessageContentSources.WechatPreview(handle);if(!lifetime.IsCancellationRequested)Navigate(page);});check.Margin=new Thickness(0,0,0,8);actions.Children.Add(check);content.Children.Add(actions);
        content.Children.Add(Text("消息详情取决于系统通知与客户端开放的会话信息。",11));
    }
    async Task LoadNotificationIcon(Image image,string client)
    {
        try{var apps=await ApplicationCatalog.GetAsync().WaitAsync(lifetime.Token);var app=apps.FirstOrDefault(a=>MessageReminders.Client(RunningWindows.WindowTarget(a.Target))==client);if(app!=null){var source=await ShellIcons.LoadAsync(app.Target).WaitAsync(lifetime.Token);if(!lifetime.IsCancellationRequested&&source!=null)image.Source=source;}}catch(OperationCanceledException){}catch{}
    }
}
