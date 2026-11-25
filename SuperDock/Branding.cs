using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SuperDock;

internal static class Branding
{
    public static void RefreshShortcuts()
    {
        var icon=System.IO.Path.Combine(AppContext.BaseDirectory,"windock-icon-v2.ico");if(!System.IO.File.Exists(icon))return;
        object? shell=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            var desktop=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"windock.lnk");
            var programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var links=new[]{desktop}.Concat(System.IO.Directory.EnumerateFiles(programs,"windock.lnk",new System.IO.EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=System.IO.FileAttributes.ReparsePoint}));
            foreach(var path in links.Where(System.IO.File.Exists)){
                object? link=null;try{
                    link=((dynamic)shell!).CreateShortcut(path);
                    if(string.Equals((string)((dynamic)link).TargetPath,System.IO.Path.Combine(AppContext.BaseDirectory,"Windock.exe"),StringComparison.OrdinalIgnoreCase)&&!string.Equals((string)((dynamic)link).IconLocation,icon+",0",StringComparison.OrdinalIgnoreCase)){((dynamic)link).IconLocation=icon+",0";((dynamic)link).Save();}
                }finally{if(link!=null&&System.Runtime.InteropServices.Marshal.IsComObject(link))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}
            }
        }catch(Exception ex){ActivityStore.Write("快捷方式图标",message:ex.Message,error:ex);}
        finally{if(shell!=null&&System.Runtime.InteropServices.Marshal.IsComObject(shell))System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    public static ImageSource Logo {get;}=Load();
    static ImageSource Load()
    {
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
        image.UriSource=new Uri("pack://application:,,,/Windock;component/Assets/logo.png");image.EndInit();image.Freeze();return image;
    }
}
