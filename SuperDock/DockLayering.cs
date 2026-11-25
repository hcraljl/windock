using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SuperDock;
public partial class MainWindow
{
    DateTime raisedUntil;
    internal bool IsLayerLowered => !Topmost && settings.SmartLayering;
    [StructLayout(LayoutKind.Sequential)] struct LayerRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out LayerRect rect);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    internal void RaiseDock() { raisedUntil=DateTime.UtcNow.AddSeconds(2); Topmost=true; }
    void UpdateSmartLayer()
    {
        if(!IsLoaded || !IsVisible || closed || manuallyHidden) return;
        if(!settings.SmartLayering){if(!Topmost)Topmost=true;return;}
        if(IsMouseOver || openMenus>0 || preview?.IsMouseOver==true || DateTime.UtcNow<raisedUntil){if(!Topmost)Topmost=true;return;}
        if(DesktopIntegration.Cursor(out var cursor)){
            var area=PhysicalWorkArea;var dpi=VisualTreeHelper.GetDpi(this);
            var origin=PointToScreen(new Point());bool vertical=settings.Edge is "Left" or "Right";
            bool along=vertical?cursor.Y>=origin.Y&&cursor.Y<=origin.Y+ActualHeight*dpi.DpiScaleY:cursor.X>=origin.X&&cursor.X<=origin.X+ActualWidth*dpi.DpiScaleX;
            bool edge=settings.Edge switch{"Left"=>Math.Abs(cursor.X-area.Left)<=5,"Right"=>Math.Abs(cursor.X-area.Right)<=5,"Top"=>Math.Abs(cursor.Y-area.Top)<=5,_=>Math.Abs(cursor.Y-area.Bottom)<=5};
            if(along&&edge){RaiseDock();return;}
        }
        var foreground=RunningWindows.ExternalForeground();if(foreground==IntPtr.Zero)return;
        var name=new StringBuilder(128);GetClassName(foreground,name,name.Capacity);
        if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd"){if(!Topmost)Topmost=true;return;}
        if(!GetWindowRect(foreground,out var rect))return;
        var screen=PointToScreen(new Point());var scale=VisualTreeHelper.GetDpi(this);
        bool covered=new Rect(rect.Left,rect.Top,Math.Max(0,rect.Right-rect.Left),Math.Max(0,rect.Bottom-rect.Top)).IntersectsWith(new Rect(screen.X,screen.Y,ActualWidth*scale.DpiScaleX,ActualHeight*scale.DpiScaleY));
        if(Topmost==covered)Topmost=!covered;
    }
}
