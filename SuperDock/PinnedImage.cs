using System.IO;
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
    double scale=1;bool alwaysTop=true;
    PinnedImage(BitmapSource source,MainWindow.Settings settings)
    {
        this.settings=settings;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Title="windock · 贴图";
        image=new Image{Source=source,Stretch=Stretch.Fill};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        var frame=new Border{CornerRadius=new CornerRadius(6),Child=image,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=4,Opacity=.45,Color=Color.FromRgb(12,18,28)}};
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
            if(DesktopIntegration.Cursor(out var cursor))
            {
                var work=SystemParameters.WorkArea;
                double x=cursor.X/dpi.DpiScaleX,y=cursor.Y/dpi.DpiScaleY;
                Left=Math.Clamp(x-image.Width/2,work.Left,Math.Max(work.Left,work.Right-image.Width));
                Top=Math.Clamp(y-image.Height/2,work.Top,Math.Max(work.Top,work.Bottom-image.Height));
            }
            else WindowStartupLocation=WindowStartupLocation.CenterScreen;
        };
        MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2){Close();return;}if(e.ButtonState==MouseButtonState.Pressed)try{DragMove();}catch(InvalidOperationException){}};
        MouseRightButtonUp+=(_,_)=>ShowMenu();
        PreviewMouseWheel+=(_,e)=>{Zoom(e.Delta>0?1.1:1/1.1);e.Handled=true;};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape)Close();};
        Deactivated+=(_,_)=>{layer.Current=AnnotationLayer.Tool.None;if(toolbar!=null)toolbar.Visibility=Visibility.Collapsed;};
    }
    void Zoom(double factor)
    {
        if(layer.Visibility==Visibility.Visible)layer.CommitTyping();
        var work=SystemParameters.WorkArea;
        double cx=Left+ActualWidth/2,cy=Top+ActualHeight/2;
        double ratio=factor;
        double width=Math.Max(40,ActualWidth*ratio),height=Math.Max(40,ActualHeight*ratio);
        if(width>work.Width*1.8||height>work.Height*2.4||width<24||height<24)return;
        scale*=ratio;
        SizeToContent=SizeToContent.Manual;
        image.Width=image.ActualWidth*ratio;image.Height=image.ActualHeight*ratio;
        layer.Width=image.Width;layer.Height=image.Height;
        Width=width;Height=height;
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
        menu.Items.Add(Item("复制",(s,_)=>{try{System.Windows.Clipboard.SetImage((BitmapSource)image.Source);}catch(System.Runtime.InteropServices.COMException){}}));
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
        if(toolbar==null)
        {
            var done=AnnotationToolbar.ActionChip("","标注完成",()=>ToggleAnnotate());
            toolbar=new Border{CornerRadius=new CornerRadius(10),Background=new SolidColorBrush(Color.FromArgb(246,252,253,255)),BorderBrush=new SolidColorBrush(Color.FromArgb(40,36,54,77)),BorderThickness=new Thickness(1),Padding=new Thickness(6,5,6,5),Child=AnnotationToolbar.Build(layer,this,OnGripDrag,0,done),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.3}};
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
        var source=(BitmapSource)image.Source;
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
    internal static void Pin(BitmapSource source,MainWindow.Settings settings)
    {
        if(source.CanFreeze&&!source.IsFrozen)source.Freeze();
        new PinnedImage(source,settings).Show();
    }
}
