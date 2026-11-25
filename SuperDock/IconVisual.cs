using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
namespace SuperDock;
internal static class IconVisual
{
    public static FrameworkElement Search()=>GlassTile(new Path{Data=Geometry.Parse("M15,9 A6,6 0 1 1 3,9 A6,6 0 1 1 15,9 M13.5,13.5 L21,21"),Stroke=Brushes.White,StrokeThickness=2.1,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=24,Height=24},Color.FromRgb(51,154,196),34);
    public static FrameworkElement Exit()=>SystemTile(true);
    static FrameworkElement GlassTile(UIElement symbol,Color tint,double size)
    {
        var layers=new System.Windows.Controls.Grid{Width=size,Height=size};double radius=size*.27;
        var fill=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(.8,1)};
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(125,255,255,255),0));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(195,tint.R,tint.G,tint.B),.42));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(155,tint.R,tint.G,tint.B),.6));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(235,tint.R,tint.G,tint.B),1));
        var rim=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(230,255,255,255),0));rim.GradientStops.Add(new GradientStop(Color.FromArgb(40,255,255,255),.45));rim.GradientStops.Add(new GradientStop(Color.FromArgb(110,225,241,255),1));
        layers.Children.Add(new System.Windows.Controls.Border{CornerRadius=new CornerRadius(radius),Background=fill,BorderBrush=rim,BorderThickness=new Thickness(.9),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(6,16,32),BlurRadius=5,ShadowDepth=2,Opacity=.2}});
        layers.Children.Add(new System.Windows.Controls.Border{Margin=new Thickness(2),CornerRadius=new CornerRadius(Math.Max(1,radius-2)),BorderThickness=new Thickness(.5),BorderBrush=new LinearGradientBrush(Color.FromArgb(10,255,255,255),Color.FromArgb(65,255,255,255),90)});
        var glint=new LinearGradientBrush(Color.FromArgb(95,255,255,255),Colors.Transparent,90);
        layers.Children.Add(new System.Windows.Controls.Border{Margin=new Thickness(3,2,3,size*.54),CornerRadius=new CornerRadius(radius-2,radius-2,size*.17,size*.17),Background=glint,IsHitTestVisible=false});
        layers.Children.Add(new System.Windows.Controls.Viewbox{Margin=new Thickness(size*.23),Child=symbol});
        return layers;
    }
    public static FrameworkElement Badge(string geometry,Color tint,double size=34)
    {
        var lighter=Color.FromRgb((byte)Math.Min(255,tint.R+65),(byte)Math.Min(255,tint.G+55),(byte)Math.Min(255,tint.B+35));
        var canvas=AppBase(lighter.ToString(),tint.ToString());
        var symbol=new Path{Data=Geometry.Parse(geometry),Stroke=new LinearGradientBrush(Colors.White,Color.FromRgb(228,239,255),90),StrokeThickness=2.1,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=1.5,ShadowDepth=1,Opacity=.22}};
        canvas.Children.Add(new System.Windows.Controls.Viewbox{Width=36,Height=36,Margin=new Thickness(14),Child=symbol});
        return new System.Windows.Controls.Viewbox{Width=size,Height=size,Child=canvas};
    }    static System.Windows.Controls.Canvas AppBase(string top,string bottom)
    {
        var canvas=new System.Windows.Controls.Canvas{Width=64,Height=64};
        var tile=new System.Windows.Controls.Border{Width=62,Height=62,Margin=new Thickness(1),CornerRadius=new CornerRadius(15),Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top),(Color)ColorConverter.ConvertFromString(bottom),90),BorderBrush=new LinearGradientBrush(Color.FromArgb(225,255,255,255),Color.FromArgb(65,255,255,255),90),BorderThickness=new Thickness(1),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=4,ShadowDepth=1,Opacity=.25}};canvas.Children.Add(tile);return canvas;
    }
    static FrameworkElement Finish(System.Windows.Controls.Canvas canvas)=>new System.Windows.Controls.Viewbox{Width=36,Height=36,Child=canvas};
    public static FrameworkElement Applications()
    {
        var canvas=AppBase("#FCFDFF","#CDD8E8");
        var search=new System.Windows.Controls.Border{Width=42,Height=9,CornerRadius=new CornerRadius(4.5),Background=new LinearGradientBrush(Color.FromRgb(170,183,201),Color.FromRgb(193,204,219),90)};System.Windows.Controls.Canvas.SetLeft(search,11);System.Windows.Controls.Canvas.SetTop(search,10);canvas.Children.Add(search);
        string[] colors={"#41A5F5","#64C45B","#F06B87","#F3A13E","#A473D6","#66B7C7"};
        for(int i=0;i<6;i++){var color=(Color)ColorConverter.ConvertFromString(colors[i]);var cell=new System.Windows.Controls.Border{Width=11,Height=11,CornerRadius=new CornerRadius(3),Background=new LinearGradientBrush(Color.FromRgb((byte)Math.Min(255,color.R+35),(byte)Math.Min(255,color.G+35),(byte)Math.Min(255,color.B+35)),color,90),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255)),BorderThickness=new Thickness(.5)};System.Windows.Controls.Canvas.SetLeft(cell,12+i%3*15);System.Windows.Controls.Canvas.SetTop(cell,26+i/3*15);canvas.Children.Add(cell);}
        return Finish(canvas);
    }
    public static FrameworkElement Add()
    {
        var canvas=AppBase("#75D8FB","#2C6DDD");
        canvas.Children.Add(new Path{Data=Geometry.Parse("M18,32 L46,32 M32,18 L32,46"),Stroke=Brushes.White,StrokeThickness=5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=2,ShadowDepth=1,Opacity=.25}});return Finish(canvas);
    }
    static FrameworkElement SystemTile(bool exit)
    {
        var canvas=AppBase(exit?"#F5B6AE":"#DCE3EB",exit?"#CF626E":"#8B99AF");
        if(exit)canvas.Children.Add(new Path{Data=Geometry.Parse("M32,14 L32,31 M23,20 A17,17 0 1 0 41,20"),Stroke=Brushes.White,StrokeThickness=4,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        else
        {
            var geometry=new StreamGeometry{FillRule=FillRule.EvenOdd};using(var ctx=geometry.Open()){for(int i=0;i<48;i++){double r=i%4 is 0 or 3?24:20;double a=i*Math.PI/24-Math.PI/2;var point=new Point(32+r*Math.Cos(a),32+r*Math.Sin(a));if(i==0)ctx.BeginFigure(point,true,true);else ctx.LineTo(point,true,false);}}
            var ring=new GeometryGroup{FillRule=FillRule.EvenOdd};ring.Children.Add(geometry);ring.Children.Add(new EllipseGeometry(new Point(32,32),12,12));
            canvas.Children.Add(new Path{Data=ring,Fill=new LinearGradientBrush(Color.FromRgb(100,113,132),Color.FromRgb(55,66,85),90),Stroke=new SolidColorBrush(Color.FromRgb(230,237,247)),StrokeThickness=1,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=2,ShadowDepth=1,Opacity=.3}});
            canvas.Children.Add(new Ellipse{Width=15,Height=15,Margin=new Thickness(24.5),Fill=new LinearGradientBrush(Colors.White,Color.FromRgb(164,179,199),90)});
        }
        return Finish(canvas);
    }    public static FrameworkElement Settings(bool windows)
    {
        if(!windows)return SystemTile(false);
        var geometry=windows ? "M2,3 L10,2 L10,10 L2,10 Z M12,2 L22,1 L22,10 L12,10 Z M2,12 L10,12 L10,20 L2,19 Z M12,12 L22,12 L22,22 L12,21 Z" : "M3,6 L21,6 M3,12 L21,12 M3,18 L21,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21";
        return new Path { Data=Geometry.Parse(geometry),Stroke=windows?new SolidColorBrush(Color.FromRgb(97,190,255)):Brushes.White,StrokeThickness=1.8,Fill=windows?new SolidColorBrush(Color.FromRgb(97,190,255)):Brushes.Transparent,Stretch=Stretch.Uniform,Width=windows?30:25,Height=windows?30:25,SnapsToDevicePixels=true };
    }
}










