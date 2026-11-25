using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;
internal static class ShellIcons
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string,Lazy<Task<ImageSource?>>> pending=new(StringComparer.OrdinalIgnoreCase);
    static readonly SemaphoreSlim workers=new(3);
    public static ImageSource? Cached(string target)=>cache.GetValueOrDefault(target);
    public static Task<ImageSource?> LoadAsync(string target)=>cache.TryGetValue(target,out var image)?Task.FromResult<ImageSource?>(image):pending.GetOrAdd(target,key=>new Lazy<Task<ImageSource?>>(()=>Task.Run(async()=>{
        await workers.WaitAsync();try{return Load(key);}catch(Exception ex){ActivityStore.Write("图标读取失败",message:ex.Message,error:ex);return null;}finally{workers.Release();}
    }))).Value;
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Info { public IntPtr Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Name; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string Type; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Stock { public uint Size; public IntPtr Icon; public int Index,IconIndex; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path; }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attr,out Info info,uint size,uint flags);
    [DllImport("shell32.dll")] static extern int SHGetStockIconInfo(int id,uint flags,ref Stock info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHDefExtractIcon(string path,int index,uint flags,out IntPtr large,out IntPtr small,uint size);
    [DllImport("shell32.dll")] static extern int SHGetImageList(int size,ref Guid iid,out IntPtr list);
    [DllImport("comctl32.dll")] static extern IntPtr ImageList_GetIcon(IntPtr list,int index,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static ImageSource? Convert(IntPtr icon)
    {
        if(icon==IntPtr.Zero) return null;
        try { var image=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image; }
        finally { DestroyIcon(icon); }
    }
    public static ImageSource? Load(string target)
    {
        if(cache.TryGetValue(target,out var existing)) return existing;
        ImageSource? image=null;
        if(target.Equals("shell:RecycleBinFolder",StringComparison.OrdinalIgnoreCase))
        {
            var stock=new Stock {Size=(uint)Marshal.SizeOf<Stock>(),Path=""};
            if(SHGetStockIconInfo(31,0,ref stock)==0) image=Extract(stock.Path,stock.IconIndex);
        }
        else
        {
            var path=ShortcutTarget.Resolve(target);
            if(!File.Exists(path)&&!Directory.Exists(path)) { path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),target);if(!File.Exists(path))path=Path.Combine(Environment.SystemDirectory,target); }
            if(File.Exists(path)||Directory.Exists(path))
            {
                if(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)) image=Extract(path,0);
                if(image==null && SHGetFileInfo(path,0,out var info,(uint)Marshal.SizeOf<Info>(),0x4000)!=IntPtr.Zero)
                {
                    var iid=new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                    if(SHGetImageList(4,ref iid,out var list)==0)
                    {
                        try { image=Convert(ImageList_GetIcon(list,info.Index,1)); } finally { Marshal.Release(list); }
                    }
                }
                if(image==null) { SHGetFileInfo(path,0,out var info2,(uint)Marshal.SizeOf<Info>(),0x100);image=Convert(info2.Icon); }
            }
        }
        if(image!=null) cache[target]=image;
        return image;
    }
    static ImageSource? Extract(string path,int index)
    {
        SHDefExtractIcon(Environment.ExpandEnvironmentVariables(path),index,0,out var large,out var small,128);
        if(small!=IntPtr.Zero)DestroyIcon(small);
        return Convert(large);
    }
}

