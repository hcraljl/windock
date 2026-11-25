using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace SuperDock;
internal static class DesktopIntegration
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorInfo { public int Size;public RECT Monitor,Work;public int Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device; }
    [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
    delegate bool MonitorCallback(IntPtr h,IntPtr dc,IntPtr rect,IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr handle,ref MonitorInfo info);
    public static List<(string Id,Rect Area)> Monitors(){var result=new List<(string,Rect)>();EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(h,dc,r,d)=>{var i=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>(),Device=""};if(GetMonitorInfo(h,ref i)){var monitor=(i.Device,new Rect(i.Work.Left,i.Work.Top,i.Work.Right-i.Work.Left,i.Work.Bottom-i.Work.Top));if((i.Flags&1)!=0)result.Insert(0,monitor);else result.Add(monitor);}return true;},IntPtr.Zero);return result;}
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
    public static bool MovePhysical(Window window,double x,double y){var handle=new WindowInteropHelper(window).Handle;return handle!=IntPtr.Zero&&SetWindowPos(handle,IntPtr.Zero,(int)Math.Round(x),(int)Math.Round(y),0,0,0x0015);}
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint{public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
    public static bool Cursor(out Point point){bool ok=GetCursorPos(out var p);point=new Point(p.X,p.Y);return ok;}
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    // Drags must survive a lost mouse capture, so the button state is polled instead of trusted.
    public static bool PrimaryButton => (GetAsyncKeyState(0x01) & 0x8000) != 0;
    [StructLayout(LayoutKind.Sequential)] struct Accent {public int State,Flags,Color,Animation;}
    [StructLayout(LayoutKind.Sequential)] struct Data {public int Attribute;public IntPtr Pointer;public int Size;}
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref Data data);
    public static bool Acrylic(Window window)
    {
        var accent=new Accent{State=4,Flags=2,Color=Theme.Dark?unchecked((int)0x3829231E):unchecked((int)0x24F7F4F0)};
        var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<Accent>());
        try{Marshal.StructureToPtr(accent,ptr,false);var data=new Data{Attribute=19,Pointer=ptr,Size=Marshal.SizeOf<Accent>()};return SetWindowCompositionAttribute(new WindowInteropHelper(window).Handle,ref data)!=0;}catch(EntryPointNotFoundException){return false;}finally{Marshal.FreeHGlobal(ptr);}
    }
    public static bool Blur(Window window,bool enabled){var accent=new Accent{State=enabled?3:0};var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<Accent>());try{Marshal.StructureToPtr(accent,ptr,false);var data=new Data{Attribute=19,Pointer=ptr,Size=Marshal.SizeOf<Accent>()};return SetWindowCompositionAttribute(new WindowInteropHelper(window).Handle,ref data)!=0;}catch(EntryPointNotFoundException){return false;}finally{Marshal.FreeHGlobal(ptr);}}
}





