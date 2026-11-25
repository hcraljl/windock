using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
namespace SuperDock;
internal static class NotificationLogo
{
    public static ImageSource Create(string name)
    {
        if(name=="QQ")return new BitmapImage(new Uri("pack://application:,,,/Windock;component/Assets/NotificationLogos/qq.png"));
        if(name=="微信"){
            var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(7,193,96)),null,new RectangleGeometry(new Rect(0,0,40,40),9,9)));
            group.Children.Add(new GeometryDrawing(Brushes.White,null,Geometry.Parse("M9,12 C15,7 26,10 26,17 C26,23 18,27 12,24 L8,26 L9,22 C4,19 5,15 9,12 Z M20,20 C25,16 35,20 34,26 C34,30 30,33 26,32 L30,35 L30,31 C20,34 16,25 20,20 Z")));
            foreach(var point in new[]{new Point(12,16),new Point(20,16),new Point(25,24),new Point(30,24)})group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(7,153,75)),null,new EllipseGeometry(point,1,1)));
            group.Freeze();return new DrawingImage(group);
        }
        string file=name switch{"企业微信"=>"wecom", "钉钉"=>"dingtalk",_=>"feishu"};using var stream=Application.GetResourceStream(new Uri("/Windock;component/Assets/NotificationLogos/"+file+".svg",UriKind.Relative))!.Stream;var svg=XDocument.Load(stream).Root!;var drawing=Read(svg,Brushes.Transparent);
        if(name=="钉钉"){var background=new DrawingGroup();background.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0,137,255)),null,new RectangleGeometry(new Rect(0,0,32,32),7,7)));background.Children.Add(drawing);drawing=background;}
        drawing.Freeze();return new DrawingImage(drawing);
    }
    static DrawingGroup Read(XElement node,Brush inherited)
    {
        double Number(string key,double fallback=0)=>double.TryParse((string?)node.Attribute(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:fallback;
        var result=new DrawingGroup();string kind=node.Name.LocalName;
        if(kind is "defs" or "mask" or "use")return result;
        Brush fill=inherited;if(node.Attribute("fill") is { } attr){string color=attr.Value;fill=color=="none"?Brushes.Transparent:color.StartsWith("url")?Brushes.DeepSkyBlue:(Brush)new BrushConverter().ConvertFromString(color)!;}
        if(kind=="path")result.Children.Add(new GeometryDrawing(fill,null,Geometry.Parse((string?)node.Attribute("d")??"")));
        else if(kind=="rect")result.Children.Add(new GeometryDrawing(fill,null,new RectangleGeometry(new Rect(Number("x"),Number("y"),Number("width"),Number("height")),Number("rx"),Number("rx"))));
        foreach(var child in node.Elements())result.Children.Add(Read(child,fill));
        var transforms=new TransformGroup();if(kind=="svg"&&node.Attribute("viewBox") is { } box){var v=box.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(s=>double.Parse(s,CultureInfo.InvariantCulture)).ToArray();double sx=Number("width",v[2])/v[2],sy=Number("height",v[3])/v[3];transforms.Children.Add(new TranslateTransform(-v[0],-v[1]));transforms.Children.Add(new ScaleTransform(sx,sy));}
        if(node.Attribute("transform") is { } transform){var match=System.Text.RegularExpressions.Regex.Match(transform.Value,@"translate\(([-\d.]+)[ ,]+([-\d.]+)\)");if(match.Success)transforms.Children.Add(new TranslateTransform(double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)));}
        if(kind=="svg")transforms.Children.Add(new TranslateTransform(Number("x"),Number("y")));result.Transform=transforms;return result;
    }
}
