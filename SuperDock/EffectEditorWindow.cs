using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace SuperDock;
// Nexus-style property sheet for one dock effect: it edits a working copy and only
// writes back on 确定, so 取消 always leaves the live configuration untouched.
internal sealed class EffectEditorWindow : Window
{
    static readonly (string Name,string Hex)[] Swatches=[("#5AA8FF","#5AA8FF"),("#4DD287","#4DD287"),("#FFC14D","#FFC14D"),("#FF6B81","#FF6B81"),("#B07CFF","#B07CFF"),("#37D3D3","#37D3D3"),("#FF9F45","#FF9F45"),("#E8EAF0","#E8EAF0")];
    readonly MainWindow.Settings settings;
    readonly bool hover;
    readonly Action changed;
    readonly StackPanel body=new();
    double scale, range, smoothing, duration;
    bool pauseOnDrag;
    string color, hex;
    readonly Dictionary<string,Border> swatchFaces=new();
    readonly Dictionary<string,RadioButton> colorOptions=new();

    public EffectEditorWindow(MainWindow.Settings settings,bool hover,Action changed)
    {
        this.settings=settings;this.hover=hover;this.changed=changed;
        scale=settings.HoverScale;range=settings.HoverRange;smoothing=settings.HoverSmoothing;pauseOnDrag=settings.HoverPauseOnDrag;
        duration=settings.LaunchEffectDuration;color=settings.LaunchEffectColor;hex=settings.LaunchEffectColorHex;
        Theme.Apply(settings.ThemeMode);
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Windock;component/SettingsTheme.xaml",UriKind.Relative)});
        Resources["ThemeCard"]=new SolidColorBrush(Theme.Dark?Color.FromArgb(38,42,50,64):Color.FromArgb(40,255,255,255));
        Resources["SettingsAccent"]=new SolidColorBrush(Theme.Dark?Color.FromRgb(100,183,255):Color.FromRgb(0,112,214));
        Resources["ThemeHover"]=new SolidColorBrush(Theme.Dark?Color.FromArgb(65,113,143,180):Color.FromArgb(75,255,255,255));
        Title=hover?"放大效果设置":"传送 - 效果设置";
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;
        Width=460;SizeToContent=SizeToContent.Height;MaxHeight=Math.Max(420,SystemParameters.WorkArea.Height-60);
        WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");Foreground=Theme.Text;UseLayoutRounding=true;
        GlassWindow.Prepare(this);
        var shell=new Border{CornerRadius=new CornerRadius(20),BorderThickness=new Thickness(1),BorderBrush=Theme.Stroke,Background=GlassWindow.Surface};
        var layout=new DockPanel{Margin=new Thickness(24,20,24,20)};shell.Child=layout;Content=shell;
        var header=new Grid{Margin=new Thickness(0,0,0,6)};
        var title=new TextBlock{Text=Title,FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center};
        header.Children.Add(title);
        var close=Face("✕",Close);close.Style=(Style)FindResource("SettingsButton");close.Width=28;close.Height=28;close.MinHeight=28;close.BorderThickness=new Thickness(0);close.Background=Brushes.Transparent;close.Padding=new Thickness(0);close.HorizontalContentAlignment=HorizontalAlignment.Center;close.HorizontalAlignment=HorizontalAlignment.Right;close.ToolTip="关闭";header.Children.Add(close);
        header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is not Button&&e.LeftButton==MouseButtonState.Pressed)DragMove();};
        DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var cancel=Face("取消",Close);cancel.Style=(Style)FindResource("SettingsButton");cancel.MinWidth=88;cancel.MinHeight=32;cancel.IsCancel=true;cancel.Margin=new Thickness(0,0,10,0);footer.Children.Add(cancel);
        var accept=Face("确定",Accept);accept.Style=(Style)FindResource("SettingsButton");accept.MinWidth=88;accept.MinHeight=32;accept.IsDefault=true;accept.Background=(Brush)FindResource("SettingsAccent");accept.Foreground=Brushes.White;footer.Children.Add(accept);
        var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-260)};
        layout.Children.Add(scroll);
        if(hover)HoverFields();else LaunchFields();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
    }
    static TextBlock Caption(string text,double size,Brush brush)=>new(){Text=text,FontSize=size,Foreground=brush,TextWrapping=TextWrapping.Wrap};
    Brush SettingsCard=>new SolidColorBrush(Theme.Dark?Color.FromArgb(38,42,50,64):Color.FromArgb(40,255,255,255));
    void Section(string title,string description)
    {
        if(title.Length>0){var head=Caption(title,13,Theme.Text);head.Margin=new Thickness(0,body.Children.Count==0?2:16,0,0);body.Children.Add(head);}
        if(description.Length>0){var detail=Caption(description,11,Theme.Muted);detail.Margin=new Thickness(0,6,0,6);body.Children.Add(detail);}
    }
    // Same card treatment as the settings page list, so both surfaces read the same.
    void Card(UIElement child)
    {
        UniformGrid grid;
        if(body.Children.Count>0&&body.Children[body.Children.Count-1] is UniformGrid existing&&existing.Tag as string=="cards")grid=existing;
        else{grid=new UniformGrid{Columns=1,Tag="cards",Margin=new Thickness(-4,0,-4,4)};body.Children.Add(grid);}
        grid.Children.Add(new Border{Background=SettingsCard,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(12),Margin=new Thickness(4,4,4,4),Child=child});
    }
    void Slider(string title,string description,double min,double max,double value,string unit,Action<double> update,double step=1,double display=1)
    {
        var stack=new StackPanel{ToolTip=description};
        var header=new DockPanel();
        var label=Caption((value*display).ToString("0.##")+unit,12,Theme.Muted);label.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(label,Dock.Right);header.Children.Add(label);
        header.Children.Add(Caption(title,13,Theme.Text));stack.Children.Add(header);
        if(description.Length>0){var detail=Caption(description,11,Theme.Muted);detail.Margin=new Thickness(0,5,0,0);stack.Children.Add(detail);}
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,SmallChange=step,Margin=new Thickness(0,7,0,0)};
        slider.ValueChanged+=(_,_)=>{label.Text=(slider.Value*display).ToString("0.##")+unit;update(slider.Value);};
        stack.Children.Add(slider);Card(stack);
    }
    void Check(string title,string description,bool value,Action<bool> update)
    {
        var row=new DockPanel{ToolTip=description};row.Children.Add(new StackPanel{Margin=new Thickness(0,0,12,0)});
        var labels=(StackPanel)row.Children[0];labels.Children.Add(Caption(title,13,Theme.Text));var detail=Caption(description,11,Theme.Muted);detail.Margin=new Thickness(0,6,0,0);labels.Children.Add(detail);
        var box=new ToggleButton{Style=(Style)FindResource("SettingsSwitch"),IsChecked=value,VerticalAlignment=VerticalAlignment.Center,ToolTip=title};
        System.Windows.Automation.AutomationProperties.SetName(box,title);
        box.Click+=(_,_)=>update(box.IsChecked==true);
        DockPanel.SetDock(box,Dock.Right);row.Children.Add(box);Card(row);
    }
    void HoverFields()
    {
        Slider("放大尺寸","你希望放大后的图标有多大",1,2.5,scale," 倍",v=>scale=v,.05);
        Slider("放大范围","多少个图标同时参与放大",1,7,range," 个图标",v=>range=v,.5);
        Slider("放大平滑","Dock 由原始尺寸到完全放大需要多长时间",0,100,smoothing*100,"%",v=>smoothing=v/100,5);
        Check("拖放时暂停放大","向 Dock 拖动项目时临时关闭放大效果",pauseOnDrag,v=>pauseOnDrag=v);
    }
    void LaunchFields()
    {
        Section("颜色","启动图标的光晕取色方式");
        var options=new StackPanel();
        foreach(var (id,label) in new[]{("None","无着色"),("Custom","着色"),("HotTrack","着色 Hot-Track")})
        {
            var radio=new RadioButton{Content=label,GroupName="launchColor",Foreground=Theme.Text,FontSize=12,Margin=new Thickness(0,0,0,8),Cursor=Cursors.Hand};
            radio.IsChecked=color==id;
            radio.Checked+=(_,_)=>{if(!radio.IsChecked==true)return;color=id;RefreshSwatches();};
            colorOptions[id]=radio;options.Children.Add(radio);
        }
        var swatches=new WrapPanel{Margin=new Thickness(24,2,0,0)};
        foreach(var (name,paint) in Swatches)
        {
            var face=new Border{Width=26,Height=18,CornerRadius=new CornerRadius(5),Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(paint)),BorderThickness=new Thickness(2),BorderBrush=Brushes.Transparent,Margin=new Thickness(0,0,8,8),Cursor=Cursors.Hand,ToolTip=paint};
            face.MouseLeftButtonDown+=(_,e)=>{hex=paint;color="Custom";colorOptions["Custom"].IsChecked=true;RefreshSwatches();e.Handled=true;};
            swatchFaces[paint]=face;swatches.Children.Add(face);
        }
        options.Children.Add(swatches);Card(options);
        var note=new Border{CornerRadius=new CornerRadius(10),Padding=new Thickness(12),Margin=new Thickness(0,10,0,0),Background=new SolidColorBrush(Theme.Dark?Color.FromArgb(46,58,72,92):Color.FromArgb(28,120,160,220))};
        var info=new StackPanel{Orientation=Orientation.Horizontal};
        info.Children.Add(new TextBlock{Text="\uE946",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=18,Foreground=(Brush)FindResource("SettingsAccent"),VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,1,10,0)});
        info.Children.Add(Caption("Hot-Track 会根据图标的主色自动着色，效果类似 Windows 任务栏按钮的发光。",11,Theme.Muted));
        note.Child=info;body.Children.Add(note);
        Slider("持续时间","效果需要持续多长时间",0,100,duration*100,"%",v=>duration=v/100,5);
        RefreshSwatches();
    }
    void RefreshSwatches()
    {
        foreach(var radio in colorOptions)radio.Value.IsChecked=color==radio.Key;
        bool custom=color=="Custom";
        foreach(var (paint,face) in swatchFaces){face.Opacity=custom?1:.4;face.BorderBrush=paint==hex?(Brush)FindResource("SettingsAccent"):Brushes.Transparent;}
        if(color=="None"&&hex.Length==0)hex=Swatches[0].Hex;
    }
    void Accept()
    {
        if(hover)
        {
            settings.HoverScale=Math.Clamp(scale,1,2.5);settings.HoverRange=Math.Clamp(range,1,7);settings.HoverSmoothing=Math.Clamp(smoothing,0,1);settings.HoverPauseOnDrag=pauseOnDrag;
        }
        else
        {
            settings.LaunchEffectColor=color;settings.LaunchEffectColorHex=hex;settings.LaunchEffectDuration=Math.Clamp(duration,0,1);
        }
        changed();Close();
    }
    Button Face(string text,Action action)
    {
        var button=new Button{Content=text,Style=(Style)FindResource("SettingsButton"),Foreground=Theme.Text};
        button.Click+=(_,_)=>action();return button;
    }
}
