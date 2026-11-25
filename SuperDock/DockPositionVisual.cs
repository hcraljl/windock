using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SuperDock;
internal static class DockPositionVisual
{
    public static FrameworkElement Create(string edge,bool compact=false)
    {
        var screen=new Grid{Width=compact?28:58,Height=compact?21:38};
        screen.Children.Add(new Border{CornerRadius=new CornerRadius(compact?4:6),BorderThickness=new Thickness(1.5),BorderBrush=compact?new SolidColorBrush(Color.FromRgb(213,229,249)):Theme.Muted,Background=new SolidColorBrush(Color.FromArgb(18,150,187,235))});
        bool vertical=edge is "Left" or "Right";
        screen.Children.Add(new Border{Width=vertical?(compact?4:5):(compact?15:30),Height=vertical?(compact?12:23):(compact?4:5),CornerRadius=new CornerRadius(2.5),Background=new SolidColorBrush(compact?Color.FromRgb(157,213,255):Theme.Dark?Color.FromRgb(143,204,255):Color.FromRgb(55,119,200)),Margin=new Thickness(compact?3:5),HorizontalAlignment=edge=="Left"?HorizontalAlignment.Left:edge=="Right"?HorizontalAlignment.Right:HorizontalAlignment.Center,VerticalAlignment=edge=="Top"?VerticalAlignment.Top:edge=="Bottom"?VerticalAlignment.Bottom:VerticalAlignment.Center});return screen;
    }
}

