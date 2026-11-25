using System.Windows;
using System.Windows.Media;
namespace SuperDock;
internal static class Theme
{
    public static bool Dark {get;private set;}
    public static Brush Get(string key)=>(Brush)Application.Current.Resources[key];
    public static Brush Text=>Get("ThemeText");public static Brush Muted=>Get("ThemeMuted");public static Brush Card=>Get("ThemeCard");public static Brush Selected=>Get("ThemeSelected");public static Brush Stroke=>Get("ThemeStroke");public static Brush Fill=>Get("GlassFill");
    public static void Apply(string mode)
    {
        Dark=mode=="Dark";var r=Application.Current.Resources;
        Brush Solid(string light,string dark)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?dark:light));
        r["SwitchOff"]=Solid("#B9C3CF","#536070");r["SwitchOn"]=Solid("#1985F5","#439CFF");r["SwitchBorder"]=Solid("#258094AC","#408EA7C7");r["MenuGlass"]=Solid("#EAF4F7FC","#EC202329");r["ThemeText"]=Solid("#24364D","#EDF4FF");r["ThemeMuted"]=Solid("#52647B","#BECBDD");r["ThemeCard"]=Solid("#48FFFFFF","#5034373F");r["ThemeSelected"]=Solid("#907AB5FA","#905579A7");r["ThemeStroke"]=new LinearGradientBrush(Color.FromArgb(Dark?(byte)100:(byte)180,255,255,255),Color.FromArgb(Dark?(byte)25:(byte)55,190,207,230),70);r["ThemeHover"]=Solid("#60FFFFFF","#607893B4");r["ThemeTrack"]=Solid("#D1DFED","#41516B");r["ThemeDanger"]=Solid("#B0414E","#FF9CA6");
        var fill=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(Dark?"#FF2B2D33":"#FFF8F8FA"),(Color)ColorConverter.ConvertFromString(Dark?"#FF22242A":"#FFECEEF2"),45);r["GlassFill"]=fill;r["GlassStroke"]=r["ThemeStroke"];
    }
}






