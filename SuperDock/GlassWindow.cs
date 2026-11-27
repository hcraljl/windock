using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace SuperDock;
internal static class GlassWindow
{
    const double Radius=8; // Match DWMWCP_ROUND; one compositor owns the entire silhouette.
    // 自定义圆角的窗口（如设置工作台）：DWM 系统圆角只有固定 8px，
    // 改用分层窗口 + 内容 Border 的圆角绘制，DWM 侧改为 DONOTROUND 避免二次裁切。
    static readonly ConditionalWeakTable<Window,object?> CustomRadii=new();
    static double RadiusOf(Window window)=>CustomRadii.TryGetValue(window,out var value)&&value is double radius?radius:Radius;
    [StructLayout(LayoutKind.Sequential)] struct Margins{public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins margins);
    [DllImport("user32.dll")]static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    static void UpdateCorners(Window window)
    {
        double radius=RadiusOf(window);
        if(window.Content is System.Windows.Controls.Border border){border.CornerRadius=new CornerRadius(radius);RoundClip.SetRadius(border,radius);}
    }
    public static void Prepare(Window window,double? radius=null)
    {
        window.Icon=Branding.Logo;
        if(radius is { } custom){CustomRadii.Add(window,custom);window.AllowsTransparency=true;}
        else window.AllowsTransparency=false;
        window.Background=Brushes.Transparent;
        WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(4),GlassFrameThickness=new Thickness(-1),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        window.SizeChanged+=(_,_)=>UpdateCorners(window);window.DpiChanged+=(_,_)=>window.Dispatcher.BeginInvoke(new Action(()=>Refresh(window)));
        window.SourceInitialized+=(_,_)=>Refresh(window);window.Loaded+=(_,_)=>UpdateCorners(window);
    }
    public static void Refresh(Window window)
    {
        UpdateCorners(window);
        if(CustomRadii.TryGetValue(window,out _))return; // 分层窗口：圆角由内容 Border 绘制，DWM 背景/系统圆角不适用
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
        if(HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)target.BackgroundColor=Colors.Transparent;
        // Accent acrylic is rendered as a separate rectangle and can bleed outside SetWindowRgn.
        // Clear both legacy accent and manual regions; DWM clips its own backdrop and border together.
        DesktopIntegration.Blur(window,false);SetWindowRgn(handle,IntPtr.Zero,true);
        int dark=Theme.Dark?1:0,backdrop=3,corner=2,noBorder=unchecked((int)0xFFFFFFFE);
        DwmSetWindowAttribute(handle,20,ref dark,4);int material=DwmSetWindowAttribute(handle,38,ref backdrop,4);
        DwmSetWindowAttribute(handle,33,ref corner,4);DwmSetWindowAttribute(handle,34,ref noBorder,4);
        var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};DwmExtendFrameIntoClientArea(handle,ref margins);UpdateCorners(window);
        if(material<0&&window.Content is System.Windows.Controls.Border fallback)fallback.Background=Theme.Fill;
    }
    // Keep a theme-owned tint over the system material so a dark desktop or an
    // inactive acrylic fallback cannot turn a light settings window into gray glass.
    public static Brush Surface=>new LinearGradientBrush(Theme.Dark?Color.FromArgb(72,32,37,47):Color.FromArgb(156,252,253,255),Theme.Dark?Color.FromArgb(90,25,30,39):Color.FromArgb(138,242,247,255),45);
}

