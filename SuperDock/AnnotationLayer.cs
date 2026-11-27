using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace SuperDock;
// 截图与贴图共用的标注画布：涂鸦（自由画笔）、矩形、椭圆、箭头、马赛克、文字、水印，
// 支持撤销与清空；矩形/椭圆/箭头可再次点击选中，拖动移动、拖手柄缩放。
// 坐标即宿主窗口的逻辑坐标；绘制期间鼠标被捕获，松手提交一个图元。
internal sealed class AnnotationLayer : Canvas
{
    public enum Tool { None, Pen, Rect, Ellipse, Arrow, Mosaic, Text, Watermark }
    Tool current=Tool.None;Color color=Color.FromRgb(255,90,90);double strokeWidth=3;
    public Tool Current{get=>current;set{current=value;Changed?.Invoke();RefreshWatermark();}}
    public Color Color{get=>color;set{color=value;ColorChanged?.Invoke();}}
    public double StrokeWidth{get=>strokeWidth;set{strokeWidth=value;ColorChanged?.Invoke();}}
    // 文本字号、水印样式、马赛克强度（由工具条选项行调节）。
    public double FontSize{get;set;}=16;
    public int MosaicStrength{get;set;}=14;
    string watermarkTextValue="";Color watermarkColor=Color.FromRgb(90,98,108);double watermarkOpacity=.45;
    public string WatermarkText{get=>watermarkTextValue;set{watermarkTextValue=value;RefreshWatermark();}}
    public Color WatermarkColor{get=>watermarkColor;set{watermarkColor=value;RefreshWatermark();}}
    public double WatermarkOpacity{get=>watermarkOpacity;set{watermarkOpacity=Math.Clamp(value,.05,1);RefreshWatermark();}}
    Rect? watermarkBounds;
    // 水印铺满区域：截图=选区；贴图=整张图。由宿主设置。
    public Rect? WatermarkBounds{get=>watermarkBounds;set{watermarkBounds=value;if(watermarkTile.Visibility==Visibility.Visible)RefreshWatermark();}}
    // 水印自动生效：选中文本非空时立即铺满；切换到其他工具后保留，撤销/清空才移除。
    bool watermarkApplied;
    double watermarkJitterX,watermarkJitterY; // 每次重新生效时随机偏移，避免每次都从同一位置平铺
    public bool WatermarkApplied=>watermarkApplied&&!string.IsNullOrWhiteSpace(watermarkTextValue);
    public event Action? Changed;
    public event Action? ColorChanged;
    public bool IsTyping=>typing!=null;
    public bool HasDrawings=>(watermarkApplied&&!string.IsNullOrWhiteSpace(watermarkTextValue))||Children.OfType<UIElement>().Any(c=>!ReferenceEquals(c,overlay)&&!ReferenceEquals(c,watermarkTile));
    // 马赛克由宿主提供像素源：给定逻辑区域返回马赛克化后的位图（截图=冻结帧区域；贴图=图片对应区域）。
    public Func<Rect,ImageSource?>? MosaicProvider {get;set;}
    readonly List<UIElement> history=[];
    Point start;UIElement? active;TextEditor? typing;
    sealed class TextEditor{public TextBox Box=null!;public Point Pos;}
    // 图形选择：修饰层（虚线框+8 手柄）挂在画布顶层，IsHitTestVisible=false，不参与绘制与撤销。
    readonly Canvas overlay=new(){IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    readonly Rectangle watermarkTile=new(){IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    readonly Rectangle shapeBorder=new(){StrokeThickness=0,Fill=Brushes.Transparent};
    readonly List<Ellipse> shapeHandles=new();
    public UIElement? SelectedShape{get;private set;}
    public AnnotationLayer()
    {
        Background=Brushes.Transparent;Focusable=false;
        overlay.Children.Add(shapeBorder);
        for(int i=0;i<8;i++)
        {
            var handle=new Ellipse{Width=13,Height=13,Fill=Brushes.White,Stroke=new SolidColorBrush(Color.FromArgb(120,70,80,90)),StrokeThickness=1};
            shapeHandles.Add(handle);overlay.Children.Add(handle);
        }
        Children.Add(overlay); // 置于最底层占位；渲染时提到顶层
    }
    public bool SuppressOverlay{set=>overlay.Visibility=value?Visibility.Collapsed:SelectedShape==null?Visibility.Collapsed:Visibility.Visible;}
    static Brush Stroke(Color color)=>new SolidColorBrush(color){Opacity=.95};
    // 修饰层置顶：新图元加入后把它移到最后（最上层）。
    void RaiseOverlay()
    {
        if(Children.Count>0&&ReferenceEquals(Children[Children.Count-1],overlay))return;
        Children.Remove(overlay);Children.Add(overlay);
    }
    // —— 自动水印：平铺铺满目标区域，文本/颜色/透明度实时生效 ——
    // 把水印层插到选择修饰层下方（图元之上），确保撤销/清空不会误删它。
    void PlaceWatermarkTile()
    {
        Children.Remove(watermarkTile);
        int at=Children.IndexOf(overlay);if(at<0)at=Children.Count;
        Children.Insert(at,watermarkTile);
    }
    void RefreshWatermark()
    {
        bool wasApplied=watermarkApplied;
        if(Current==Tool.Watermark&&!string.IsNullOrWhiteSpace(watermarkTextValue))watermarkApplied=true;
        bool show=WatermarkApplied;
        watermarkTile.Visibility=show?Visibility.Visible:Visibility.Collapsed;
        if(!show){watermarkTile.Fill=null;if(watermarkApplied!=wasApplied)Changed?.Invoke();return;}
        var region=watermarkBounds??new Rect(0,0,Math.Max(1,ActualWidth),Math.Max(1,ActualHeight));
        if(region.Width<8||region.Height<8){watermarkTile.Visibility=Visibility.Collapsed;return;}
        double ppd=1;try{ppd=VisualTreeHelper.GetDpi(this).PixelsPerDip;}catch{}
        var face=new Typeface(new FontFamily("Microsoft YaHei UI, Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
        var ft=new FormattedText(watermarkTextValue,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,face,26,new SolidColorBrush(watermarkColor),ppd);
        var geo=ft.BuildGeometry(new Point(0,0));
        var bounds=geo.Bounds;
        var rotation=new RotateTransform(-30,bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
        double minX=double.MaxValue,minY=double.MaxValue,maxX=double.MinValue,maxY=double.MinValue;
        foreach(var corner in new[]{new Point(bounds.Left,bounds.Top),new Point(bounds.Right,bounds.Top),new Point(bounds.Left,bounds.Bottom),new Point(bounds.Right,bounds.Bottom)})
        {
            var p=rotation.Transform(corner);
            minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);
        }
        double padX=260,padY=170; // 稀疏排布：实例间留大间隙
        var group=new DrawingGroup
        {
            Transform=new TransformGroup{Children={rotation,new TranslateTransform(padX/2-minX,padY/2-minY)}},
            Children={new GeometryDrawing{Geometry=geo,Brush=new SolidColorBrush(watermarkColor)}}
        };
        double stepX=(maxX-minX)+padX,stepY=(maxY-minY)+padY;
        if(!wasApplied&&watermarkApplied) // 重新生效时随机偏移平铺起点
        {
            watermarkJitterX=System.Random.Shared.NextDouble()*stepX;
            watermarkJitterY=System.Random.Shared.NextDouble()*stepY;
        }
        // 用 Viewport 负原点整体平移瓦片网格：文字始终完整落在单元格内，不会被裁切
        watermarkTile.Fill=new DrawingBrush{Drawing=group,TileMode=TileMode.Tile,Viewport=new Rect(-watermarkJitterX,-watermarkJitterY,stepX,stepY),ViewportUnits=BrushMappingMode.Absolute,Opacity=watermarkOpacity};
        SetLeft(watermarkTile,region.Left);SetTop(watermarkTile,region.Top);
        watermarkTile.Width=region.Width;watermarkTile.Height=region.Height;
        watermarkTile.Clip=new RectangleGeometry(new Rect(0,0,region.Width,region.Height));
        PlaceWatermarkTile();
        if(watermarkApplied!=wasApplied)Changed?.Invoke();
    }
    public void Undo()
    {
        for(int i=Children.Count-1;i>=0;i--)
        {
            var child=Children[i];
            if(ReferenceEquals(child,overlay)||ReferenceEquals(child,watermarkTile))continue;
            Children.Remove(child);history.Remove(child);
            if(ReferenceEquals(child,SelectedShape))Deselect();
            Changed?.Invoke();return;
        }
        if(watermarkApplied) // 没有可撤销的图元时先撤水印
        {
            watermarkApplied=false;RefreshWatermark();Changed?.Invoke();
        }
    }
    public void ClearAll(){Deselect();watermarkApplied=false;RefreshWatermark();for(int i=Children.Count-1;i>=0;i--)if(!ReferenceEquals(Children[i],overlay)&&!ReferenceEquals(Children[i],watermarkTile))Children.RemoveAt(i);history.Clear();Changed?.Invoke();}
    // —— 图形选中/移动/缩放 ——
    static Rect BoundsOf(UIElement el)=>el switch
    {
        Rectangle r=>new Rect(GetLeft(r),GetTop(r),r.Width,r.Height),
        Ellipse e=>new Rect(GetLeft(e),GetTop(e),e.Width,e.Height),
        Image i=>new Rect(GetLeft(i),GetTop(i),i.Width,i.Height),
        TextBlock t=>new Rect(GetLeft(t),GetTop(t),t.ActualWidth,t.ActualHeight),
        Path p=>p.Data is null?Rect.Empty:new Rect(GetLeft(p)+p.Data.Bounds.X,GetTop(p)+p.Data.Bounds.Y,p.Data.Bounds.Width,p.Data.Bounds.Height),
        _=>Rect.Empty
    };
    // 可再次点击选中的图形：矩形 / 椭圆 / 箭头（用户指定范围）。
    static bool Selectable(UIElement el)=>el switch
    {
        Rectangle{Tag:"mosaic"}=>false,
        Rectangle or Ellipse=>true,
        Path p=>p.Data!=null&&!p.Data.Bounds.IsEmpty&&p.Data.Bounds.Width>2&&p.Data.Bounds.Height>2,
        _=>false
    };
    // 命中判定只认图形边框（描边带）：点内部不算选中，便于在图形内部继续绘制新图元。
    public UIElement? ShapeAt(Point point)
    {
        for(int i=Children.Count-1;i>=0;i--)
        {
            var el=Children[i];
            if(ReferenceEquals(el,overlay)||!Selectable(el))continue;
            if(!HitsStroke(el,point))continue;
            return el;
        }
        return null;
    }
    // 容差：描边半宽 + 少量余量，保证贴着边框点击能选中。
    static double HitTolerance(UIElement el)=>el switch{Shape s=>Math.Max(3.5,s.StrokeThickness*.5+2),_=>4};
    static bool HitsStroke(UIElement el,Point point)
    {
        var bounds=BoundsOf(el);
        if(bounds.IsEmpty||bounds.Width<=0||bounds.Height<=0)return false;
        double t=HitTolerance(el);
        switch(el)
        {
            case Rectangle:
            {
                var outer=bounds;outer.Inflate(t,t);
                if(!outer.Contains(point))return false;
                var inner=bounds;inner.Inflate(-t,-t);
                return inner.Width<=0||inner.Height<=0||!inner.Contains(point);
            }
            case Ellipse:
            {
                var outer=bounds;outer.Inflate(t,t);
                double ox=outer.Left+outer.Width/2,oy=outer.Top+outer.Height/2;
                double kx=(point.X-ox)/(outer.Width/2),ky=(point.Y-oy)/(outer.Height/2);
                if(kx*kx+ky*ky>1)return false;
                double iw=bounds.Width/2-t,ih=bounds.Height/2-t; // 图形太小时整体都算命中
                if(iw<=0||ih<=0)return true;
                double cx=bounds.Left+bounds.Width/2,cy=bounds.Top+bounds.Height/2;
                double dx=(point.X-cx)/iw,dy=(point.Y-cy)/ih;
                return dx*dx+dy*dy>=1;
            }
            case Path arrow when arrow.Tag is Point[] ft&&ft.Length==2: // 箭头：杆 + 两翼任一线段附近
            {
                double limit=Math.Max(t,arrow.StrokeThickness*.5+2);
                var vector=ft[1]-ft[0];double length=Math.Sqrt(vector.X*vector.X+vector.Y*vector.Y);
                if(length<1)return false;
                if(DistToSegment(point,ft[0],ft[1])<=limit)return true;
                var unit=new Point(vector.X/length,vector.Y/length);
                double head=ArrowHead(arrow);
                var left=new Point(ft[1].X-(unit.X*.7-unit.Y*.7)*head,ft[1].Y-(unit.Y*.7+unit.X*.7)*head);
                var right=new Point(ft[1].X-(unit.X*.7+unit.Y*.7)*head,ft[1].Y-(unit.Y*.7-unit.X*.7)*head);
                return DistToSegment(point,ft[1],left)<=limit||DistToSegment(point,ft[1],right)<=limit;
            }
            default:return false;
        }
    }
    static double DistToSegment(Point p,Point a,Point b)
    {
        double abx=b.X-a.X,aby=b.Y-a.Y,len2=abx*abx+aby*aby;
        if(len2<.01)return (p-a).Length;
        double t2=Math.Clamp(((p.X-a.X)*abx+(p.Y-a.Y)*aby)/len2,0,1);
        return (p-(new Point(a.X+abx*t2,a.Y+aby*t2))).Length;
    }
    void Select(UIElement el)
    {
        SelectedShape=el;
        RaiseOverlay();
        overlay.Visibility=Visibility.Visible;
        RefreshShapeOverlay();
        Changed?.Invoke();
    }
    public void Deselect()
    {
        if(SelectedShape==null)return;
        SelectedShape=null;
        overlay.Visibility=Visibility.Collapsed;
        Changed?.Invoke();
    }
    void RefreshShapeOverlay()
    {
        if(SelectedShape==null)return;
        var bounds=BoundsOf(SelectedShape);
        if(bounds.IsEmpty){Deselect();return;}
        // 选中框与手柄直接贴在图形边框上，不再向外偏移
        Canvas.SetLeft(shapeBorder,bounds.Left);Canvas.SetTop(shapeBorder,bounds.Top);
        shapeBorder.Width=Math.Max(0,bounds.Width);shapeBorder.Height=Math.Max(0,bounds.Height);
        var points=new[]
        {
            new Point(bounds.Left,bounds.Top),new Point(bounds.Left+bounds.Width/2,bounds.Top),new Point(bounds.Right,bounds.Top),
            new Point(bounds.Left,bounds.Top+bounds.Height/2),new Point(bounds.Right,bounds.Top+bounds.Height/2),
            new Point(bounds.Left,bounds.Bottom),new Point(bounds.Left+bounds.Width/2,bounds.Bottom),new Point(bounds.Right,bounds.Bottom)
        };
        for(int i=0;i<shapeHandles.Count&&i<points.Length;i++)
        {
            Canvas.SetLeft(shapeHandles[i],points[i].X-6.5);Canvas.SetTop(shapeHandles[i],points[i].Y-6.5);
            shapeHandles[i].Visibility=shapeBorder.Width>18||shapeBorder.Height>18?Visibility.Visible:Visibility.Collapsed;
        }
    }
    // 手柄命中：返回 0..7（角/边），未命中 -1。与外部选区手柄布局一致。
    int HandleHit(Point point)
    {
        if(SelectedShape==null)return -1;
        var bounds=BoundsOf(SelectedShape);if(bounds.IsEmpty)return -1;
        var points=new[]
        {
            new Point(bounds.Left,bounds.Top),new Point(bounds.Left+bounds.Width/2,bounds.Top),new Point(bounds.Right,bounds.Top),
            new Point(bounds.Left,bounds.Top+bounds.Height/2),new Point(bounds.Right,bounds.Top+bounds.Height/2),
            new Point(bounds.Left,bounds.Bottom),new Point(bounds.Left+bounds.Width/2,bounds.Bottom),new Point(bounds.Right,bounds.Bottom)
        };
        for(int i=0;i<points.Length;i++)if((point-points[i]).Length<=9)return i;
        return -1;
    }
    int shapeHandle=-1;Point shapeStart;Rect shapeStartBounds;Point[] shapeArrowFromTo=null!;
    void BeginShapeDrag(UIElement el,int handle,Point point)
    {
        CommitTyping();
        if(!ReferenceEquals(el,SelectedShape))Select(el);
        shapeHandle=handle;shapeStart=point;shapeStartBounds=BoundsOf(el);
        shapeDragActive=true;
        if(el is Path{Tag:Point[] ft})shapeArrowFromTo=ft;
        CaptureMouse();
    }
    void ApplyShapeDrag(Point point)
    {
        var el=SelectedShape;if(el==null)return;
        var d=point-shapeStart;
        if(shapeHandle<0) // 移动
        {
            switch(el)
            {
                case Rectangle r:SetLeft(r,shapeStartBounds.Left+d.X);SetTop(r,shapeStartBounds.Top+d.Y);break;
                case Ellipse e:SetLeft(e,shapeStartBounds.Left+d.X);SetTop(e,shapeStartBounds.Top+d.Y);break;
                case Path a when shapeArrowFromTo!=null:
                    a.Tag=new[]{shapeArrowFromTo[0]+d,shapeArrowFromTo[1]+d};
                    a.Data=ArrowGeometry(shapeArrowFromTo[0]+d,shapeArrowFromTo[1]+d,ArrowHead(a));break;
                default:return;
            }
        }
        else // 缩放：手柄拖到哪，边界到哪
        {
            double l=shapeStartBounds.Left,r=shapeStartBounds.Right,t=shapeStartBounds.Top,b=shapeStartBounds.Bottom;
            if(shapeHandle is 0 or 3 or 5)l=Math.Min(point.X,r-8); // W
            if(shapeHandle is 2 or 4 or 7)r=Math.Max(point.X,l+8); // E
            if(shapeHandle is 0 or 1 or 2)t=Math.Min(point.Y,b-8); // N
            if(shapeHandle is 5 or 6 or 7)b=Math.Max(point.Y,t+8); // S
            if(r-l<8||b-t<8)return;
            var target=new Rect(l,t,r-l,b-t);
            switch(el)
            {
                case Rectangle rect:SetLeft(rect,target.Left);SetTop(rect,target.Top);rect.Width=target.Width;rect.Height=target.Height;break;
                case Ellipse e:SetLeft(e,target.Left);SetTop(e,target.Top);e.Width=target.Width;e.Height=target.Height;break;
                case Path a when shapeArrowFromTo!=null:
                    var from=shapeStartBounds;var fx=target.Width/Math.Max(1,from.Width);var fy=target.Height/Math.Max(1,from.Height);
                    var p0=new Point(target.X+(shapeArrowFromTo[0].X-from.X)*fx,target.Y+(shapeArrowFromTo[0].Y-from.Y)*fy);
                    var p1=new Point(target.X+(shapeArrowFromTo[1].X-from.X)*fx,target.Y+(shapeArrowFromTo[1].Y-from.Y)*fy);
                    a.Tag=new[]{p0,p1};a.Data=ArrowGeometry(p0,p1,ArrowHead(a));break;
                default:return;
            }
        }
        RefreshShapeOverlay();
    }
    static double ArrowHead(Path arrow)=>arrow.StrokeThickness>=1?Math.Max(12,arrow.StrokeThickness*4):16;
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var point=e.GetPosition(this);
        if(Current==Tool.None){e.Handled=false;return;}
        if(Current==Tool.Text){BeginText(point);e.Handled=true;return;}
        if(Current==Tool.Watermark){CommitTyping();e.Handled=true;return;} // 水印自动铺满，点击仅提交输入
        // 已选中图形：优先命中手柄（缩放）与本体（移动）
        if(SelectedShape!=null)
        {
            var handle=HandleHit(point);
            if(handle>=0){BeginShapeDrag(SelectedShape,handle,point);e.Handled=true;return;}
        }
        var hit=ShapeAt(point);
        if(hit!=null){BeginShapeDrag(hit,-1,point);e.Handled=true;return;}
        if(SelectedShape!=null)Deselect(); // 点击空白开始新绘制前取消选择
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
        if(active!=null){SetLeft(active,0);SetTop(active,0);Children.Add(active);RaiseOverlay();}
        e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(shapeDragActive){ApplyShapeDrag(e.GetPosition(this));return;}
        if(active==null)
        {
            // 光标提示：手柄=缩放，图形本体=移动
            if(SelectedShape!=null)
            {
                var handle=HandleHit(e.GetPosition(this));
                Cursor hc=handle switch{1 or 6=>Cursors.SizeNS,3 or 4=>Cursors.SizeWE,0 or 2=>Cursors.SizeNWSE,5 or 7=>Cursors.SizeNESW,_=>Cursors.Cross};
                Cursor=handle>=0?hc:ShapeAt(e.GetPosition(this))!=null?Cursors.SizeAll:null;
            }
            return;
        }
        var point=e.GetPosition(this);
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
    bool shapeDragActive;
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(shapeDragActive){shapeDragActive=false;shapeStartBounds=default;shapeHandle=-1;if(IsMouseCaptured)ReleaseMouseCapture();e.Handled=true;return;}
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
                    Children.Add(mosaic);history.Add(mosaic);RaiseOverlay();Changed?.Invoke();
                }
            }
            active=null;e.Handled=true;return;
        }
        if(empty)Children.Remove(active);
        else
        {
            history.Add(active);
            if(active is Path arrow && arrow.Data!=null && !arrow.Data.Bounds.IsEmpty)arrow.Tag=new[]{start,point}; // 记录箭头端点，供选中拖动/缩放
            Changed?.Invoke();
        }
        active=null;e.Handled=true;
    }
    void BeginText(Point point)
    {
        CommitTyping();Deselect();
        var box=new TextBox{MinWidth=60,Background=Brushes.Transparent,BorderBrush=new SolidColorBrush(Color.FromArgb(140,255,255,255)),BorderThickness=new Thickness(0,0,0,1),Foreground=Stroke(Color),FontSize=FontSize,FontWeight=FontWeights.SemiBold,CaretBrush=Stroke(Color),Padding=new Thickness(2)};
        SetLeft(box,point.X);SetTop(box,point.Y-11);Children.Add(box);box.Focus();
        var editor=new TextEditor{Box=box,Pos=point};
        typing=editor;
        box.KeyDown+=(_,ev)=>{if(ev.Key==Key.Enter){CommitTyping();ev.Handled=true;}else if(ev.Key==Key.Escape){Children.Remove(box);typing=null;ev.Handled=true;}};
        box.LostKeyboardFocus+=(_,_)=>CommitTyping();
    }
    // 字号实时生效：工具条调节时同步更新正在输入的文本框。
    public void ApplyFontSize(double value)
    {
        FontSize=value;
        if(typing!=null)typing.Box.FontSize=value;
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
        var right=new Point(to.X-(unit.X*.7+unit.Y*.7)*head,to.Y-(unit.Y*.7-unit.X*.7)*head);
        var geometry=new StreamGeometry();using(var ctx=geometry.Open()){ctx.BeginFigure(from,false,false);ctx.LineTo(to,true,false);ctx.BeginFigure(left,false,false);ctx.LineTo(to,true,false);ctx.LineTo(right,true,false);}
        geometry.Freeze();return geometry;
    }
    // 马赛克生成：物理像素区域 → 降采样再最近邻放大，得到打码位图。
    internal static ImageSource? Pixelate(BitmapSource source,Rect physical,int strength=14)
    {
        int x=(int)Math.Round(physical.Left),y=(int)Math.Round(physical.Top),w=(int)Math.Round(physical.Width),h=(int)Math.Round(physical.Height);
        x=Math.Max(0,Math.Min(x,source.PixelWidth-4));y=Math.Max(0,Math.Min(y,source.PixelHeight-4));
        w=Math.Max(4,Math.Min(w,source.PixelWidth-x));h=Math.Max(4,Math.Min(h,source.PixelHeight-y));
        try
        {
            var crop=new CroppedBitmap(source,new Int32Rect(x,y,w,h));
            int block=Math.Max(6,Math.Min(w,h)/Math.Max(1,strength));
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
