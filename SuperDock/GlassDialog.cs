using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
internal static class GlassDialog
{
    public static void Show(string message, string title = "SuperDock"){ActivityStore.Write(message.Contains("失败")||message.Contains("无法")?"操作失败":"界面提示",message:message);Display(message,title,false,"知道了",false); }
    public static bool Confirm(string message,string title="windock",string action="退出 Dock",bool danger=false) => Display(message,title,true,action,danger);
    static bool Display(string message,string title,bool confirm,string action,bool danger)
    {
        bool accepted=false;
        var window = new Window { Title = title, Width = 400, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        GlassWindow.Prepare(window);var panel = new StackPanel();
        if(danger)panel.Children.Add(new TextBlock{Text="\uE7BA",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=28,Foreground=Theme.Get("ThemeDanger"),Margin=new Thickness(0,0,0,16)});
        panel.Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Theme.Text });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 13, Margin = new Thickness(0,14,0,20), MaxHeight = 320 });
        var button = new Button { Content = confirm?action:"知道了", Style = (Style)Application.Current.FindResource("GlassButton"), MinWidth=96,Padding=new Thickness(18,10,18,10),Background=danger?new SolidColorBrush(Color.FromRgb(181,65,83)):Theme.Selected,Foreground=danger?Brushes.White:Theme.Text,HorizontalAlignment = HorizontalAlignment.Right, IsDefault = !confirm, IsCancel = !confirm };
        button.Click += (_,_) => {accepted=true;window.Close();};var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};if(confirm){var cancel=new Button{Content="取消",IsCancel=true,IsDefault=true,Style=(Style)Application.Current.FindResource("GlassButton"),Margin=new Thickness(0,0,10,0)};cancel.Click+=(_,_)=>window.Close();actions.Children.Add(cancel);}actions.Children.Add(button);panel.Children.Add(actions);
        window.Content = new Border { CornerRadius = new CornerRadius(20), Padding = new Thickness(24), BorderThickness = new Thickness(1), Background = GlassWindow.Surface, BorderBrush = (Brush)Application.Current.FindResource("GlassStroke"), Child = panel };
        window.ShowDialog();return accepted;
    }
}





