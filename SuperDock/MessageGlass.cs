using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
namespace SuperDock;
internal static class MessageGlass
{
    static Color Tint(string client)=>client switch{"微信"=>Color.FromRgb(92,193,153),"QQ"=>Color.FromRgb(110,166,235),"钉钉"=>Color.FromRgb(85,169,242),"飞书"=>Color.FromRgb(139,126,223),_=>Color.FromRgb(106,168,210)};
    public static Brush Surface(string client,bool popup=false)
    {
        var tint=Tint(client);var brush=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        brush.GradientStops.Add(new GradientStop(Theme.Dark?Color.FromArgb(popup?(byte)92:(byte)42,72,82,105):Color.FromArgb(popup?(byte)118:(byte)75,255,255,255),0));
        brush.GradientStops.Add(new GradientStop(Theme.Dark?Color.FromArgb(popup?(byte)106:(byte)28,36,43,62):Color.FromArgb(popup?(byte)98:(byte)33,235,242,252),.55));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(popup?(byte)62:(byte)24,tint.R,tint.G,tint.B),1));return brush;
    }
    public static Brush PopupSurface(string client,double opacity)
    {
        opacity=double.IsFinite(opacity)?Math.Clamp(opacity,.2,1):.65;
        var tint=Tint(client);byte alpha=(byte)Math.Round(opacity*255);
        var brush=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        brush.GradientStops.Add(new GradientStop(Theme.Dark?Color.FromArgb(alpha,44,53,69):Color.FromArgb(alpha,247,251,255),0));
        brush.GradientStops.Add(new GradientStop(Theme.Dark?Color.FromArgb(alpha,30,39,54):Color.FromArgb(alpha,230,240,249),.6));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,(byte)((tint.R+ (Theme.Dark?32:235))/2),(byte)((tint.G+(Theme.Dark?42:245))/2),(byte)((tint.B+(Theme.Dark?56:250))/2)),1));
        return brush;
    }    public static Brush Rim=>new LinearGradientBrush(Color.FromArgb(135,255,255,255),Color.FromArgb(24,174,200,230),70);
    public static Border Card(UIElement child,string client)
    {
        var border=new Border{Child=child,Background=Surface(client),CornerRadius=new CornerRadius(20),BorderBrush=Rim,BorderThickness=new Thickness(.7),Padding=new Thickness(16),Margin=new Thickness(5,6,5,6),Effect=new DropShadowEffect{Color=Color.FromRgb(27,45,75),BlurRadius=18,ShadowDepth=3,Opacity=Theme.Dark?.18:.07}};
        var lift=new TranslateTransform();border.RenderTransform=lift;
        border.MouseEnter+=(_,_)=>lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(-1,TimeSpan.FromMilliseconds(130)));
        border.MouseLeave+=(_,_)=>lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(160)));return border;
    }
}
