using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace SuperDock;
// 标注工具条：涂鸦/矩形/椭圆/箭头/文字 + 颜色与粗细 + 撤销/清空，截图与贴图共用。
internal static class AnnotationToolbar
{
    static readonly (string Name,Color Color)[] Palette=
    [
        ("红",Color.FromRgb(255,90,90)),("橙",Color.FromRgb(255,166,77)),("黄",Color.FromRgb(255,214,102)),("绿",Color.FromRgb(102,214,148)),("蓝",Color.FromRgb(96,171,255)),("紫",Color.FromRgb(186,150,255)),("白",Color.FromRgb(255,255,255)),("黑",Color.FromRgb(40,44,52))
    ];
    static readonly double[] Widths=[2,4,6];
    // build: layer=标注画布；owner=承载工具条的窗口；extras=追加到末尾的动作按钮（复制/保存/钉图/取消等）。
    public static FrameworkElement Build(AnnotationLayer layer,Window owner,params FrameworkElement[] extras)
    {
        var row=new StackPanel{Orientation=Orientation.Vertical,Tag=owner};
        var tools=new StackPanel{Orientation=Orientation.Horizontal};
        void ToolButton(string glyph,string tip,AnnotationLayer.Tool tool)
        {
            var button=Small(glyph,tip,()=>{layer.Current=layer.Current==tool?AnnotationLayer.Tool.None:tool;Refresh();});
            button.Tag=tool;tools.Children.Add(button);
        }
        ToolButton("\uE70F","涂鸦",AnnotationLayer.Tool.Pen);
        ToolButton("\uE8A5","矩形",AnnotationLayer.Tool.Rect);
        ToolButton("\uE91D","椭圆",AnnotationLayer.Tool.Ellipse);
        ToolButton("\uE72A","箭头",AnnotationLayer.Tool.Arrow);
        ToolButton("\uE70B","文字",AnnotationLayer.Tool.Text);
        var undo=Small("\uE7A7","撤销",()=>layer.Undo());undo.Tag="undo";tools.Children.Add(undo);
        var clear=Small("\uE74D","清空标注",()=>layer.ClearAll());clear.Tag="clear";tools.Children.Add(clear);
        var colors=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,0)};
        foreach(var(_,color) in Palette)
        {
            var swatch=new Border{Width=18,Height=18,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(color),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255)),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,Margin=new Thickness(3,0,3,0),Tag=color};
            swatch.MouseLeftButtonUp+=(_,_)=>{layer.Color=color;Refresh();};
            colors.Children.Add(swatch);
        }
        tools.Children.Add(colors);
        var widths=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,0)};
        foreach(var w in Widths)
        {
            var dot=new Border{Width=w+10,Height=18,CornerRadius=new CornerRadius(9),Cursor=Cursors.Hand,Margin=new Thickness(3,0,3,0),Tag=w,Child=new Ellipse{Width=w,Height=w,Fill=new SolidColorBrush(Color.FromArgb(235,238,242,248)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
            dot.MouseLeftButtonUp+=(_,_)=>{layer.StrokeWidth=w;Refresh();};
            widths.Children.Add(dot);
        }
        tools.Children.Add(widths);
        foreach(var extra in extras)tools.Children.Add(extra);
        row.Children.Add(tools);
        // Refresh 同步选中态：工具高亮、颜色描边、粗细底色。
        void Refresh()
        {
            foreach(var child in tools.Children)
            {
                if(child is Border{Tag:Color color} swatch)swatch.BorderBrush=layer.Color==color?new SolidColorBrush(Color.FromRgb(120,190,255)):new SolidColorBrush(Color.FromArgb(90,255,255,255));
                if(child is Border{Tag:double width} dot)dot.Background=layer.StrokeWidth==width?new SolidColorBrush(Color.FromArgb(70,255,255,255)):Brushes.Transparent;
            }
            foreach(var child in tools.Children)
            {
                if(child is Button{Tag:AnnotationLayer.Tool tool} button)button.Background=layer.Current==tool?Theme.Selected:GlassWindow.Surface;
            }
            if(tools.Children.OfType<FrameworkElement>().FirstOrDefault(e=>e.Tag as string=="undo") is Button undoButton)undoButton.IsEnabled=layer.HasDrawings;
            if(tools.Children.OfType<FrameworkElement>().FirstOrDefault(e=>e.Tag as string=="clear") is Button clearButton)clearButton.IsEnabled=layer.HasDrawings;
        }
        layer.Changed+=Refresh;layer.ColorChanged+=Refresh;row.Tag=Refresh;Refresh();
        return row;
    }
    static Button Small(string glyph,string tip,Action action)=>new()
    {
        Content=new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=14,Foreground=Theme.Text},
        Style=(Style)Application.Current.FindResource("GlassButton"),Width=32,Height=28,Padding=new Thickness(0),ToolTip=tip,Cursor=Cursors.Hand,Margin=new Thickness(2,0,2,0),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Background=GlassWindow.Surface
    };
}
