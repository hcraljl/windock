using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace SuperDock;
// 截图入口：F1 框选截图（可涂鸦标注、复制、保存、贴图），F3 把截图或剪贴板图片贴到桌面置顶显示。
internal static class Screenshot
{
    internal static CaptureWindow? Capturing;
    public static void Begin(MainWindow.Settings settings,MainWindow owner)
    {
        if(Capturing!=null){Capturing.Activate();return;}
        var window=new CaptureWindow(owner,settings);Capturing=window;
        window.Closed+=(_,_)=>{if(Capturing==window)Capturing=null;};
        window.Start();
    }
    public static void PinFromClipboard(MainWindow.Settings settings)
    {
        if(Capturing is {} window){window.PinCurrent();return;} // 截图进行中：F3 直接钉当前选区
        if(!System.Windows.Clipboard.ContainsImage()){GlassDialog.Show("剪贴板中没有图片。先按截图快捷键框选，或复制一张图片再按贴图键。");return;}
        PinnedImage.Pin(System.Windows.Clipboard.GetImage(),settings);
    }
}
sealed class CaptureWindow : Window
{
    readonly MainWindow owner;readonly MainWindow.Settings settings;
    readonly BitmapSource screen;readonly double dpiX,dpiY;readonly Rect area;
    readonly Grid root=new();
    readonly Image frame=new();
    readonly AnnotationLayer layer=new();
    readonly Canvas chrome=new();
    readonly Border dim=new(){Background=new SolidColorBrush(Color.FromArgb(92,10,14,20))};
    readonly Rectangle selectionBorder=new(){Stroke=new SolidColorBrush(Color.FromRgb(111,207,255)),StrokeThickness=1.4,Visibility=Visibility.Hidden};
    readonly TextBlock sizeLabel=new(){Foreground=Brushes.White,FontSize=12,FontWeight=FontWeights.SemiBold,Padding=new Thickness(6,3,6,3),Visibility=Visibility.Hidden};
    Border? toolbar;
    FrameworkElement? firstHint;
    Point start;bool selecting,selected;Rect selection;
    public CaptureWindow(MainWindow owner,MainWindow.Settings settings)
    {
        this.owner=owner;this.settings=settings;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=true;Background=Brushes.Black;Cursor=Cursors.Cross;Title="windock · 截图";
        frame.Stretch=Stretch.Fill;RenderOptions.SetBitmapScalingMode(frame,BitmapScalingMode.NearestNeighbor);
        chrome.Children.Add(dim);chrome.Children.Add(selectionBorder);chrome.Children.Add(sizeLabel);
        var hint=new TextBlock{Text="拖动框选截图区域 · 完成后可标注、复制、保存或贴图 · 右键或 Esc 取消",FontSize=14,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(140,10,14,20)),Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,24,0,0)};
        chrome.Children.Add(hint);firstHint=hint;
        root.Children.Add(frame);root.Children.Add(layer);root.Children.Add(chrome);
        layer.Visibility=Visibility.Hidden; // 完成框选前不响应标注
        Content=root;
        screen=CaptureScreen(out var capturedArea,out dpiX,out dpiY);area=capturedArea;
        frame.Source=screen;
        WindowStartupLocation=WindowStartupLocation.Manual;
        SourceInitialized+=(_,_)=>{var dpi=VisualTreeHelper.GetDpi(this);frame.Width=area.Width/dpi.DpiScaleX;frame.Height=area.Height/dpi.DpiScaleY;Width=area.Width/dpi.DpiScaleX;Height=area.Height/dpi.DpiScaleY;DesktopIntegration.MovePhysical(this,area.Left,area.Top);};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Enter&&selected){CopySelection();Close();e.Handled=true;}};
        PreviewKeyDown+=(_,e)=>{if(layer.IsTyping)return;if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Enter&&selected){CopySelection();Close();e.Handled=true;}};
        PreviewMouseLeftButtonDown+=(_,e)=>{if(selected&&!InSelection(e.GetPosition(root))&&!OverChrome(e,chrome))BeginSelect(e.GetPosition(root));};
        // 右键取消：不进入框选、不弹菜单，直接退出截图状态。
        PreviewMouseRightButtonDown+=(_,e)=>{e.Handled=true;Close();};
        MouseRightButtonDown+=(_,e)=>e.Handled=true;
        MouseRightButtonUp+=(_,e)=>e.Handled=true;
        ContextMenu=null;
        MouseLeftButtonDown+=(_,e)=>{if(!selected){BeginSelect(e.GetPosition(root));e.Handled=true;}};
        MouseMove+=(_,e)=>{if(selecting)UpdateSelection(e.GetPosition(root));else if(!IsActive)Activate();};
        MouseLeftButtonUp+=(_,e)=>{if(selecting)FinishSelection();};
    }
    static bool OverChrome(MouseEventArgs e,DependencyObject chrome)=>e.OriginalSource is DependencyObject d&&IsDescendantOf(d,chrome);
    static bool IsDescendantOf(DependencyObject node,DependencyObject ancestor){while(node!=null){if(ReferenceEquals(node,ancestor))return true;node=System.Windows.Media.VisualTreeHelper.GetParent(node);}return false;}
    bool InSelection(Point point)=>selection.Contains(point);
    void BeginSelect(Point point)
    {
        CommitTyping();layer.ClearAll();toolbar?.Visibility=Visibility.Collapsed;if(firstHint!=null){chrome.Children.Remove(firstHint);firstHint=null;}selected=false;selecting=true;start=point;
        layer.Visibility=Visibility.Hidden;UpdateSelection(point);
    }
    void UpdateSelection(Point point)
    {
        double x=Math.Min(start.X,point.X),y=Math.Min(start.Y,point.Y),w=Math.Abs(point.X-start.X),h=Math.Abs(point.Y-start.Y);
        selection=new Rect(x,y,w,h);
        var full=new RectangleGeometry(new Rect(0,0,Width,Height));
        dim.Clip=w>1&&h>1?new CombinedGeometry(GeometryCombineMode.Exclude,full,new RectangleGeometry(selection)):full;
        selectionBorder.Visibility=w>1?Visibility.Visible:Visibility.Hidden;
        Canvas.SetLeft(selectionBorder,x);Canvas.SetTop(selectionBorder,y);selectionBorder.Width=w;selectionBorder.Height=h;
        sizeLabel.Text=$"{(int)(w*dpiX)} × {(int)(h*dpiY)}";
        sizeLabel.Visibility=Visibility.Visible;
        Canvas.SetLeft(sizeLabel,x);Canvas.SetTop(sizeLabel,Math.Max(0,y-26));
    }
    void FinishSelection()
    {
        selecting=false;
        if(selection.Width<8||selection.Height<8){dim.Clip=null;selectionBorder.Visibility=Visibility.Hidden;sizeLabel.Visibility=Visibility.Hidden;selected=false;return;}
        selected=true;
        layer.Visibility=Visibility.Visible;
        ShowToolbar();
    }
    void ShowToolbar()
    {
        if(toolbar==null)
        {
            var copy=ToolButton("复制","复制到剪贴板","\uE8C8",CopySelection);
            var save=ToolButton("保存","保存为 PNG 文件","\uE74E",SaveSelection);
            var pin=ToolButton("贴图","贴到桌面置顶显示","\uE8A7",PinCurrent);
            var cancel=ToolButton("取消","退出本次截图","\uE711",Close);
            toolbar=new Border{CornerRadius=new CornerRadius(12),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(8,6,8,6),Child=AnnotationToolbar.Build(layer,this,copy,save,pin,cancel),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=3,Opacity=.25},Cursor=Cursors.Hand};
            chrome.Children.Add(toolbar);
        }
        PlaceToolbar();toolbar.Visibility=Visibility.Visible;
    }
    static Button ToolButton(string label,string tip,string glyph,Action action)
    {
        var button=new Button
        {
            Content=new StackPanel{Orientation=Orientation.Horizontal,Children={new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=13,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center},new TextBlock{Text=label,FontSize=12,Foreground=Theme.Text,Margin=new Thickness(5,0,0,0),VerticalAlignment=VerticalAlignment.Center}}},
            Style=(Style)Application.Current.FindResource("GlassButton"),Height=30,Padding=new Thickness(10,2,10,2),Cursor=Cursors.Hand,Margin=new Thickness(2,0,0,0),Background=GlassWindow.Surface,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,ToolTip=tip
        };
        button.Click+=(_,_)=>action();
        return button;
    }
    void PlaceToolbar()
    {
        if(toolbar==null)return;
        toolbar.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double tw=toolbar.DesiredSize.Width,th=toolbar.DesiredSize.Height;
        double x=selection.Right-tw,y=selection.Bottom+10;
        if(y+th>Height-8)y=selection.Top-th-10;
        if(y<8)y=Math.Max(8,Height-th-8);
        if(x<8)x=8;if(x+tw>Width-8)x=Width-tw-8;
        Canvas.SetLeft(toolbar,Math.Max(8,x));Canvas.SetTop(toolbar,Math.Max(8,y));
    }
    void CommitTyping()=>layer.CommitTyping();
    // 合成选区结果：冻结的屏幕帧 + 标注，按物理像素输出。
    BitmapSource Compose()
    {
        var crop=new Int32Rect((int)Math.Round(selection.Left*dpiX),(int)Math.Round(selection.Top*dpiY),(int)Math.Round(selection.Width*dpiX),(int)Math.Round(selection.Height*dpiY));
        crop.Width=Math.Max(1,Math.Min(crop.Width,screen.PixelWidth-crop.X));crop.Height=Math.Max(1,Math.Min(crop.Height,screen.PixelHeight-crop.Y));
        var rtb=new RenderTargetBitmap(crop.Width,crop.Height,96,96,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();
        using(var ctx=visual.RenderOpen())
        {
            ctx.DrawImage(new CroppedBitmap(screen,crop),new Rect(0,0,crop.Width,crop.Height));
            if(layer.HasDrawings)
            {
                ctx.PushTransform(new ScaleTransform(dpiX,dpiY));ctx.PushTransform(new TranslateTransform(-selection.Left,-selection.Top));
                var brush=new VisualBrush(layer);ctx.DrawRectangle(brush,null,new Rect(0,0,frame.Width,frame.Height));
                ctx.Pop();ctx.Pop();
            }
        }
        rtb.Render(visual);rtb.Freeze();return rtb;
    }
    void CopySelection(){try{System.Windows.Clipboard.SetImage(Compose());}catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException){GlassDialog.Show("复制失败："+ex.Message);}}
    // 保存放到独立 STA 线程：实测在部分机器上 Vista 文件对话框可能长时间不显示却阻塞 UI 线程（首个 ~43s，失败 ~30s），导致覆盖层假死。
    void SaveSelection()
    {
        var image=Compose();
        var thread=new System.Threading.Thread(()=>
        {
            try
            {
                var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="windock-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"};
                var pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if(!string.IsNullOrEmpty(pictures)&&Directory.Exists(pictures))dialog.InitialDirectory=pictures;
                if(dialog.ShowDialog()!=true)return;
                using var stream=File.Create(dialog.FileName);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
                ActivityStore.Write("截图",message:"已保存 "+dialog.FileName);
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Dispatcher.BeginInvoke(new Action(()=>GlassDialog.Show("保存失败："+ex.Message)));
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
    public void PinCurrent()
    {
        try{var image=Compose();Close();PinnedImage.Pin(image,settings);}
        catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException){GlassDialog.Show("贴图失败："+ex.Message);}
    }
    // 截取光标所在屏幕的物理像素（GDI BitBlt，避免引入 WinForms）。
    static BitmapSource CaptureScreen(out Rect area,out double dpiX,out double dpiY)
    {
        dpiX=1;dpiY=1;
        if(!DesktopIntegration.Cursor(out var cursor))cursor=new(0,0);
        var monitors=DesktopIntegration.Monitors();
        var first=monitors.FirstOrDefault();
        var monitor=monitors.FirstOrDefault(m=>m.Area.Contains(cursor));
        if(monitor.Equals(default))monitor=first;
        area=monitor.Area;
        int width=(int)area.Width,height=(int)area.Height;
        var hdcScreen=GetDC(IntPtr.Zero);
        var hdcMem=CreateCompatibleDC(hdcScreen);
        var info=new BitmapInfo{Size=40,Width=width,Height=-height,Planes=1,BitCount=32,Compression=0};
        var dib=CreateDIBSection(hdcMem,ref info,0,out var bits,IntPtr.Zero,0);
        var previous=SelectObject(hdcMem,dib);
        BitBlt(hdcMem,0,0,width,height,hdcScreen,(int)area.Left,(int)area.Top,0x00CC0020|0x40000000); // SRCCOPY|CAPTUREBLT
        var pixels=new byte[width*height*4];
        Marshal.Copy(bits,pixels,0,pixels.Length);
        SelectObject(hdcMem,previous);DeleteObject(dib);DeleteDC(hdcMem);ReleaseDC(IntPtr.Zero,hdcScreen);
        var source=BitmapSource.Create(width,height,96,96,PixelFormats.Bgr32,null,pixels,width*4);
        source.Freeze();return source;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfo{public int Size,Width,Height;public short Planes,BitCount;public int Compression,SizeImage,XPelsPerMeter,YPelsPerMeter,ColorsUsed,ColorsImportant;}
    [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
    [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr hdc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr holder,uint offset);
    [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr dst,int x,int y,int w,int h,IntPtr src,int sx,int sy,int rop);
    internal void Start()
    {
        owner.Dispatcher.BeginInvoke(new Action(async()=>{
            owner.SuppressForCapture();
            await Task.Delay(150); // 等 DWM 合成隐藏后的画面
            Show();
        }));
    }
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        owner.Dispatcher.BeginInvoke(new Action(()=>owner.RestoreFromCapture()));
    }
}
