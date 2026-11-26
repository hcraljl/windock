using System.Windows.Media;
namespace SuperDock;
// 桌面组件文字颜色：全部组件共用同一支未冻结的画刷，切换颜色时改 Color 即可全组件即时生效。
internal static class WidgetPaint
{
    public static readonly SolidColorBrush Label = new(Color.FromRgb(255,255,255));
    public static void Apply(string hex)
    {
        try
        {
            if(ColorConverter.ConvertFromString((hex??"").Trim()) is Color color){Label.Color=color;return;}
        }
        catch(FormatException){}
        Label.Color=Colors.White;
    }
    // 组件背景的浅色底：跟随所选颜色，保持低透明度以不干扰桌面。
    public static Brush Tint(double alpha)=>new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha*255),255,255,255));
}
