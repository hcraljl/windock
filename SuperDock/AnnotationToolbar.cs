using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace SuperDock;
// 标注工具条（钉钉风格：深色底、白色线性图标、蓝色选中高亮），截图与贴图共用。
// 行一：涂鸦/矩形/椭圆/箭头/马赛克/文字 + 撤销/清空 + 动作（贴图/保存/复制/取消/完成）。
// 行二：当前颜色指示 + 8 色色板 + 3 档粗细。
internal static class AnnotationToolbar
{
    static readonly (string Name,Color Color)[] Palette=
    [
        ("红",Color.FromRgb(255,90,90)),("橙",Color.FromRgb(255,166,77)),("黄",Color.FromRgb(255,214,102)),("绿",Color.FromRgb(102,214,148)),("蓝",Color.FromRgb(96,171,255)),("紫",Color.FromRgb(186,150,255)),("白",Color.FromRgb(255,255,255)),("黑",Color.FromRgb(40,44,52))
    ];
    static readonly double[] Widths=[2,4,6];
    static readonly Brush IconFg=new SolidColorBrush(Color.FromRgb(240,244,248));
    static readonly Brush SelectedBrush=new SolidColorBrush(Color.FromRgb(35,125,245));
    static readonly Brush HoverBrush=new SolidColorBrush(Color.FromArgb(46,255,255,255));
    // build: layer=标注画布；owner=承载工具条的窗口；extras=行一末尾的动作图标（贴图/保存/复制/取消/完成）。
    public static FrameworkElement Build(AnnotationLayer layer,Window owner,params FrameworkElement[] extras)
    {
        var row=new StackPanel{Orientation=Orientation.Vertical,Tag=owner};
        var tools=new StackPanel{Orientation=Orientation.Horizontal};
        var colors=new StackPanel{Orientation=Orientation.Horizontal};
        var widths=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,0)};
        var current=new Ellipse{Width=15,Height=15,Fill=new SolidColorBrush(layer.Color),Stroke=new SolidColorBrush(Color.FromArgb(220,255,255,255)),StrokeThickness=1.6,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,8,0)};
        var toolChips=new Dictionary<Border,AnnotationLayer.Tool>();
        Border? hovered=null;
        void Restyle() // 统一底色：选中蓝色、悬停提亮、其余透明。
        {
            foreach(var(chip,tool) in toolChips)chip.Background=ReferenceEquals(chip,hovered)?HoverBrush:layer.Current==tool?SelectedBrush:Brushes.Transparent;
            foreach(var child in tools.Children)
            {
                if(child is Border{Tag:string tag} chip&&(tag=="undo"||tag=="clear"))
                {
                    chip.Opacity=layer.HasDrawings?1:.4;
                    chip.IsHitTestVisible=layer.HasDrawings;
                    if(!ReferenceEquals(chip,hovered))chip.Background=Brushes.Transparent;
                }
            }
        }
        Border Chip(string tip,FrameworkElement icon,Action action)
        {
            var chip=new Border{Height=28,MinWidth=30,CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=new ContentPresenter{Content=icon,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},ToolTip=tip};
            chip.MouseEnter+=(_,_)=>{hovered=chip;Restyle();};
            chip.MouseLeave+=(_,_)=>{hovered=null;Restyle();};
            chip.MouseLeftButtonUp+=(_,_)=>action();
            return chip;
        }
        void ToolButton(string glyph,string tip,AnnotationLayer.Tool tool,FrameworkElement? icon=null)
        {
            var chip=Chip(tip,icon??Icon(glyph),()=>{
                layer.Current=layer.Current==tool?AnnotationLayer.Tool.None:tool;
                Restyle();
            });
            toolChips[chip]=tool;tools.Children.Add(chip);
        }
        ToolButton("\uE70F","涂鸦",AnnotationLayer.Tool.Pen);
        ToolButton("\uE8A5","矩形",AnnotationLayer.Tool.Rect);
        ToolButton("\uE91D","椭圆",AnnotationLayer.Tool.Ellipse,icon:EllipseIcon());
        ToolButton("\uE72A","箭头",AnnotationLayer.Tool.Arrow);
        ToolButton("\uE72E","马赛克",AnnotationLayer.Tool.Mosaic);
        ToolButton("\uE70B","文字",AnnotationLayer.Tool.Text);
        tools.Children.Add(Separator());
        var undo=Chip("撤销",Icon("\uE7A7"),()=>layer.Undo());undo.Tag="undo";tools.Children.Add(undo);
        var clear=Chip("清空标注",Icon("\uE74D"),()=>layer.ClearAll());clear.Tag="clear";tools.Children.Add(clear);
        tools.Children.Add(Separator());
        foreach(var extra in extras)tools.Children.Add(extra);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,4,0,0)};
        actions.Children.Add(current);
        foreach(var(_,color) in Palette)
        {
            var swatch=new Border{Width=17,Height=17,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(color),BorderBrush=new SolidColorBrush(Color.FromArgb(80,255,255,255)),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,Margin=new Thickness(3,0,3,0),Tag=color,VerticalAlignment=VerticalAlignment.Center};
            swatch.MouseEnter+=(_,_)=>swatch.Opacity=.85;
            swatch.MouseLeave+=(_,_)=>swatch.Opacity=1;
            swatch.MouseLeftButtonUp+=(_,_)=>{layer.Color=color;RestyleColors();};
            colors.Children.Add(swatch);
        }
        actions.Children.Add(colors);
        foreach(var w in Widths)
        {
            var dot=new Border{Width=22,Height=20,CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,Cursor=Cursors.Hand,Margin=new Thickness(2,0,2,0),Tag=w,VerticalAlignment=VerticalAlignment.Center,Child=new Ellipse{Width=w+3,Height=w+3,Fill=IconFg,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
            dot.MouseEnter+=(_,_)=>{if(layer.StrokeWidth!=w)dot.Background=HoverBrush;};
            dot.MouseLeave+=(_,_)=>{if(layer.StrokeWidth!=w)dot.Background=Brushes.Transparent;};
            dot.MouseLeftButtonUp+=(_,_)=>{layer.StrokeWidth=w;RestyleColors();};
            widths.Children.Add(dot);
        }
        actions.Children.Add(widths);
        row.Children.Add(tools);
        row.Children.Add(actions);
        void RestyleColors()
        {
            current.Fill=new SolidColorBrush(layer.Color);
            foreach(var child in colors.Children)
            {
                if(child is Border{Tag:Color color} swatch)
                {
                    bool selected=layer.Color==color;
                    swatch.BorderThickness=new Thickness(selected?2:1);
                    swatch.BorderBrush=selected?Brushes.White:new SolidColorBrush(Color.FromArgb(80,255,255,255));
                }
            }
            foreach(var child in widths.Children)
            {
                if(child is Border{Tag:double width} dot)dot.Background=layer.StrokeWidth==width?SelectedBrush:Brushes.Transparent;
            }
        }
        void Refresh()
        {
            Restyle();
            RestyleColors();
        }
        layer.Changed+=Refresh;layer.ColorChanged+=Refresh;row.Tag=Refresh;Refresh();
        return row;
    }
    static FrameworkElement Icon(string glyph)=>new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=14.5,Foreground=IconFg,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    static FrameworkElement EllipseIcon()=>new Ellipse{Width=13,Height=13,Stroke=IconFg,StrokeThickness=1.7,Fill=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    static FrameworkElement Separator()=>new Border{Width=1,Height=16,Background=new SolidColorBrush(Color.FromArgb(45,255,255,255)),Margin=new Thickness(6,0,6,0),VerticalAlignment=VerticalAlignment.Center};
    // 动作图标（钉钉样式）：宿主创建后追加到行一末尾。
    public static FrameworkElement ActionChip(string glyph,string tip,Action action)
    {
        var chip=new Border{Height=28,MinWidth=30,CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=new ContentPresenter{Content=Icon(glyph),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},ToolTip=tip};
        chip.MouseEnter+=(_,_)=>chip.Background=HoverBrush;
        chip.MouseLeave+=(_,_)=>chip.Background=Brushes.Transparent;
        chip.MouseLeftButtonUp+=(_,_)=>action();
        return chip;
    }
}
