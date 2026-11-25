using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace SuperDock;
internal sealed class WindowPreview : Window
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] struct Properties
    {
        public uint Flags; public Rect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref Properties properties);
    [DllImport("dwmapi.dll")] static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out Size size);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    readonly List<(IntPtr Handle, Border Host)> thumbnails = [];
    public WindowPreview(Window owner, List<AppWindow> windows)
    {
        Owner = owner; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        GlassWindow.Prepare(this);Background = Brushes.Transparent; SizeToContent = SizeToContent.WidthAndHeight;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        var scroll = new ScrollViewer { Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxWidth = Math.Max(240,SystemParameters.WorkArea.Width-32) };
        Content = new Border { Background = GlassWindow.Surface, BorderBrush = (Brush)FindResource("GlassStroke"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(2), Child = scroll };
        foreach (var window in windows)
        {
            var card = new StackPanel { Width = Math.Min(340,SystemParameters.WorkArea.Width-60), Margin = new Thickness(5) };
            card.Children.Add(new TextBlock { Text = window.Title, Foreground = Theme.Text, FontSize=12,FontWeight=FontWeights.Medium,TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5,3,5,7),ToolTip=window.Title });
            var host = new Border { Height = 205, Background = new SolidColorBrush(Color.FromArgb(24,0,0,0)),CornerRadius=new CornerRadius(10), Child = new TextBlock { Text = "窗口预览不可用", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            card.Children.Add(host); row.Children.Add(card);
            card.MouseLeftButtonUp += (_, _) => { RunningWindows.Activate(window.Handle); Close(); };
            thumbnails.Add((window.Handle, host));
        }
        SourceInitialized += (_, _) => Register();
        LayoutUpdated += (_, _) => Update();
        Closed += (_, _) => { foreach (var t in registrations) DwmUnregisterThumbnail(t.Thumbnail); registrations.Clear(); };
    }
    readonly List<(IntPtr Thumbnail, Border Host)> registrations = [];
    void Register()
    {
        var destination = new WindowInteropHelper(this).Handle;
        foreach (var t in thumbnails)
            if (DwmRegisterThumbnail(destination, t.Handle, out var thumbnail) == 0) { registrations.Add((thumbnail,t.Host)); t.Host.Child = null;if(DwmQueryThumbnailSourceSize(thumbnail,out var source)==0&&source.Width>0&&source.Height>0){double width=Math.Min(340,SystemParameters.WorkArea.Width-60);t.Host.Height=Math.Clamp(width*source.Height/source.Width,110,Math.Min(280,SystemParameters.WorkArea.Height-140));} }
    }
    void Update()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        foreach (var t in registrations)
        {
            if (t.Host.ActualWidth <= 0) continue;
            var p = t.Host.TranslatePoint(new Point(), this);
            double w = t.Host.ActualWidth, h = t.Host.ActualHeight;
            if (DwmQueryThumbnailSourceSize(t.Thumbnail,out var size) == 0 && size.Width > 0 && size.Height > 0)
            {
                double ratio = Math.Min(w/size.Width,h/size.Height); double nw=size.Width*ratio, nh=size.Height*ratio; p.X+=(w-nw)/2; p.Y+=(h-nh)/2; w=nw; h=nh;
            }
            var properties = new Properties { Flags = 1|4|8|16, Destination = new Rect { Left=(int)(p.X*dpi.DpiScaleX), Top=(int)(p.Y*dpi.DpiScaleY), Right=(int)((p.X+w)*dpi.DpiScaleX), Bottom=(int)((p.Y+h)*dpi.DpiScaleY) }, Opacity=255, Visible=true, ClientOnly=false };
            DwmUpdateThumbnailProperties(t.Thumbnail,ref properties);
        }
    }
}




