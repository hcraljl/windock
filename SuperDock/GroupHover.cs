using System.Windows;
using System.Windows.Input;

namespace SuperDock;
public partial class MainWindow
{
    AppGroup? groupHoverCandidate;
    DateTime groupHoverSince;
    bool groupHoverOpened;

    void CheckGroupHover()
    {
        AppGroup? candidate=null;
        // Do not expand groups during drag, long press, or a context-menu operation.
        if(IsLoaded&&IsVisible&&!closed&&!hidden&&!manuallyHidden&&Topmost&&
            groupFlyout?.HasContextMenu!=true&&Mouse.LeftButton==MouseButtonState.Released&&Mouse.RightButton==MouseButtonState.Released&&
            openMenus<=(groupFlyout?.IsOpen==true?1:0)&&DesktopIntegration.Cursor(out var cursor))
        {
            foreach(var anchor in panel.Children.OfType<FrameworkElement>())
            {
                if(anchor.Tag is not string tag||!tag.StartsWith("group:"))continue;
                var start=anchor.PointToScreen(new Point());
                var end=anchor.PointToScreen(new Point(anchor.ActualWidth,anchor.ActualHeight));
                if(cursor.X>=start.X&&cursor.X<=end.X&&cursor.Y>=start.Y&&cursor.Y<=end.Y){
                    candidate=settings.Groups.FirstOrDefault(g=>tag=="group:"+g.Name);break;
                }
            }
        }
        if(!ReferenceEquals(candidate,groupHoverCandidate)){
            groupHoverCandidate=candidate;groupHoverSince=DateTime.UtcNow;groupHoverOpened=false;
        }
        if(candidate==null||groupHoverOpened||(DateTime.UtcNow-groupHoverSince).TotalMilliseconds<350)return;
        groupHoverOpened=true;
        GroupMenu(candidate,true);
    }
}
