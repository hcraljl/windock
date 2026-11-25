using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
internal sealed partial class AppearanceWindow
{
    void AppearancePreferences()
    {
        StackPanel Collect(Action build){int start=content.Children.Count;build();var result=new StackPanel();while(content.Children.Count>start){var child=content.Children[start];content.Children.RemoveAt(start);result.Children.Add(child);}return result;}
        var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});top.ColumnDefinitions.Add(new ColumnDefinition());
        var theme=Collect(ThemePreferences);var effects=Collect(EffectPreferences);top.Children.Add(theme);Grid.SetColumn(effects,2);top.Children.Add(effects);content.Children.Add(top);SizePreferences();
        top.SizeChanged+=(_,_)=>{bool narrow=top.ActualWidth<520;if(narrow){top.RowDefinitions.Clear();top.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});top.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(effects,1);Grid.SetColumn(effects,0);Grid.SetColumnSpan(theme,3);Grid.SetColumnSpan(effects,3);}else{Grid.SetRow(effects,0);Grid.SetColumn(effects,2);Grid.SetColumnSpan(theme,1);Grid.SetColumnSpan(effects,1);}};
    }
    FrameworkElement ThemeDiagram(bool dark)
    {
        var canvas=new Canvas{Width=154,Height=72,ClipToBounds=true};
        Brush fill= new SolidColorBrush(dark?Color.FromRgb(31,34,40):Color.FromRgb(245,247,250));
        canvas.Children.Add(new Border{Width=154,Height=72,Background=fill,CornerRadius=new CornerRadius(6,6,0,0)});
        canvas.Children.Add(new Border{Width=43,Height=72,Background=new SolidColorBrush(dark?Color.FromRgb(46,50,57):Color.FromRgb(225,232,242)),CornerRadius=new CornerRadius(6,0,0,0)});
        for(int i=0;i<4;i++){var line=new Border{Width=29,Height=5,CornerRadius=new CornerRadius(2),Background=new SolidColorBrush(i==1?Color.FromRgb(74,153,244):dark?Color.FromRgb(96,106,121):Color.FromRgb(186,199,215))};Canvas.SetLeft(line,7);Canvas.SetTop(line,15+i*12);canvas.Children.Add(line);}
        for(int i=0;i<3;i++){var row=new Border{Width=88,Height=12,CornerRadius=new CornerRadius(3),Background=new SolidColorBrush(dark?Color.FromRgb(49,54,62):Colors.White)};Canvas.SetLeft(row,54);Canvas.SetTop(row,15+i*17);canvas.Children.Add(row);var toggle=new Border{Width=14,Height=7,CornerRadius=new CornerRadius(4),Background=new SolidColorBrush(i==0?Color.FromRgb(39,139,246):dark?Color.FromRgb(104,112,126):Color.FromRgb(195,205,218))};Canvas.SetLeft(toggle,122);Canvas.SetTop(toggle,17+i*17);canvas.Children.Add(toggle);}
        return canvas;
    }
    void Tabs(string[] names,string selected,Action<string> choose)
    {
        var tabs=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var name in names){var button=Button(name,()=>choose(name));button.Style=(Style)FindResource("SettingsTab");button.Tag=name==selected?"selected":null;button.Margin=new Thickness(0,0,16,0);button.Padding=new Thickness(2,7,2,10);button.MinHeight=34;tabs.Children.Add(button);}
        tabHost.Children.Add(new Border{BorderBrush=Theme.Stroke,BorderThickness=new Thickness(0,0,0,.5),Child=tabs,Margin=new Thickness(0,0,12,2)});
    }
}
