using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace SuperDock;
// 滚动截图：在固定屏幕区域内反复滚动并截图，按重叠位移纵向拼接为长图。
// 流程：框选 → 点"滚动截图" → 点"开始滚动" → 覆盖层关闭，HUD 显示进度（Esc/停止可中断）→ 完成弹出结果窗口（复制/保存/贴图/关闭）。
internal static class ScrollCapture
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,int dx,int dy,uint data,UIntPtr extra);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(WheelPoint point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr holder,uint offset);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst,int x,int y,int w,int h,IntPtr src,int sx,int sy,int rop);
    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfo{public int Size,Width,Height;public short Planes,BitCount;public int Compression,SizeImage,XPelsPerMeter,YPelsPerMeter,ColorsUsed,ColorsImportant;}
    [StructLayout(LayoutKind.Sequential)]struct WheelPoint{public int X,Y;}
    static volatile bool cancelled;
    static Dispatcher ui=null!;
    public static void Run(Rect phys,MainWindow.Settings settings)
    {
        ui=Dispatcher.CurrentDispatcher; // UI 线程（chip 点击处）
        cancelled=false;
        int x=(int)Math.Round(phys.Left),y=(int)Math.Round(phys.Top),w=(int)Math.Round(phys.Width),h=(int)Math.Round(phys.Height);
        w=Math.Max(16,w);h=Math.Max(16,h);
        // HUD：进度与停止
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>ShowHud(x,y,w,h)));
        var thread=new System.Threading.Thread(()=>Loop(x,y,w,h,settings));
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
    static void ShowHud(int x,int y,int w,int h)
    {
        var hud=new Window
        {
            WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,
            // 不抢焦点：HUD 拿走键盘焦点后，鼠标滚轮会发给 HUD 而不是被截取的页面（表现为“滚动只有一会”/根本不滚）
            ShowInTaskbar=false,Topmost=true,ShowActivated=false,Focusable=false,Width=300,Height=44,WindowStartupLocation=WindowStartupLocation.Manual,Title="windock · 滚动截图"
        };
        var text=new TextBlock{Text="滚动截图中… 0 px",FontSize=12,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center};
        var stop=new Border{Background=new SolidColorBrush(Color.FromRgb(240,82,82)),CornerRadius=new CornerRadius(12),Height=26,Padding=new Thickness(14,0,14,0),Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0),Child=new TextBlock{Text="停止",FontSize=12,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center}};
        stop.MouseLeftButtonUp+=(_,_)=>cancelled=true;
        hud.Content=new Border{CornerRadius=new CornerRadius(10),Background=GlassWindow.Surface,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),Padding=new Thickness(14,0,14,0),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.3},Child=new StackPanel{Orientation=Orientation.Horizontal,Children={text,stop}}};
        // 位置：优先放在选区上方；放不下则下方
        double scale=1;
        var source=PresentationSource.FromVisual(App.Current?.MainWindow??(Window)null!);
        if(source?.CompositionTarget!=null)scale=source.CompositionTarget.TransformToDevice.M11;
        hud.Loaded+=(_,_)=>{
            double lx=x/scale+w/scale/2-150;
            double ly=y/scale-54;
            if(ly<SystemParameters.VirtualScreenTop+8)ly=(y+h)/scale+12;
            hud.Left=Math.Clamp(lx,SystemParameters.VirtualScreenLeft+8,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-308);
            hud.Top=ly;
        };
        hud.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)cancelled=true;};
        hud.Show();
        hudState.Text=text;hudState.Window=hud;
    }
    static class hudState{internal static TextBlock? Text;internal static Window? Window;}
    static void UpdateHud(int px)
    {
        var window=hudState.Window;
        window?.Dispatcher.BeginInvoke(new Action(()=>{if(hudState.Text!=null)hudState.Text.Text=$"滚动截图中… {px} px";}));
    }
    static void CloseHud()
    {
        var window=hudState.Window;
        window?.Dispatcher.BeginInvoke(new Action(()=>window.Close()));
    }
    // GDI 截取屏幕固定区域（物理像素，Bgr32 自上而下）。
    static byte[]? Capture(int x,int y,int w,int h)
    {
        try
        {
            var hdcScreen=GetDC(IntPtr.Zero);
            var hdcMem=CreateCompatibleDC(hdcScreen);
            var info=new BitmapInfo{Size=40,Width=w,Height=-h,Planes=1,BitCount=32,Compression=0};
            var dib=CreateDIBSection(hdcMem,ref info,0,out var bits,IntPtr.Zero,0);
            if(dib==IntPtr.Zero){ReleaseDC(IntPtr.Zero,hdcScreen);DeleteDC(hdcMem);return null;}
            var previous=SelectObject(hdcMem,dib);
            BitBlt(hdcMem,0,0,w,h,hdcScreen,x,y,0x00CC0020|0x40000000);
            var pixels=new byte[w*h*4];
            Marshal.Copy(bits,pixels,0,pixels.Length);
            SelectObject(hdcMem,previous);DeleteObject(dib);DeleteDC(hdcMem);ReleaseDC(IntPtr.Zero,hdcScreen);
            return pixels;
        }
        catch(Exception){return null;}
    }
    // 位移匹配（内带去边版）：prev 内容带 [pad+o, h-pad) 应与 cur 内容带 [pad, h-pad-o) 一致。
    // o=0 恒自洽，因此从 o=1 起搜；误差小于阈值才认定真实位移。
    static int FindOffset(byte[] prev,byte[] cur,int w,int h,int maxSearch,int pad,out double bestErrOut)
    {
        int best=0;double bestErr=double.MaxValue;
        int cols=Math.Min(w,512)/16+1;
        int stride=w*4;
        int lo=pad,hi=h-pad;
        for(int o=1;o<=maxSearch&&o<hi-lo-4;o++)
        {
            double err=0;long count=0;
            for(int r=lo+o;r<hi;r++)
            {
                int pr=r*stride,cr=(r-o)*stride;
                for(int c=0;c<cols;c++)
                {
                    int px=c*16*4;
                    if(px+3>=stride)break;
                    int d=prev[pr+px]-cur[cr+px]+prev[pr+px+1]-cur[cr+px+1]+prev[pr+px+2]-cur[cr+px+2];
                    err+=d<0?-d:d;count++;
                }
            }
            if(count==0)continue;
            err/=count;
            if(err<bestErr){bestErr=err;best=o;}
        }
        bestErrOut=bestErr;
        return bestErr<42?best:0;
    }
    // 帧是否一致（采样比较）：用于等待滚动动画结束（连续两帧相同才算定格）。
    static bool SameFrame(byte[]? a,byte[]? b,int w,int h)
    {
        if(a==null||b==null)return false;
        int stride=w*4;
        for(int r=0;r<h;r+=4)
        {
            int at=r*stride;
            for(int c=0;c<stride;c+=64)
            {
                int px=at+c;
                if(px+3>=a.Length)return true;
                int d=a[px]-b[px]+a[px+1]-b[px+1]+a[px+2]-b[px+2];
                if((d<0?-d:d)>10)return false;
            }
        }
        return true;
    }
    static void Loop(int x,int y,int w,int h,MainWindow.Settings settings)
    {
        var rows=new List<byte[]>();
        var first=Capture(x,y,w,h);
        if(first==null){Finish(null,settings);return;}
        // 去边内带：窗口标题栏/状态栏/滚动条等固定边框不参与匹配与拼接（否则底行永不移动，永远匹配不上）
        int pad=Math.Min(64,Math.Max(24,h/10));
        rows.Add(first.AsSpan(pad*w*4,(h-2*pad)*w*4).ToArray());
        byte[] lastFrame=first;
        SetCursorPos(x+w/2,Math.Min(y+h/2,y+h-8)); // 光标放到选区内，滚轮作用于下方窗口
        System.Threading.Thread.Sleep(250);
        int total=0,idle=0;
        var sw= Stopwatch.StartNew();
        int search=Math.Min(480,h/2);
        bool directMode=false; // mouse_event 无效时（焦点不在目标页/系统关闭悬停滚动），改用 SendMessage 直发滚轮
        while(!cancelled&&rows.Count<1200&&total<40000&&sw.ElapsedMilliseconds<180000)
        {
            int delta=-240; // 滚轮下滚 2 格
            if(directMode)SendWheel(x+w/2,y+h/2,delta);
            else mouse_event(0x0800,0,0,unchecked((uint)delta),UIntPtr.Zero);
            // 等滚动动画定格：连续两帧采样一致才继续（不等会拼出错位/重复内容）
            byte[]? current=Capture(x,y,w,h);byte[]? previous=null;int settle=0;
            while(settle<20&&!cancelled)
            {
                System.Threading.Thread.Sleep(120);
                previous=current;current=Capture(x,y,w,h);
                if(current==null)break;
                if(SameFrame(previous,current,w,h))break;
                settle++;
            }
            if(current==null)break;
            // 视口完全没变（到底了或页面不可滚）：帧不变就没新内容，重复内容也不能误判成滚动
            if(SameFrame(lastFrame,current,w,h))
            {
                idle++;
                if(idle>=2)break;
                continue;
            }
            int o=FindOffset(lastFrame,current,w,h,search,pad,out var err);
            if(o<=1)
            {
                if(!directMode) // mouse_event 一轮无位移：改用直接发送滚轮再试一次
                {
                    directMode=true;
                    SendWheel(x+w/2,y+h/2,delta);
                    System.Threading.Thread.Sleep(350);
                    var retry=Capture(x,y,w,h);
                    if(retry!=null)
                    {
                        int o2=FindOffset(lastFrame,retry,w,h,search,pad,out var err2);
                        if(o2>1){idle=0;total+=o2;rows.Add(retry.AsSpan((h-pad-o2)*w*4,o2*w*4).ToArray());lastFrame=retry;UpdateHud(total);continue;}
                    }
                }
                // 页面没动：可能已到底。连续两个周期仍无位移才收尾。
                idle++;
                lastFrame=current;
                if(idle>=2)break;
                continue;
            }
            idle=0;
            total+=o;
            rows.Add(current.AsSpan((h-pad-o)*w*4,o*w*4).ToArray());
            lastFrame=current;
            UpdateHud(total);
        }
        Finish(Assemble(rows,w),settings);
    }
    // 直接把 WM_MOUSEWHEEL 发给光标下的顶层窗口：不依赖“悬停滚动”设置或焦点归属。
    static void SendWheel(int x,int y,int delta)
    {
        try
        {
            var hwnd=WindowFromPoint(new WheelPoint{X=x,Y=y});
            if(hwnd==IntPtr.Zero)return;
            var root=GetAncestor(hwnd,2); // GA_ROOT
            if(root!=IntPtr.Zero)hwnd=root;
            SendMessage(hwnd,0x020A,(IntPtr)(delta<<16),(IntPtr)((y<<16)|(x&0xFFFF)));
        }
        catch{ }
    }
    static BitmapSource? Assemble(List<byte[]> strips,int w)
    {
        try
        {
            // strips：首条=首帧内容带（h-2*pad 行），其后每条为追加的 o 行
            int stride=w*4,total=0;
            foreach(var s in strips)total+=s.Length/stride;
            if(total<=0)return null;
            var result=new WriteableBitmap(w,total,96,96,PixelFormats.Bgr32,null);
            result.Lock();
            int at=0;
            foreach(var s in strips)
            {
                int sh=s.Length/stride;
                result.WritePixels(new Int32Rect(0,at,w,sh),s,stride,0);
                at+=sh;
            }
            result.Unlock();
            result.Freeze();
            return result;
        }
        catch(Exception){return null;}
    }
    static void Finish(BitmapSource? result,MainWindow.Settings settings)
    {
        CloseHud();
        ui.BeginInvoke(new Action(()=>{
            if(result==null)GlassDialog.Show("滚动截图失败：无法截取屏幕区域");
            else ScrollResult.Show(result,settings);
        }));
    }
}
