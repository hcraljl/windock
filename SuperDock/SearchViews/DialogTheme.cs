using System.Windows;
using System.Windows.Media;
namespace SuperDock.SearchViews;
internal static class DialogTheme
{
    public static void Attach(Window dialog)
    {
        GlassWindow.Prepare(dialog);dialog.Loaded+=(_,_)=>Apply(dialog);
    }
    static void Apply(Window dialog)
    {
        Window? current=dialog;string mode=Theme.Dark?"Dark":"Light";
        while(current!=null){if(current is SearchWindow search){mode=search.SearchTheme;break;}current=current.Owner;}
        Theme.Apply(mode);if(dialog.FindName("RootBorder") is System.Windows.Controls.Border root)root.CornerRadius=new CornerRadius(8);
        var r=dialog.Resources;r["SearchMenuFill"]=new SolidColorBrush(Theme.Dark?Color.FromRgb(36,39,45):Color.FromRgb(255,255,255));r["SearchMenuBorder"]=new SolidColorBrush(Theme.Dark?Color.FromRgb(66,71,80):Color.FromRgb(218,224,233));r["SearchMenuHover"]=new SolidColorBrush(Theme.Dark?Color.FromRgb(59,74,94):Color.FromRgb(225,237,253));r["DialogBrush"]=GlassWindow.Surface;r["DialogBorderBrush"]=Theme.Stroke;
        r["PanelBrush"]=new SolidColorBrush(Theme.Dark?Color.FromArgb(70,32,37,45):Color.FromArgb(80,233,239,248));
        r["TileBrush"]=Theme.Card;r["TileHoverBrush"]=Theme.Selected;r["InputBrush"]=Theme.Card;r["IconTileBrush"]=Theme.Card;r["PreviewBrush"]=Theme.Card;r["LineBrush"]=Theme.Stroke;
        r["ScrollTrackBrush"]=Brushes.Transparent;r["ScrollThumbBrush"]=new SolidColorBrush(Color.FromArgb(90,124,146,172));r["ScrollThumbHoverBrush"]=Theme.Muted;
        r["TextBrush"]=Theme.Text;r["MutedBrush"]=Theme.Muted;r["AccentBrush"]=new SolidColorBrush(Theme.Dark?Color.FromRgb(126,189,255):Color.FromRgb(24,117,222));r["AccentSoftBrush"]=Theme.Selected;r["PrimaryBrush"]=r["AccentBrush"];r["PrimaryTextBrush"]=Brushes.White;
    }
}
