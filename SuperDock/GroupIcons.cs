using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;
internal static class GroupIcons
{
    public static readonly (string Name,string Glyph,string Color)[] BuiltIn=[("工作","\uE821","#5589DF"),("游戏","\uE7FC","#9C64DD"),("娱乐","\uE714","#E37C9B"),("AI","\uE945","#5AABB2"),("提效","\uE945","#E4A34C"),("学习","\uE82D","#6C9F75"),("设计","\uE790","#D87858")];
    public static FrameworkElement Create(MainWindow.AppGroup group,double size=34)
    {
        if(!string.IsNullOrEmpty(group.CustomIcon)&&File.Exists(group.CustomIcon))try{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=128;image.UriSource=new Uri(group.CustomIcon,UriKind.Absolute);image.EndInit();image.Freeze();return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size*.24),Background=new ImageBrush(image){Stretch=Stretch.UniformToFill}};}catch(Exception ex) when(ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException){}
        var preset=BuiltIn.FirstOrDefault(i=>i.Name==group.Icon);if(preset==default)preset=BuiltIn[0];var color=(Color)ColorConverter.ConvertFromString(preset.Color);
        string shape=preset.Name switch
        {
            "工作"=>"M3,7 L21,7 L21,21 L3,21 Z M8,7 L8,3 L16,3 L16,7 M3,12 L21,12 M10,12 L10,15 L14,15 L14,12",
            "游戏"=>"M7,7 L17,7 Q21,7 22,13 L23,19 Q22,23 18,19 L16,16 L8,16 L5,20 Q1,22 1,18 L2,12 Q3,7 7,7 M5,11 L5,15 M3,13 L7,13 M16,11 L16,12 M19,13 L19,14",
            "娱乐"=>"M5,2 L5,22 L21,12 Z",
            "AI"=>"M12,1 Q13,10 23,12 Q13,14 12,23 Q10,14 1,12 Q10,10 12,1 Z",
            "提效"=>"M14,1 L4,14 L11,14 L9,23 L21,9 L14,9 Z",
            "学习"=>"M12,5 Q7,1 2,4 L2,21 Q7,18 12,22 Q17,18 22,21 L22,4 Q17,1 12,5 L12,22",
            _=>"M3,17 L16,4 L21,9 L8,22 L2,23 Z M13,7 L18,12"
        };
        return IconVisual.Badge(shape,color,size);
    }
}



