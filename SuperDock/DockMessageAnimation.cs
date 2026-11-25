using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace SuperDock;
public partial class MainWindow
{
    internal string DockEdge=>settings.Edge;
    internal double MessageReminderOpacity=>settings.MessageReminderOpacity;
    // 更新提醒气泡锚点：设置图标（无则回落到应用中心图标）。
    internal FrameworkElement? SettingsAnchor()=>panel.Children.OfType<FrameworkElement>().FirstOrDefault(e=>Equals(e.Tag,"action:settings")||Equals(e.Tag,"action:library"));
    internal FrameworkElement? MessageAnchor(string client)
    {
        bool Matches(Entry entry)=>MessageReminders.Client(RunningWindows.WindowTarget(entry.Target))==client;
        var entry=settings.Entries.FirstOrDefault(Matches);
        if(entry!=null)return panel.Children.OfType<FrameworkElement>().FirstOrDefault(e=>Equals(e.Tag,"app:"+entry.Target));
        var group=settings.Groups.FirstOrDefault(g=>g.Entries.Any(Matches));
        return panel.Children.OfType<FrameworkElement>().FirstOrDefault(e=>Equals(e.Tag,group!=null?"group:"+group.Name:"action:library"));
    }
    internal Action BeginMessage(FrameworkElement anchor)
    {
        bool visible=IsVisible;openMenus++;hideTimer.Stop();hidden=false;if(!visible)Show();Position();UpdateLayout();
        var face=anchor is ContentControl control?control.Content as FrameworkElement:anchor;
        var original=face?.RenderTransform;double opacity=face?.Opacity??1;
        TranslateTransform? jump=null;
        if(face!=null){jump=new TranslateTransform();var transforms=new TransformGroup();if(original!=null)transforms.Children.Add(original);transforms.Children.Add(jump);face.RenderTransform=transforms;
            var motion=new DoubleAnimation(0,settings.Edge is "Top" or "Left"?8:-8,TimeSpan.FromMilliseconds(240)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(5),EasingFunction=new SineEase()};jump.BeginAnimation(settings.Edge is "Left" or "Right"?TranslateTransform.XProperty:TranslateTransform.YProperty,motion);
            face.BeginAnimation(OpacityProperty,new DoubleAnimation(1,.62,TimeSpan.FromMilliseconds(350)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(4)});
        }
        bool ended=false;return ()=>{if(ended)return;ended=true;if(face!=null){face.BeginAnimation(OpacityProperty,null);face.Opacity=opacity;face.RenderTransform=original??Transform.Identity;}openMenus--;if(!closed){if(!visible&&manuallyHidden)Hide();else ScheduleHide();}};
    }
}
