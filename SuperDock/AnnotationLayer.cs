using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace SuperDock;
// 截图与贴图共用的标注画布：涂鸦（自由画笔）、矩形、椭圆、箭头、马赛克、文字，支持撤销与清空。
// 坐标即宿主窗口的逻辑坐标；绘制期间鼠标被捕获，松手提交一个图元。
internal sealed class AnnotationLayer : Canvas
{
    public enum Tool { None, Pen, Rect, Ellipse, Arrow, Mosaic, Text }
    Tool current=Tool.None;Color color=Color.FromRgb(255,90,90);double strokeWidth=3;
    public Tool Current{get=>current;set{current=value;Changed?.Invoke();}}
    public Color Color{get=>color;set{color=value;ColorChanged?.Invoke();}}
    public double StrokeWidth{get=>strokeWidth;set{strokeWidth=value;ColorChanged?.Invoke();}}
    public event Action? Changed;
    public event Action? ColorChanged;
    public bool IsTyping=>typing!=null;
    public bool HasDrawings=>Children.Count>0;
    // 马赛克由宿主提供像素源：给定逻辑区域返回马赛克化后的位图（截图=冻结帧区域；贴图=图片对应区域）。
    public Func<Rect,ImageSource?>? MosaicProvider {get;set;}
    readonly List<UIElement> history=[];
    Point start;UIElement? active;TextEditor? typing;
    sealed class TextEditor{public TextBox Box=null!;public Point Pos;}
    public AnnotationLayer(){Background=Brushes.Transparent;Focusable=false;}
    static Brush Stroke(Color color)=>new SolidColorBrush(color){Opacity=.95};
    public void Undo(){if(Children.Count==0)return;var last=Children[^1];Children.Remove(last);history.Remove(last);Changed?.Invoke();}
    public void ClearAll(){Children.Clear();history.Clear();Changed?.Invoke();}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var point=e.GetPosition(this);
        if(Current==Tool.None){e.Handled=false;return;}
        if(Current==Tool.Text){BeginText(point);e.Handled=true;return;}
        CommitTyping();CaptureMouse();start=point;
        Brush stroke=Stroke(Color);double w=StrokeWidth;
        active=Current switch
        {
            Tool.Pen=>new Polyline{Stroke=stroke,StrokeThickness=w,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Points=new PointCollection{point}},
            Tool.Rect=>new Rectangle{Stroke=stroke,StrokeThickness=w},
            Tool.Ellipse=>new Ellipse{Stroke=stroke,StrokeThickness=w},
            Tool.Arrow=>new Path{Stroke=stroke,StrokeThickness=w,StrokeLineJoin=PenLineJoin.Round},
            Tool.Mosaic=>new Rectangle{Tag="mosaic",Stroke=new SolidColorBrush(Color.FromRgb(111,207,255)){Opacity=.9},StrokeThickness=1.2,StrokeDashArray=new DoubleCollection{3,2},Fill=Brushes.Transparent},
            _=>null
        };
        if(active!=null){SetLeft(active,0);SetTop(active,0);Children.Add(active);}
        e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(active==null)return;var point=e.GetPosition(this);
        double x=Math.Min(start.X,point.X),y=Math.Min(start.Y,point.Y),w=Math.Abs(point.X-start.X),h=Math.Abs(point.Y-start.Y);
        switch(active)
        {
            case Polyline line:
                if(line.Points.Count==0)line.Points=new PointCollection{start};
                var last=line.Points[^1];
                if(Math.Abs(point.X-last.X)>1.5||Math.Abs(point.Y-last.Y)>1.5)line.Points.Add(point); // 自由画笔：跟随轨迹逐点添加
                break;
            case Rectangle rect:SetLeft(rect,x);SetTop(rect,y);rect.Width=w;rect.Height=h;break;
            case Ellipse ellipse:SetLeft(ellipse,x);SetTop(ellipse,y);ellipse.Width=w;ellipse.Height=h;break;
            case Path arrow:arrow.Data=ArrowGeometry(start,point,Math.Max(12,StrokeWidth*4));break;
        }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(active==null)return; // 文字工具的输入框保持可编辑，由 Enter/失焦/再次点击提交
        ReleaseMouseCapture();
        var point=e.GetPosition(this);
        bool empty=active switch
        {
            Polyline line=>line.Points.Count<2,
            Rectangle rect=>rect.Width<2&&rect.Height<2,
            Ellipse ellipse=>ellipse.Width<2&&ellipse.Height<2,
            Path arrow=>arrow.Data==null,
            _=>true
        };
        if(active is Rectangle{Tag:"mosaic"} preview)
        {
            Children.Remove(preview);
            var rect=new Rect(Math.Min(start.X,point.X),Math.Min(start.Y,point.Y),Math.Abs(point.X-start.X),Math.Abs(point.Y-start.Y));
            if(rect.Width>=8&&rect.Height>=8)
            {
                var source=MosaicProvider?.Invoke(rect);
                if(source!=null)
                {
                    var mosaic=new Image{Source=source,Width=rect.Width,Height=rect.Height};
                    RenderOptions.SetBitmapScalingMode(mosaic,BitmapScalingMode.NearestNeighbor);
                    SetLeft(mosaic,rect.Left);SetTop(mosaic,rect.Top);
                    Children.Add(mosaic);history.Add(mosaic);Changed?.Invoke();
                }
            }
            active=null;e.Handled=true;return;
        }
        if(empty)Children.Remove(active);else{history.Add(active);Changed?.Invoke();}
        active=null;e.Handled=true;
    }
    void BeginText(Point point)
    {
        CommitTyping();
        var box=new TextBox{MinWidth=60,Background=Brushes.Transparent,BorderBrush=new SolidColorBrush(Color.FromArgb(140,255,255,255)),BorderThickness=new Thickness(0,0,0,1),Foreground=Stroke(Color),FontSize=18,FontWeight=FontWeights.SemiBold,CaretBrush=Stroke(Color),Padding=new Thickness(2)};
        SetLeft(box,point.X);SetTop(box,point.Y-11);Children.Add(box);box.Focus();
        var editor=new TextEditor{Box=box,Pos=point};
        typing=editor;
        box.KeyDown+=(_,ev)=>{if(ev.Key==Key.Enter){CommitTyping();ev.Handled=true;}else if(ev.Key==Key.Escape){Children.Remove(box);typing=null;ev.Handled=true;}};
        box.LostKeyboardFocus+=(_,_)=>CommitTyping();
    }
    public void CommitTyping()
    {
        if(typing==null)return;var box=typing.Box;
        if(box.Text.Trim().Length==0){Children.Remove(box);typing=null;return;}
        int index=Children.IndexOf(box);Children.Remove(box);
        var text=new TextBlock{Text=box.Text,Foreground=box.Foreground,FontSize=box.FontSize,FontWeight=FontWeights.SemiBold};
        SetLeft(text,typing.Pos.X);SetTop(text,typing.Pos.Y-11);
        if(index>=0)Children.Insert(index,text);else Children.Add(text);
        history.Add(text);typing=null;Changed?.Invoke();
    }
    // 箭头几何：杆 + 两条短线组成箭头头部，单一 Path 便于撤销。
    Geometry ArrowGeometry(Point from,Point to,double head)
    {
        var vector=to-from;double length=Math.Sqrt(vector.X*vector.X+vector.Y*vector.Y);
        if(length<1) return Geometry.Empty;
        var unit=new Point(vector.X/length,vector.Y/length);
        var left=new Point(to.X-(unit.X*.7-unit.Y*.7)*head,to.Y-(unit.Y*.7+unit.X*.7)*head);
        var right=new Point(to.X-(unit.X*.7+unit.Y*.7)*head,to.Y-(unit.X*.7-unit.Y*.7)*head);
        var geometry=new StreamGeometry();using(var ctx=geometry.Open()){ctx.BeginFigure(from,false,false);ctx.LineTo(to,true,false);ctx.BeginFigure(left,false,false);ctx.LineTo(to,true,false);ctx.LineTo(right,true,false);}
        geometry.Freeze();return geometry;
    }
    // 马赛克生成：物理像素区域 → 降采样再最近邻放大，得到打码位图。
    internal static ImageSource? Pixelate(BitmapSource source,Rect physical)
    {
        int x=(int)Math.Round(physical.Left),y=(int)Math.Round(physical.Top),w=(int)Math.Round(physical.Width),h=(int)Math.Round(physical.Height);
        x=Math.Max(0,Math.Min(x,source.PixelWidth-4));y=Math.Max(0,Math.Min(y,source.PixelHeight-4));
        w=Math.Max(4,Math.Min(w,source.PixelWidth-x));h=Math.Max(4,Math.Min(h,source.PixelHeight-y));
        try
        {
            var crop=new CroppedBitmap(source,new Int32Rect(x,y,w,h));
            int block=Math.Max(10,Math.Min(w,h)/14);
            int sw=Math.Max(1,w/block),sh=Math.Max(1,h/block);
            var downX=new ScaleTransform((double)sw/w,(double)sh/h);downX.Freeze();
            var down=new TransformedBitmap(crop,downX);down.Freeze();
            var upX=new ScaleTransform((double)w/sw,(double)h/sh);upX.Freeze();
            var up=new TransformedBitmap(down,upX);up.Freeze();
            return up;
        }
        catch(InvalidOperationException){return null;}
    }
}
