using System.Windows;
using System.Windows.Media;
namespace SuperDock;
internal static class RoundClip
{
    public static readonly DependencyProperty RadiusProperty=DependencyProperty.RegisterAttached("Radius",typeof(double),typeof(RoundClip),new PropertyMetadata(0d,Changed));
    public static void SetRadius(DependencyObject target,double value)=>target.SetValue(RadiusProperty,value);
    public static double GetRadius(DependencyObject target)=>(double)target.GetValue(RadiusProperty);
    static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        if(target is not FrameworkElement element)return;element.SizeChanged-=SizeChanged;element.SizeChanged+=SizeChanged;Apply(element);
    }
    static void SizeChanged(object sender,SizeChangedEventArgs args)=>Apply((FrameworkElement)sender);
    static void Apply(FrameworkElement element){double radius=GetRadius(element);element.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,element.ActualWidth),Math.Max(0,element.ActualHeight)),radius,radius);}
}

