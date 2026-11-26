using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace SuperDock;
// 截图工具条（参考样式）：单行图标 + 可选的选项子行。
// 行一：拖动手柄 | 普通模式◐ 矩形 椭圆 箭头 画笔 马赛克 文本 水印 | 贴图/滚动截图/提取文字 | 撤销 | 保存/退出/复制。
// 行二：随选中工具切换的选项（形状=颜色+粗细；马赛克=强度；文本=字号+颜色；水印=内容+透明度；普通=隐藏）。
internal static class AnnotationToolbar
{
    static readonly (string Name,Color Color)[] Palette=
    [
        ("红",Color.FromRgb(240,82,82)),("黄",Color.FromRgb(245,185,66)),("蓝",Color.FromRgb(35,125,245)),("绿",Color.FromRgb(52,180,110)),("白",Color.FromRgb(255,255,255)),("灰",Color.FromRgb(142,148,156)),("黑",Color.FromRgb(40,44,52))
    ];
    static readonly Brush IconFg=new SolidColorBrush(Color.FromRgb(36,54,77)); // 深色图标
    static readonly Brush SelectedBrush=new SolidColorBrush(Color.FromRgb(214,233,255)); // 选中：浅蓝底
    static readonly Brush HoverBrush=new SolidColorBrush(Color.FromArgb(20,0,0,0));
    public static FrameworkElement Build(AnnotationLayer layer,Window owner,Action<Vector> onGripDrag,int undoAfter,params FrameworkElement[] extras)
    {
        var row=new StackPanel{Orientation=Orientation.Vertical};
        var tools=new StackPanel{Orientation=Orientation.Horizontal};
        var optionsHost=new ContentControl{VerticalAlignment=VerticalAlignment.Center};
        var toolChips=new Dictionary<Border,AnnotationLayer.Tool>();
        Border? hovered=null;
        void Restyle()
        {
            foreach(var(chip,tool) in toolChips)
            {
                bool selected=layer.Current==tool;
                chip.Background=ReferenceEquals(chip,hovered)&&!selected?HoverBrush:selected?SelectedBrush:Brushes.Transparent;
            }
            foreach(var child in tools.Children)
            {
                if(child is Border{Tag:"undo"} chip)
                {
                    chip.Opacity=layer.HasDrawings?1:.4;
                    chip.IsHitTestVisible=layer.HasDrawings;
                    if(!ReferenceEquals(chip,hovered))chip.Background=Brushes.Transparent;
                }
            }
        }
        Border Chip(string tip,FrameworkElement icon,Action action)
        {
            var chip=new Border{Height=30,MinWidth=32,CornerRadius=new CornerRadius(7),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=new ContentPresenter{Content=icon,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},ToolTip=tip};
            chip.MouseEnter+=(_,_)=>{hovered=chip;Restyle();};
            chip.MouseLeave+=(_,_)=>{hovered=null;Restyle();};
            chip.MouseLeftButtonUp+=(_,_)=>action();
            return chip;
        }
        void Select(AnnotationLayer.Tool tool) // 选择工具并刷新选项子行
        {
            layer.Current=layer.Current==tool?AnnotationLayer.Tool.None:tool;
            Restyle();UpdateOptions();
        }
        void ToolButton(string glyph,string tip,AnnotationLayer.Tool tool,FrameworkElement? icon=null)
        {
            var chip=Chip(tip,icon??Icon(glyph),()=>Select(tool));
            toolChips[chip]=tool;tools.Children.Add(chip);
        }
        FrameworkElement ToolIcon(FrameworkElement inner)=>new ContentPresenter{Content=inner,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        // 普通模式（半圆）：未选标注工具，可整框拖动选区
        var normalIcon=new Grid{Width=16,Height=16};
        normalIcon.Children.Add(new Ellipse{Width=16,Height=16,Fill=Brushes.Transparent,Stroke=IconFg,StrokeThickness=1.6});
        normalIcon.Children.Add(new Ellipse{Width=16,Height=16,Fill=new SolidColorBrush(Color.FromRgb(120,126,136)),StrokeThickness=0,Opacity=.85,Margin=new Thickness(8,1,1,1)});
        ToolButton("","普通模式（可拖动选区）",AnnotationLayer.Tool.None,icon:normalIcon);
        ToolButton("","矩形",AnnotationLayer.Tool.Rect,icon:new Rectangle{Width=15,Height=15,RadiusX=3.5,RadiusY=3.5,Stroke=IconFg,StrokeThickness=1.6,Fill=Brushes.Transparent});
        ToolButton("","椭圆",AnnotationLayer.Tool.Ellipse,icon:EllipseIcon());
        ToolButton("\uE72A","箭头",AnnotationLayer.Tool.Arrow);
        ToolButton("\uE70F","画笔",AnnotationLayer.Tool.Pen);
        // 马赛克：2×2 像素块
        var mosaicGrid=new Grid{RowDefinitions={new RowDefinition(),new RowDefinition()},ColumnDefinitions={new ColumnDefinition(),new ColumnDefinition()},Width=15,Height=15,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        for(int i=0;i<4;i++)
        {
            var block=new Rectangle{Width=6.2,Height=6.2,RadiusX=1.4,RadiusY=1.4,Fill=IconFg};
            Grid.SetRow(block,i/2);Grid.SetColumn(block,i%2);
            mosaicGrid.Children.Add(block);
        }
        ToolButton("","马赛克",AnnotationLayer.Tool.Mosaic,icon:mosaicGrid);
        ToolButton("","文本",AnnotationLayer.Tool.Text,icon:new TextBlock{Text="T",FontSize=15,FontWeight=FontWeights.Bold,Foreground=IconFg,FontFamily=new FontFamily("Georgia, Segoe UI"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
        // 水印：四道斜杠
        var markIcon=new Canvas{Width=16,Height=16};
        for(int i=0;i<4;i++)
        {
            var line=new Line{X1=2+i*3.6,Y1=14,X2=6+i*3.6,Y2=2,Stroke=IconFg,StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
            markIcon.Children.Add(line);
        }
        ToolButton("","水印",AnnotationLayer.Tool.Watermark,icon:markIcon);
        tools.Children.Add(Separator());
        // —— 选项子行：随工具切换 ——
        FrameworkElement? ShapeOptions() // 形状/画笔共用：颜色 + 粗细
        {
            var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(2,2,0,0)};
            var current=new Ellipse{Width=15,Height=15,Fill=new SolidColorBrush(layer.Color),Stroke=new SolidColorBrush(Color.FromArgb(200,36,54,77)),StrokeThickness=1.6,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,6,0)};
            panel.Children.Add(current);
            var slider=new Slider{Minimum=2,Maximum=8,Value=layer.StrokeWidth,Width=90,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
            slider.ValueChanged+=(_,_)=>layer.StrokeWidth=slider.Value;
            panel.Children.Add(slider);
            foreach(var(_,color) in Palette)
            {
                var swatch=new Border{Width=17,Height=17,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(color),BorderBrush=new SolidColorBrush(Color.FromArgb(110,36,54,77)),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,Margin=new Thickness(3,0,3,0),Tag=color,VerticalAlignment=VerticalAlignment.Center};
                swatch.MouseEnter+=(_,_)=>swatch.Opacity=.8;
                swatch.MouseLeave+=(_,_)=>swatch.Opacity=1;
                swatch.MouseLeftButtonUp+=(_,_)=>{layer.Color=color;RestyleColors();};
                panel.Children.Add(swatch);
            }
            void RestyleColors()
            {
                current.Fill=new SolidColorBrush(layer.Color);
                foreach(var child in panel.Children)
                {
                    if(child is Border{Tag:Color color} swatch)
                    {
                        bool selected=layer.Color==color;
                        swatch.BorderThickness=new Thickness(selected?2:1);
                        swatch.BorderBrush=selected?SelectedBrush:new SolidColorBrush(Color.FromArgb(110,36,54,77));
                    }
                }
            }
            RestyleColors();
            return panel;
        }
        FrameworkElement MosaicOptions()
        {
            var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(2,2,0,0)};
            panel.Children.Add(new TextBlock{Text="强度",FontSize=12,Foreground=IconFg,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,8,0)});
            var slider=new Slider{Minimum=6,Maximum=30,Value=layer.MosaicStrength,Width=110,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
            slider.ValueChanged+=(_,_)=>layer.MosaicStrength=(int)slider.Value;
            panel.Children.Add(slider);
            return panel;
        }
        FrameworkElement TextOptions()
        {
            var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(2,2,0,0)};
            panel.Children.Add(new Border{Width=28,Height=24,CornerRadius=new CornerRadius(5),Child=new TextBlock{Text="T",FontSize=13,FontWeight=FontWeights.Bold,Foreground=IconFg,FontFamily=new FontFamily("Georgia, Segoe UI"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,6,0)});
            var size=new ComboBox{Width=76,Height=26,FontSize=12,VerticalContentAlignment=VerticalAlignment.Center,SelectedIndex=2};
            foreach(var v in new[]{12,14,16,18,20,24})size.Items.Add(new ComboBoxItem{Content=v.ToString()});
            size.SelectionChanged+=(_,_)=>{if(size.SelectedItem is ComboBoxItem item&&int.TryParse(item.Content+"",out var v))layer.FontSize=v;};
            panel.Children.Add(size);
            panel.Children.Add(new Border{Width=1,Height=16,Background=new SolidColorBrush(Color.FromArgb(40,36,54,77)),Margin=new Thickness(8,0,8,0),VerticalAlignment=VerticalAlignment.Center});
            var current=new Ellipse{Width=15,Height=15,Fill=new SolidColorBrush(layer.Color),Stroke=new SolidColorBrush(Color.FromArgb(200,36,54,77)),StrokeThickness=1.6,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)};
            panel.Children.Add(current);
            foreach(var(_,color) in Palette)
            {
                var swatch=new Border{Width=17,Height=17,CornerRadius=new CornerRadius(9),Background=new SolidColorBrush(color),BorderBrush=new SolidColorBrush(Color.FromArgb(110,36,54,77)),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,Margin=new Thickness(3,0,3,0),Tag=color,VerticalAlignment=VerticalAlignment.Center};
                swatch.MouseLeftButtonUp+=(_,_)=>{layer.Color=color;};
                panel.Children.Add(swatch);
            }
            return panel;
        }
        FrameworkElement WatermarkOptions()
        {
            var panel=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(2,2,0,0)};
            var input=new TextBox{Width=110,Height=26,FontSize=12,Padding=new Thickness(6,1,6,1),VerticalContentAlignment=VerticalAlignment.Center,Text=layer.WatermarkText};
            input.TextChanged+=(_,_)=>layer.WatermarkText=input.Text;
            panel.Children.Add(input);
            panel.Children.Add(new TextBlock{Text="透明度",FontSize=12,Foreground=IconFg,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,8,0)});
            var slider=new Slider{Minimum=10,Maximum=80,Value=layer.WatermarkOpacity*100,Width=110,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
            slider.ValueChanged+=(_,_)=>layer.WatermarkOpacity=slider.Value/100;
            panel.Children.Add(slider);
            return panel;
        }
        void UpdateOptions()
        {
            optionsHost.Content=layer.Current switch
            {
                AnnotationLayer.Tool.Pen or AnnotationLayer.Tool.Rect or AnnotationLayer.Tool.Ellipse or AnnotationLayer.Tool.Arrow=>ShapeOptions(),
                AnnotationLayer.Tool.Mosaic=>MosaicOptions(),
                AnnotationLayer.Tool.Text=>TextOptions(),
                AnnotationLayer.Tool.Watermark=>WatermarkOptions(),
                _=>null
            };
            optionsHost.Visibility=optionsHost.Content==null?Visibility.Collapsed:Visibility.Visible;
            owner.Dispatcher.BeginInvoke(new Action(()=>{if(owner is CaptureWindow cw)cw.OnToolbarLayoutChanged();else if(owner is PinnedImage pi)pi.OnToolbarLayoutChanged();}),DispatcherPriority.Loaded);
        }
        // 拖动手柄：按住拖动整个工具条
        var grip=new Border{Width=24,Height=30,CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,Cursor=Cursors.SizeAll,Child=BuildGrip(),VerticalAlignment=VerticalAlignment.Center};
        var gripStart=new Point();var gripDragging=false;
        grip.MouseLeftButtonDown+=(_,e)=>{gripDragging=true;gripStart=e.GetPosition(owner);grip.CaptureMouse();e.Handled=true;};
        grip.MouseMove+=(_,e)=>{if(!gripDragging)return;var p=e.GetPosition(owner);onGripDrag(new Vector(p.X-gripStart.X,p.Y-gripStart.Y));gripStart=p;e.Handled=true;};
        grip.MouseLeftButtonUp+=(_,e)=>{gripDragging=false;grip.ReleaseMouseCapture();e.Handled=true;};
        tools.Children.Insert(0,grip);
        tools.Children.Insert(1,Separator());
        // 撤销（插入到第 undoAfter 个动作图标之后）
        var undo=Chip("撤销",Icon("\uE7A7"),()=>layer.Undo());undo.Tag="undo";
        tools.Children.Add(undo);
        tools.Children.Add(Separator());
        foreach(var extra in extras)tools.Children.Add(extra);
        row.Children.Add(tools);
        row.Children.Add(optionsHost);
        void Refresh()=>Restyle();
        layer.Changed+=Refresh;layer.ColorChanged+=Refresh;
        // 把撤销移到动作组中第 undoAfter 个之后（与参考布局一致：工具组 | 贴图/滚动/提取 | 撤销 | 保存/退出/复制）
        if(undoAfter>0)
        {
            tools.Children.Remove(undo);
            int insertAt=1+1+8+1+Math.Min(undoAfter,extras.Length); // grip,sep,8 tools,sep,前 undoAfter 个 extras
            insertAt=Math.Min(insertAt,tools.Children.Count);
            tools.Children.Insert(insertAt,undo);
            tools.Children.Insert(insertAt+1,Separator());
        }
        UpdateOptions();
        Refresh();
        return row;
    }
    static FrameworkElement BuildGrip()
    {
        var grid=new Grid{RowDefinitions={new RowDefinition(),new RowDefinition(),new RowDefinition()},ColumnDefinitions={new ColumnDefinition(),new ColumnDefinition()},HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        for(int r=0;r<3;r++)for(int c=0;c<2;c++)
        {
            var dot=new Ellipse{Width=2.6,Height=2.6,Fill=new SolidColorBrush(Color.FromRgb(150,158,168))};
            Grid.SetRow(dot,r);Grid.SetColumn(dot,c);
            grid.Children.Add(dot);
        }
        return grid;
    }
    static FrameworkElement Icon(string glyph)=>new TextBlock{Text=glyph,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=15,Foreground=IconFg,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    static FrameworkElement EllipseIcon()=>new Ellipse{Width=14,Height=14,Stroke=IconFg,StrokeThickness=1.7,Fill=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    public static FrameworkElement Separator()=>new Border{Width=1,Height=18,Background=new SolidColorBrush(Color.FromArgb(40,36,54,77)),Margin=new Thickness(7,0,7,0),VerticalAlignment=VerticalAlignment.Center};
    // 动作图标：宿主创建后追加。
    public static FrameworkElement ActionChip(string glyph,string tip,Action action,Brush? foreground=null)
    {
        var chip=new Border{Height=30,MinWidth=32,CornerRadius=new CornerRadius(7),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=new ContentPresenter{Content=Icon(glyph),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},ToolTip=tip};
        if(foreground!=null&&chip.Child is ContentPresenter{Content:TextBlock t})t.Foreground=foreground;
        chip.MouseEnter+=(_,_)=>chip.Background=HoverBrush;
        chip.MouseLeave+=(_,_)=>chip.Background=Brushes.Transparent;
        chip.MouseLeftButtonUp+=(_,_)=>action();
        return chip;
    }
    public static FrameworkElement ActionChip(FrameworkElement icon,string tip,Action action)
    {
        var chip=new Border{Height=30,MinWidth=32,CornerRadius=new CornerRadius(7),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=new ContentPresenter{Content=icon,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},ToolTip=tip};
        chip.MouseEnter+=(_,_)=>chip.Background=HoverBrush;
        chip.MouseLeave+=(_,_)=>chip.Background=Brushes.Transparent;
        chip.MouseLeftButtonUp+=(_,_)=>action();
        return chip;
    }
    public static FrameworkElement ScanIcon()
    {
        var grid=new Grid{Width=15,Height=15};
        string[] data={"M 1,6 L1,1 L6,1","M 9,1 L14,1 L14,6","M 1,9 L1,14 L6,14","M 14,9 L14,14 L9,14"};
        foreach(var d in data)grid.Children.Add(new Path{Data=Geometry.Parse(d),Stroke=IconFg,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        return grid;
    }
}
