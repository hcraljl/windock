using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
namespace SuperDock;
internal sealed class PositionFlyout : Popup
{
    public PositionFlyout(FrameworkElement anchor,string current,Action<string> select)
    {
        PlacementTarget=anchor;Placement=current switch{"Top"=>PlacementMode.Bottom,"Left"=>PlacementMode.Right,"Right"=>PlacementMode.Left,_=>PlacementMode.Top};AllowsTransparency=true;StaysOpen=false;PopupAnimation=PopupAnimation.Fade;
        var choices=new UniformGrid{Columns=4};
        foreach(var (edge,label) in new[]{("Left","左侧"),("Top","顶部"),("Bottom","底部"),("Right","右侧")})
        {
            bool selected=edge==current;var face=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};face.Children.Add(new Viewbox{Width=34,Height=24,Child=DockPositionVisual.Create(edge)});face.Children.Add(new TextBlock{Text=label,FontSize=11,FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal,Foreground=Theme.Text,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,7,0,0)});
            var button=new Button{Content=face,Width=55,Height=62,Style=(Style)Application.Current.FindResource("GlassButton"),Padding=new Thickness(4),Margin=new Thickness(2),Background=selected?Theme.Selected:Brushes.Transparent,ToolTip=selected?label+" · 当前停靠位置":"停靠在"+label};button.Click+=(_,_)=>{IsOpen=false;select(edge);};choices.Children.Add(button);
        }
        var panel=new Border{Child=choices,Padding=new Thickness(5),CornerRadius=new CornerRadius(14),Background=Theme.Get("MenuGlass"),BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.23}};RoundClip.SetRadius(panel,14);Child=new Border{Padding=new Thickness(8),Child=panel};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};DateTime opened=DateTime.UtcNow;DateTime? outside=null;
        Opened+=(_,_)=>{opened=DateTime.UtcNow;timer.Start();};Closed+=(_,_)=>timer.Stop();
        timer.Tick+=(_,_)=>{if(!DesktopIntegration.Cursor(out var p))return;try{var a=panel.PointToScreen(new Point());var b=panel.PointToScreen(new Point(panel.ActualWidth,panel.ActualHeight));if(p.X>=a.X&&p.X<=b.X&&p.Y>=a.Y&&p.Y<=b.Y){outside=null;return;}if((DateTime.UtcNow-opened).TotalMilliseconds<900)return;outside??=DateTime.UtcNow;if((DateTime.UtcNow-outside.Value).TotalMilliseconds>350)IsOpen=false;}catch(InvalidOperationException){timer.Stop();}};
    }
}

