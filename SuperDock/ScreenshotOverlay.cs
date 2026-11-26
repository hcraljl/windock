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
        if(Capturing is {} current)
        {
            if(current.IsLoaded&&current.IsVisible){current.Activate();return;}
            // 残留的隐藏/未显示窗口：丢弃后重新开始，避免 F1 看似无反应
            Capturing=null;try{current.Close();}catch{}
        }
        var window=new CaptureWindow(owner,settings,translate:false);Capturing=window;
        window.Closed+=(_,_)=>{if(Capturing==window)Capturing=null;};
        window.Start();
    }
    public static void Translate(MainWindow.Settings settings,MainWindow owner)
    {
        if(Capturing is {} current)
        {
            if(current.IsLoaded&&current.IsVisible){current.Activate();return;}
            Capturing=null;try{current.Close();}catch{}
        }
        var window=new CaptureWindow(owner,settings,translate:true);Capturing=window;
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
    readonly MainWindow owner;readonly MainWindow.Settings settings;readonly bool translateMode;
    readonly BitmapSource screen;double dpiX=1,dpiY=1; // 物理像素↔逻辑坐标换算，SourceInitialized 后按窗口实际 DPI 重设
    readonly Rect area;
    readonly Grid root=new();
    readonly Image frame=new();
    readonly AnnotationLayer layer=new();
    readonly Canvas chrome=new();
    readonly Border dim=new(){Background=new SolidColorBrush(Color.FromArgb(118,10,14,20))};
    readonly List<Ellipse> handles=new();
    readonly Rectangle selectionBorder=new(){Stroke=new SolidColorBrush(Color.FromRgb(111,207,255)),StrokeThickness=1.4,Visibility=Visibility.Hidden};
    readonly TextBlock sizeLabel=new(){Foreground=Brushes.White,FontSize=12,FontWeight=FontWeights.SemiBold,Background=new SolidColorBrush(Color.FromArgb(220,34,37,41)),Padding=new Thickness(7,3,7,3),Visibility=Visibility.Hidden};
    Border? toolbar;
    FrameworkElement? firstHint;
    Point start;bool selecting,selected;Rect selection;
    public CaptureWindow(MainWindow owner,MainWindow.Settings settings,bool translate=false)
    {
        this.owner=owner;this.settings=settings;translateMode=translate;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=true;Background=Brushes.Black;Cursor=Cursors.Cross;Title="windock · 截图";
        frame.Stretch=Stretch.Fill;RenderOptions.SetBitmapScalingMode(frame,BitmapScalingMode.NearestNeighbor);
        dim.IsHitTestVisible=false;selectionBorder.IsHitTestVisible=false;sizeLabel.IsHitTestVisible=false;
        chrome.Children.Add(dim);chrome.Children.Add(selectionBorder);chrome.Children.Add(sizeLabel);
        var hint=new TextBlock{Text=translateMode?"拖动框选要翻译的文字 · 框选后自动识别并中英互译 · 右键或 Esc 取消":"拖动框选截图区域 · 完成后可标注、复制、保存或贴图 · 右键或 Esc 取消",FontSize=14,Foreground=Brushes.White,Background=new SolidColorBrush(Color.FromArgb(140,10,14,20)),Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,24,0,0)};
        hint.IsHitTestVisible=false;chrome.Children.Add(hint);firstHint=hint;
        // 钉钉式选区手柄：四角 + 四边中点，白色圆点蓝描边（拖边缘即可调整大小，手柄仅作视觉指示）。
        for(int i=0;i<8;i++)
        {
            var handle=new Ellipse{Width=9,Height=9,Fill=Brushes.White,Stroke=new SolidColorBrush(Color.FromRgb(35,125,245)),StrokeThickness=1.4,Visibility=Visibility.Hidden,IsHitTestVisible=false};
            handles.Add(handle);chrome.Children.Add(handle);
        }
        root.Children.Add(frame);root.Children.Add(layer);root.Children.Add(chrome);
        layer.Visibility=Visibility.Hidden; // 完成框选前不响应标注
        layer.MosaicProvider=rect=>AnnotationLayer.Pixelate(screen,new Rect(rect.Left*dpiX,rect.Top*dpiY,rect.Width*dpiX,rect.Height*dpiY),layer.MosaicStrength);
        Content=root;
        // 截图支持跨屏：覆盖层与冻结帧覆盖整个虚拟桌面（所有显示器拼接）。
        screen=CaptureScreen(out var capturedArea,out dpiX,out dpiY);area=capturedArea;
        frame.Source=screen;
        WindowStartupLocation=WindowStartupLocation.Manual;
        SourceInitialized+=(_,_)=>{DesktopIntegration.MovePhysical(this,area.Left,area.Top);
            var dpi=VisualTreeHelper.GetDpi(this);
            // 关键：裁剪/马赛克/尺寸标注都按窗口实际 DPI 换算（否则在 125%/150% 缩放下贴图会偏移、尺寸不符）
            dpiX=dpi.DpiScaleX;dpiY=dpi.DpiScaleY;
            frame.Width=area.Width/dpi.DpiScaleX;frame.Height=area.Height/dpi.DpiScaleY;Width=area.Width/dpi.DpiScaleX;Height=area.Height/dpi.DpiScaleY;
            // 遮罩是 Canvas 内的 Border，必须显式设定尺寸（Canvas 不拉伸子元素），否则永远不会渲染。
            dim.Width=Width;dim.Height=Height;};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Enter&&selected&&!translateMode){CopySelection();e.Handled=true;}};
        PreviewKeyDown+=(_,e)=>{if(layer.IsTyping)return;if(e.Key==Key.Escape&&layer.SelectedShape!=null){layer.Deselect();e.Handled=true;}else if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Enter&&selected&&!translateMode){CopySelection();e.Handled=true;}};
        // 选区外点击：无标注工具时重新框选；工具激活时忽略（避免误触清空所有标注/重置截图），也不在选区外绘制
        PreviewMouseLeftButtonDown+=(_,e)=>{var p=e.GetPosition(root);if(selected&&!InSelection(p)&&!OverChrome(e,chrome)){if(layer.Current==AnnotationLayer.Tool.None)BeginSelect(p);e.Handled=true;}};
        // 选区二次调整与整体拖动：靠近边缘/角拖动可拉大拉小；未选标注工具时按住选区内部可整框拖动（默认提供，不单独列按钮）。
        PreviewMouseLeftButtonDown+=(_,e)=>{if(!selected||selecting||OverChrome(e,chrome))return;var p=e.GetPosition(root);
            var edge=EdgeHit(p);
            if(edge!=ResizeEdge.None){StartResize(edge,p);e.Handled=true;return;}
            if(layer.Current==AnnotationLayer.Tool.None&&InSelection(p)&&selection.Width>0&&layer.ShapeAt(p)==null){StartMove(p);e.Handled=true;}};
        // 右键取消：不进入框选、不弹菜单，直接退出截图状态。
        PreviewMouseRightButtonDown+=(_,e)=>{e.Handled=true;Close();};
        MouseRightButtonDown+=(_,e)=>e.Handled=true;
        MouseRightButtonUp+=(_,e)=>e.Handled=true;
        ContextMenu=null;
        MouseLeftButtonDown+=(_,e)=>{if(!selected){BeginSelect(e.GetPosition(root));e.Handled=true;}};
        MouseMove+=(_,e)=>{
            var p=e.GetPosition(root);
            if(resizing)UpdateResize(p);
            else if(moving)UpdateMove(p);
            else if(selecting)UpdateSelection(p);
            else if(selected)Cursor=EdgeHit(p)!=ResizeEdge.None?CursorFor(EdgeHit(p)):layer.Current==AnnotationLayer.Tool.None&&InSelection(p)?Cursors.SizeAll:Cursors.Cross;
            else Cursor=Cursors.Cross;
            if(!IsActive)Activate();
        };
        MouseLeftButtonUp+=(_,e)=>{if(resizing||moving){resizing=false;moving=false;ReleaseMouseCapture();Cursor=Cursors.Cross;PlaceToolbar();toolbar?.Visibility=Visibility.Visible;}else if(selecting)FinishSelection();};
    }
    bool moving;Point moveStart;Rect moveRect;
    void StartMove(Point point)
    {
        CommitTyping();moving=true;moveStart=point;moveRect=selection;CaptureMouse();
    }
    void UpdateMove(Point p)
    {
        double left=Math.Clamp(moveRect.X+(p.X-moveStart.X),0,Math.Max(0,Width-moveRect.Width));
        double top=Math.Clamp(moveRect.Y+(p.Y-moveStart.Y),0,Math.Max(0,Height-moveRect.Height));
        ApplySelectionRect(new Rect(left,top,moveRect.Width,moveRect.Height));
    }
    [Flags]
    enum ResizeEdge{None=0,W=1,E=2,N=4,S=8}
    static readonly Cursor CrossCursor=Cursors.Cross;
    static Cursor CursorFor(ResizeEdge edge)=>edge switch
    {
        ResizeEdge.W or ResizeEdge.E=>Cursors.SizeWE,
        ResizeEdge.N or ResizeEdge.S=>Cursors.SizeNS,
        ResizeEdge.W|ResizeEdge.N or ResizeEdge.E|ResizeEdge.S=>Cursors.SizeNWSE,
        ResizeEdge.E|ResizeEdge.N or ResizeEdge.W|ResizeEdge.S=>Cursors.SizeNESW,
        _=>CrossCursor
    };
    ResizeEdge EdgeHit(Point p)
    {
        const double t=6;
        var r=selection;
        bool left=Math.Abs(p.X-r.Left)<=t&&p.Y>=r.Top-t&&p.Y<=r.Bottom+t;
        bool right=Math.Abs(p.X-r.Right)<=t&&p.Y>=r.Top-t&&p.Y<=r.Bottom+t;
        bool top=Math.Abs(p.Y-r.Top)<=t&&p.X>=r.Left-t&&p.X<=r.Right+t;
        bool bottom=Math.Abs(p.Y-r.Bottom)<=t&&p.X>=r.Left-t&&p.X<=r.Right+t;
        return (ResizeEdge)((left?1:0)|(right?2:0)|(top?4:0)|(bottom?8:0));
    }
    bool resizing;ResizeEdge resizeEdge;Rect resizeStart;
    void StartResize(ResizeEdge edge,Point point)
    {
        CommitTyping();resizing=true;resizeEdge=edge;resizeStart=selection;CaptureMouse();
        // 工具条保持可见并随选区下缘跟随移动。
    }
    void UpdateResize(Point p)
    {
        var r0=resizeStart;
        double l=r0.Left,r=r0.Right,t=r0.Top,b=r0.Bottom;
        p.X=Math.Clamp(p.X,0,Width);p.Y=Math.Clamp(p.Y,0,Height);
        if((resizeEdge&ResizeEdge.W)!=0)l=Math.Min(p.X,r0.Right-8);
        if((resizeEdge&ResizeEdge.E)!=0)r=Math.Max(p.X,r0.Left+8);
        if((resizeEdge&ResizeEdge.N)!=0)t=Math.Min(p.Y,r0.Bottom-8);
        if((resizeEdge&ResizeEdge.S)!=0)b=Math.Max(p.Y,r0.Top+8);
        ApplySelectionRect(new Rect(l,t,r-l,b-t));
    }
    // 工具条及其弹层（字号下拉等独立 HWND Popup）都视作界面元素：弹层内容不在 chrome 视觉树内，
    // 但鼠标事件会沿逻辑树冒泡回窗口，不识别会把下拉选项点击误判成“点在选区外”。
    static bool OverChrome(MouseEventArgs e,DependencyObject chrome)=>e.OriginalSource is DependencyObject d&&(IsDescendantOf(d,chrome)||AnnotationToolbar.FromPopupContent(d));
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
        ApplySelectionRect(new Rect(x,y,w,h));
    }
    // 统一刷新选区几何：遮罩挖孔、边框、尺寸标签、工具条位置。框选与二次调整共用。
    void ApplySelectionRect(Rect r)
    {
        selection=r;
        var full=new RectangleGeometry(new Rect(0,0,Width,Height));
        dim.Clip=r.Width>1&&r.Height>1?new CombinedGeometry(GeometryCombineMode.Exclude,full,new RectangleGeometry(r)):full;
        selectionBorder.Visibility=r.Width>1?Visibility.Visible:Visibility.Hidden;
        Canvas.SetLeft(selectionBorder,r.Left);Canvas.SetTop(selectionBorder,r.Top);selectionBorder.Width=r.Width;selectionBorder.Height=r.Height;
        foreach(var handle in handles)handle.Visibility=selectionBorder.Visibility;
        PositionHandles(r);
        sizeLabel.Text=$"{(int)(r.Width*dpiX)} × {(int)(r.Height*dpiY)}";
        sizeLabel.Visibility=Visibility.Visible;
        Canvas.SetLeft(sizeLabel,r.Left);Canvas.SetTop(sizeLabel,Math.Max(0,r.Top-26));
        if(!selecting)PlaceToolbar();
        layer.WatermarkBounds=r; // 自动水印随选区铺满
    }
    void PositionHandles(Rect r)
    {
        var points=new[]
        {
            new Point(r.Left,r.Top),new Point(r.Left+r.Width/2,r.Top),new Point(r.Right,r.Top),
            new Point(r.Left,r.Top+r.Height/2),new Point(r.Right,r.Top+r.Height/2),
            new Point(r.Left,r.Bottom),new Point(r.Left+r.Width/2,r.Bottom),new Point(r.Right,r.Bottom)
        };
        for(int i=0;i<handles.Count&&i<points.Length;i++)
        {
            Canvas.SetLeft(handles[i],points[i].X-4.5);
            Canvas.SetTop(handles[i],points[i].Y-4.5);
        }
    }
    void FinishSelection()
    {
        selecting=false;
        if(selection.Width<8||selection.Height<8){dim.Clip=null;selectionBorder.Visibility=Visibility.Hidden;sizeLabel.Visibility=Visibility.Hidden;selected=false;return;}
        selected=true;
        layer.Visibility=Visibility.Visible;
        if(translateMode)RunTranslation();
        else ShowToolbar();
    }
    // 翻译流程：OCR 识别选区文字（优先本机 Umi-OCR，回退系统 OCR），再中英互译（优先 Umi-OCR，回退在线），结果卡片贴近选区展示。
    bool translating;
    void RunTranslation()
    {
        if(translating)return;translating=true;
        sizeLabel.Text="正在识别与翻译…";sizeLabel.Visibility=Visibility.Visible;
        Canvas.SetLeft(sizeLabel,selection.Left);Canvas.SetTop(sizeLabel,Math.Max(0,selection.Top-26));
        var image=Compose();
        _=RunTranslationAsync(image);
    }
    async System.Threading.Tasks.Task RunTranslationAsync(BitmapSource image)
    {
        var (text,ocrOk)=await Translation.RecognizeAsync(settings,image);
        if(!ocrOk||string.IsNullOrWhiteSpace(text)){TranslatingFailed("未识别到文字，请框选包含文字的区域");return;}
        var (translated,detected,ok,error)=await Translation.TranslateAsync(settings,text);
        if(!ok){TranslatingFailed(error);return;}
        translating=false;
        await Dispatcher.BeginInvoke(new Action(()=>{
            sizeLabel.Visibility=Visibility.Hidden;
            TranslateCard.Show(text,translated,this,selection,dpiX,dpiY);
        }));
    }
    void TranslatingFailed(string message)
    {
        translating=false;
        Dispatcher.BeginInvoke(new Action(()=>{sizeLabel.Text=message;sizeLabel.Visibility=Visibility.Visible;Canvas.SetLeft(sizeLabel,selection.Left);Canvas.SetTop(sizeLabel,Math.Max(0,selection.Top-26));}));
    }
    void ShowToolbar()
    {
        if(toolbar==null)
        {
            StackPanel toolbarRow=null!;
            var pin=AnnotationToolbar.ActionChip("\uE718","贴到屏幕",PinCurrent);
            var scroll=AnnotationToolbar.ActionChip("\uE8C6","滚动截图",()=>{
                // 选项子行显示“开始滚动”按钮；点击后自动向下滚动并拼接长图
                var start=new Border{Background=new SolidColorBrush(Color.FromRgb(35,125,245)),CornerRadius=new CornerRadius(13),Height=26,Padding=new Thickness(14,0,14,0),Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,2,0,0),Child=new StackPanel{Orientation=Orientation.Horizontal,Children={new TextBlock{Text="\uE768",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=12,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center},new TextBlock{Text=" 开始滚动",FontSize=12,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center}}},ToolTip="开始滚动截图"};
                start.MouseEnter+=(_,_)=>start.Opacity=.85;start.MouseLeave+=(_,_)=>start.Opacity=1;
                start.MouseLeftButtonUp+=(_,_)=>StartScrollCapture();
                AnnotationToolbar.SetOptions(toolbarRow,start);
            });
            var save=AnnotationToolbar.ActionChip("\uE719","保存",SaveSelection);
            var cancel=AnnotationToolbar.ActionChip("\uE711","退出",Close,new SolidColorBrush(Color.FromRgb(229,72,77)));
            var done=AnnotationToolbar.ActionChip("\uE73E","复制",CopySelection,new SolidColorBrush(Color.FromRgb(46,164,90)));
            toolbarRow=(StackPanel)AnnotationToolbar.Build(layer,this,OnGripDrag,2,pin,scroll,save,cancel,done);
            toolbar=new Border{CornerRadius=new CornerRadius(10),Background=new SolidColorBrush(Color.FromArgb(246,252,253,255)),BorderBrush=new SolidColorBrush(Color.FromArgb(40,36,54,77)),BorderThickness=new Thickness(1),Padding=new Thickness(6,5,6,5),Child=toolbarRow,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=3,Opacity=.3},Cursor=Cursors.Hand};
            chrome.Children.Add(toolbar);
        }
        PlaceToolbar();toolbar.Visibility=Visibility.Visible;
    }
    // 手柄拖动：移动工具条位置（限制在窗口内）。
    void OnGripDrag(Vector v)
    {
        if(toolbar==null)return;
        double l=Canvas.GetLeft(toolbar)+v.X,t=Canvas.GetTop(toolbar)+v.Y;
        if(double.IsNaN(l))l=0;if(double.IsNaN(t))t=0;
        l=Math.Clamp(l,4,Math.Max(4,Width-toolbar.ActualWidth-4));
        t=Math.Clamp(t,4,Math.Max(4,Height-toolbar.ActualHeight-4));
        Canvas.SetLeft(toolbar,l);Canvas.SetTop(toolbar,t);
    }
    internal void OnToolbarLayoutChanged()=>PlaceToolbar();
    // 滚动截图：把选区屏幕物理位置交给 ScrollCapture（覆盖层先关闭，露出下方内容）。
    void StartScrollCapture()
    {
        var origin=PointToScreen(new Point(selection.Left,selection.Top));
        var dpi=VisualTreeHelper.GetDpi(this);
        var phys=new Rect(origin.X,origin.Y,selection.Width*dpi.DpiScaleX,selection.Height*dpi.DpiScaleY);
        Close();
        ScrollCapture.Run(phys,settings);
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
        layer.SuppressOverlay=true; // 选择框不进入成图
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
    void CopySelection()
    {
        try{System.Windows.Clipboard.SetImage(Compose());Close();} // 复制后结束截图
        catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException){GlassDialog.Show("复制失败："+ex.Message);}
    }
    // 保存放到独立 STA 线程，并临时隐藏覆盖层：避免全屏置顶覆盖层把保存对话框挡在后面（表现为“点了没反应”）。
    void SaveSelection()
    {
        var image=Compose();
        Visibility=Visibility.Hidden;
        var thread=new System.Threading.Thread(()=>
        {
            bool saved=false;
            try
            {
                var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="windock-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"};
                var pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if(!string.IsNullOrEmpty(pictures)&&Directory.Exists(pictures))dialog.InitialDirectory=pictures;
                if(dialog.ShowDialog()==true)
                {
                    using var stream=File.Create(dialog.FileName);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
                    ActivityStore.Write("截图",message:"已保存 "+dialog.FileName);
                    saved=true;
                }
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Dispatcher.BeginInvoke(new Action(()=>{if(Screenshot.Capturing==this)Visibility=Visibility.Visible;GlassDialog.Show("保存失败："+ex.Message);}));
                return;
            }
            Dispatcher.BeginInvoke(new Action(()=>{if(saved)Close();else if(Screenshot.Capturing==this)Visibility=Visibility.Visible;}));
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
    public void PinCurrent()
    {
        try
        {
            var image=Compose();
            // 原位贴图：贴图窗口出现在选区位置（覆盖层坐标即选区坐标系）
            var at=new Rect(Left+selection.Left,Top+selection.Top,selection.Width,selection.Height);
            Close();PinnedImage.Pin(image,settings,at);
        }
        catch(Exception ex)when(ex is System.Runtime.InteropServices.COMException){GlassDialog.Show("贴图失败："+ex.Message);}
    }
    // 截取整个虚拟桌面（所有显示器）的物理像素（GDI BitBlt，避免引入 WinForms）。
    static BitmapSource CaptureScreen(out Rect area,out double dpiX,out double dpiY)
    {
        int vx=GetSystemMetrics(76),vy=GetSystemMetrics(77),vw=GetSystemMetrics(78),vh=GetSystemMetrics(79); // SM_XVIRTUALSCREEN…
        if(vw<=0){vx=0;vy=0;vw=GetSystemMetrics(0);vh=GetSystemMetrics(1);}
        area=new Rect(vx,vy,vw,vh);
        dpiX=1;dpiY=1;
        var hdcScreen=GetDC(IntPtr.Zero);
        var hdcMem=CreateCompatibleDC(hdcScreen);
        var info=new BitmapInfo{Size=40,Width=vw,Height=-vh,Planes=1,BitCount=32,Compression=0};
        var dib=CreateDIBSection(hdcMem,ref info,0,out var bits,IntPtr.Zero,0);
        var previous=SelectObject(hdcMem,dib);
        BitBlt(hdcMem,0,0,vw,vh,hdcScreen,vx,vy,0x00CC0020|0x40000000); // SRCCOPY|CAPTUREBLT
        var pixels=new byte[vw*vh*4];
        Marshal.Copy(bits,pixels,0,pixels.Length);
        SelectObject(hdcMem,previous);DeleteObject(dib);DeleteDC(hdcMem);ReleaseDC(IntPtr.Zero,hdcScreen);
        var source=BitmapSource.Create(vw,vh,96,96,PixelFormats.Bgr32,null,pixels,vw*4);
        source.Freeze();return source;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfo{public int Size,Width,Height;public short Planes,BitCount;public int Compression,SizeImage,XPelsPerMeter,YPelsPerMeter,ColorsUsed,ColorsImportant;}
    [DllImport("user32.dll")]static extern int GetSystemMetrics(int index);
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
            Activate();
            if(!IsActive){Topmost=false;Topmost=true;Activate();} // Windows 前台锁定时强制置顶抡焦点，避免"按了没反应"
        }));
    }
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        owner.Dispatcher.BeginInvoke(new Action(()=>owner.RestoreFromCapture()));
    }
}
