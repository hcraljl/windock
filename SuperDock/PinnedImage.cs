using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;
// 贴图窗口：把截图或剪贴板图片贴到桌面并置顶显示。拖动移动、滚轮缩放、右键菜单（标注/复制/保存/置顶/关闭）。
sealed class PinnedImage : Window
{
    readonly MainWindow.Settings settings;
    readonly Image image;
    readonly Grid root=new();
    readonly AnnotationLayer layer=new();
    Border? toolbar;
    double zoomScale=1,zoomTarget=1;bool zoomAnimating;DateTime zoomLastFrame;double baseImgW=double.NaN,baseImgH=double.NaN;
    bool alwaysTop=true;
    readonly Rect? at;
    PinnedImage(BitmapSource source,MainWindow.Settings settings,Rect? at=null)
    {
        this.settings=settings;this.at=at;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 贴图";
        image=new Image{Source=source,Stretch=Stretch.Fill};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        // 浅蓝色光晕：柔和阴影 + 一圈淡蓝描边，在亮色背景下也能看到边界
        var frame=new Border{CornerRadius=new CornerRadius(7),BorderBrush=new SolidColorBrush(Color.FromArgb(200,125,200,255)),BorderThickness=new Thickness(1.5),Child=image,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=0,Opacity=.8,Color=Color.FromRgb(115,190,255)}};
        layer.Visibility=Visibility.Hidden;
        layer.MosaicProvider=rect=>{
            var source=(BitmapSource)image.Source;
            double k=source.PixelWidth/Math.Max(1,image.ActualWidth);
            return AnnotationLayer.Pixelate(source,new Rect(rect.X*k,rect.Y*k,rect.Width*k,rect.Height*k),layer.MosaicStrength);
        };
        root.Children.Add(frame);root.Children.Add(layer);
        Content=root;
        SizeToContent=SizeToContent.WidthAndHeight;
        Loaded+=(_,_)=>{
            var dpi=VisualTreeHelper.GetDpi(this);
            image.Width=source.PixelWidth/dpi.DpiScaleX;image.Height=source.PixelHeight/dpi.DpiScaleY;
            if(at!=null)
            {
                // 原位贴图：出现在指定屏幕位置（截图选区处），保证至少一部分在可视桌面上
                Left=at.Value.Left;Top=at.Value.Top;
                var vLeft=SystemParameters.VirtualScreenLeft;var vTop=SystemParameters.VirtualScreenTop;var vRight=vLeft+SystemParameters.VirtualScreenWidth;var vBottom=vTop+SystemParameters.VirtualScreenHeight;
                if(Left+ActualWidth<vLeft+40)Left=vLeft+8;else if(Left>vRight-40)Left=vRight-ActualWidth-8;
                if(Top+ActualHeight<vTop+40)Top=vTop+8;else if(Top>vBottom-40)Top=vBottom-ActualHeight-8;
                return;
            }
            if(DesktopIntegration.Cursor(out var cursor))
            {
                var work=SystemParameters.WorkArea;
                double x=cursor.X/dpi.DpiScaleX,y=cursor.Y/dpi.DpiScaleY;
                Left=Math.Clamp(x-image.Width/2,work.Left,Math.Max(work.Left,work.Right-image.Width));
                Top=Math.Clamp(y-image.Height/2,work.Top,Math.Max(work.Top,work.Bottom-image.Height));
            }
            else WindowStartupLocation=WindowStartupLocation.CenterScreen;
        };
        // 手动拖动（DragMove 在贴图不抢焦点的场景下不稳定）：捕获鼠标，按屏幕偏移移动窗口。
        bool dragging=false;Point dragOffset;
        MouseLeftButtonDown+=(_,e)=>{
            if(e.OriginalSource is DependencyObject src&&AnnotationToolbar.FromPopupContent(src))return; // 工具条弹层（如字号下拉）：不算拖动/双击
            if(e.ClickCount==2){Close();return;}
            if(e.ButtonState!=MouseButtonState.Pressed)return;
            var p=e.GetPosition(this);var screen=new Point(Left+p.X,Top+p.Y);
            dragging=true;dragOffset=new Point(screen.X-Left,screen.Y-Top);
            CaptureMouse();e.Handled=true;
        };
        MouseMove+=(_,e)=>{
            if(!dragging)return;
            var p=e.GetPosition(this);var screen=new Point(Left+p.X,Top+p.Y);
            Left=Math.Round(screen.X-dragOffset.X);Top=Math.Round(screen.Y-dragOffset.Y);
        };
        MouseLeftButtonUp+=(_,e)=>{if(dragging){dragging=false;ReleaseMouseCapture();}};
        MouseRightButtonUp+=(_,_)=>ShowMenu();
        PreviewMouseWheel+=(_,e)=>{Zoom(e.Delta>0?1.1:1/1.1);e.Handled=true;};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape&&layer.SelectedShape!=null){layer.Deselect();e.Handled=true;}else if(e.Key==Key.Escape)Close();};
        Loaded+=(_,_)=>{live.Add(this);EnsureHook();};
        Closed+=(_,_)=>{live.Remove(this);if(live.Count==0)Unhook();};
        Deactivated+=(_,_)=>{layer.Current=AnnotationLayer.Tool.None;if(toolbar!=null)toolbar.Visibility=Visibility.Collapsed;};
    }
    // 悬停滚轮缩放：Windows 默认把滚轮消息发给焦点窗口，贴图窗口不抢焦点，
    // 因此用 WH_MOUSE_LL 钩子捕获悬停在贴图上的滚轮事件并转为缩放（吃掉事件，避免滚动到背后的页面）。
    static readonly List<PinnedImage> live=[];
    static IntPtr hook;static HookProc hookProc=null!;
    delegate IntPtr HookProc(int code,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr mod,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wParam,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] struct MsllHookStruct{public PointApi pt;public uint mouseData;public uint flags;public uint time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Sequential)] struct PointApi{public int X,Y;}
    static void EnsureHook()
    {
        if(hook!=IntPtr.Zero)return;
        hookProc=(code,wParam,lParam)=>{
            const int WM_MOUSEWHEEL=0x020A;
            if(code>=0&&wParam==(IntPtr)WM_MOUSEWHEEL)
            {
                var info=(MsllHookStruct)Marshal.PtrToStructure(lParam,typeof(MsllHookStruct))!;
                var delta=(short)((info.mouseData>>16)&0xFFFF);
                var pt=new Point(info.pt.X,info.pt.Y);
                foreach(var window in live.ToList())
                {
                    try
                    {
                        if(!window.IsVisible)continue;
                        var tl=window.PointToScreen(new Point(0,0));
                        var br=window.PointToScreen(new Point(window.ActualWidth,window.ActualHeight));
                        if(pt.X>=tl.X&&pt.X<=br.X&&pt.Y>=tl.Y&&pt.Y<=br.Y)
                        {
                            var target=window;var factor=delta>0?1.1:1/1.1;
                            target.Dispatcher.BeginInvoke(new Action(()=>{try{target.Zoom(factor);}catch{}}));
                            return (IntPtr)1; // 吃掉滚轮，避免穿透到背后的页面
                        }
                    }
                    catch{}
                }
            }
            return CallNextHookEx(hook,code,wParam,lParam);
        };
        hook=SetWindowsHookEx(14,hookProc,IntPtr.Zero,0); // WH_MOUSE_LL
    }
    static void Unhook(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
    // 平滑缩放：滚轮只更新目标缩放，渲染帧间用指数趋近过渡，消除逐格跳动的顿挫感。
    void Zoom(double factor)
    {
        if(layer.Visibility==Visibility.Visible)layer.CommitTyping();
        if(double.IsNaN(baseImgW))
        {
            baseImgW=image.ActualWidth;baseImgH=image.ActualHeight;
            if(baseImgW<=0||baseImgH<=0)return;
        }
        var work=SystemParameters.WorkArea;
        double min=Math.Max(24/baseImgW,24/baseImgH);
        double max=Math.Max(min,Math.Min(work.Width*1.8/baseImgW,work.Height*2.4/baseImgH));
        double target=Math.Clamp(zoomTarget*factor,min,max);
        if(target==zoomTarget)return; // 已到目标（含边界钳制）
        zoomTarget=target;zoomLastFrame=DateTime.UtcNow;
        if(!zoomAnimating){zoomAnimating=true;CompositionTarget.Rendering+=OnZoomFrame;}
    }
    void OnZoomFrame(object? sender,EventArgs e)
    {
        if(!IsVisible){zoomAnimating=false;CompositionTarget.Rendering-=OnZoomFrame;return;} // 窗口已关闭
        var now=DateTime.UtcNow;
        double dt=Math.Min(.1,Math.Max(.001,(now-zoomLastFrame).TotalSeconds));
        zoomLastFrame=now;
        zoomScale+=(zoomTarget-zoomScale)*(1-Math.Exp(-dt*13));
        if(Math.Abs(zoomTarget-zoomScale)<=zoomTarget*.002)zoomScale=zoomTarget;
        ApplyZoom();
        if(zoomScale==zoomTarget){zoomAnimating=false;CompositionTarget.Rendering-=OnZoomFrame;}
    }
    void ApplyZoom()
    {
        double cx=Left+ActualWidth/2,cy=Top+ActualHeight/2; // 以窗口当前中心为锚点缩放
        SizeToContent=SizeToContent.Manual;
        image.Width=baseImgW*zoomScale;image.Height=baseImgH*zoomScale;
        layer.Width=image.Width;layer.Height=image.Height;
        layer.WatermarkBounds=new Rect(0,0,image.Width,image.Height);
        Width=image.Width;Height=image.Height;
        UpdateLayout();
        Left=cx-ActualWidth/2;Top=cy-ActualHeight/2;
        ClampIntoWork();
    }
    void ClampIntoWork()
    {
        var work=SystemParameters.WorkArea;
        Left=Math.Clamp(Left,work.Left-Math.Max(0,ActualWidth-work.Width),Math.Max(work.Left,work.Right-ActualWidth*.4));
        Top=Math.Clamp(Top,work.Top,Math.Max(work.Top,work.Bottom-Math.Min(ActualHeight,work.Height*.9)));
    }
    void ShowMenu()
    {
        var menu=new ContextMenu{PlacementTarget=this,Placement=PlacementMode.MousePoint};
        MenuItem Item(string header,RoutedEventHandler handler,bool enabled=true,bool check=false)
        {
            var item=new MenuItem{Header=header,IsEnabled=enabled,IsCheckable=check,IsChecked=check&&alwaysTop};
            item.Click+=handler;return item;
        }
        menu.Items.Add(Item("标注",(s,_)=>ToggleAnnotate()));
        menu.Items.Add(Item("复制",(s,_)=>{try{System.Windows.Clipboard.SetImage(Compose());}catch(System.Runtime.InteropServices.COMException){}}));
        menu.Items.Add(Item("保存为 PNG",(s,_)=>SaveImage()));
        menu.Items.Add(Item("窗口置顶",(s,_)=>{alwaysTop=!alwaysTop;Topmost=alwaysTop;},true,true));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("关闭",(s,_)=>Close()));
        menu.IsOpen=true;
    }
    void ToggleAnnotate()
    {
        if(layer.Visibility==Visibility.Visible)
        {
            layer.CommitTyping();layer.Visibility=Visibility.Hidden;layer.Current=AnnotationLayer.Tool.None;toolbar?.Visibility=Visibility.Collapsed;
            return;
        }
        layer.Visibility=Visibility.Visible;layer.Width=image.ActualWidth;layer.Height=image.ActualHeight;
        layer.WatermarkBounds=new Rect(0,0,image.ActualWidth,image.ActualHeight); // 自动水印铺满整张图
        if(toolbar==null)
        {
            var done=AnnotationToolbar.ActionChip("","标注完成",()=>ToggleAnnotate());
            toolbar=new Border{CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(Color.FromArgb(246,252,253,255)),BorderBrush=new SolidColorBrush(Color.FromArgb(40,36,54,77)),BorderThickness=new Thickness(1),Padding=new Thickness(8,6,8,6),Child=AnnotationToolbar.Build(layer,this,OnGripDrag,0,done),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.3}};
            var host=new Canvas{Width=image.ActualWidth,Height=image.ActualHeight};
            Canvas.SetLeft(toolbar,4);Canvas.SetTop(toolbar,4);
            host.Children.Add(toolbar);
            root.Children.Add(host);
        }
        toolbar.Visibility=Visibility.Visible;
    }
    // 手柄拖动：移动标注工具条（限制在图片范围内）。
    void OnGripDrag(Vector v)
    {
        if(toolbar==null||toolbar.Parent is not Canvas hostCanvas)return;
        double l=Canvas.GetLeft(toolbar)+v.X,t=Canvas.GetTop(toolbar)+v.Y;
        l=Math.Clamp(l,2,Math.Max(2,image.ActualWidth-toolbar.ActualWidth-2));
        t=Math.Clamp(t,2,Math.Max(2,image.ActualHeight-toolbar.ActualHeight-2));
        Canvas.SetLeft(toolbar,l);Canvas.SetTop(toolbar,t);
    }
    internal void OnToolbarLayoutChanged()
    {
        if(toolbar!=null&&toolbar.Parent is Canvas hc)
        {
            Canvas.SetLeft(toolbar,Math.Clamp(Canvas.GetLeft(toolbar),2,Math.Max(2,image.ActualWidth-toolbar.ActualWidth-2)));
            Canvas.SetTop(toolbar,Math.Clamp(Canvas.GetTop(toolbar),2,Math.Max(2,image.ActualHeight-toolbar.ActualHeight-2)));
        }
    }
    static Button SmallButton(string label,string tip,Action action)
    {
        var button=new Button{Content=label,Style=(Style)Application.Current.FindResource("GlassButton"),Height=26,Padding=new Thickness(10,2,10,2),ToolTip=tip,Cursor=Cursors.Hand,Margin=new Thickness(2,0,0,0),Background=GlassWindow.Surface,FontSize=12,Foreground=Theme.Text};
        button.Click+=(_,_)=>action();
        return button;
    }
    void SaveImage()
    {
        var source=Compose();
        var thread=new System.Threading.Thread(()=>
        {
            try
            {
                var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="windock-pin-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"};
                var pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if(!string.IsNullOrEmpty(pictures)&&Directory.Exists(pictures))dialog.InitialDirectory=pictures;
                if(dialog.ShowDialog()!=true)return;
                using var stream=File.Create(dialog.FileName);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));encoder.Save(stream);
                ActivityStore.Write("贴图",message:"已保存 "+dialog.FileName);
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Dispatcher.BeginInvoke(new Action(()=>GlassDialog.Show("保存失败："+ex.Message)));
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
    // 合成当前显示内容：原图 + 标注层，按图片物理像素输出（复制/保存共用，水印等标注都包含在内）。
    BitmapSource Compose()
    {
        var source=(BitmapSource)image.Source;
        if(!layer.HasDrawings)return source;
        double k=source.PixelWidth/Math.Max(1,image.ActualWidth);
        layer.SuppressOverlay=true; // 选择框不进入成图
        var rtb=new RenderTargetBitmap(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();
        using(var ctx=visual.RenderOpen())
        {
            ctx.DrawImage(source,new Rect(0,0,source.PixelWidth,source.PixelHeight));
            ctx.PushTransform(new ScaleTransform(k,k));
            ctx.DrawRectangle(new VisualBrush(layer),null,new Rect(0,0,layer.Width,layer.Height));
            ctx.Pop();
        }
        rtb.Render(visual);rtb.Freeze();
        layer.SuppressOverlay=false;
        return rtb;
    }
    internal static void Pin(BitmapSource source,MainWindow.Settings settings,Rect? at=null)
    {
        if(source.CanFreeze&&!source.IsFrozen)source.Freeze();
        new PinnedImage(source,settings,at).Show();
    }
}
