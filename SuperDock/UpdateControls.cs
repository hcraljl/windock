using System.Windows;
namespace SuperDock;
internal sealed partial class AppearanceWindow
{
    void UpdateControls()
    {
        Heading("软件更新");content.Children.Add(Text("windock 不会自动下载或更新。点击下方按钮手动检查新版本，确认后才会下载安装。",12));
        var status=Text(UpdateService.Status,12);content.Children.Add(status);var button=Button("检查更新",()=>{});button.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(button);
        button.Click+=async(_,_)=>
        {
            button.IsEnabled=false;status.Text="正在检查…";
            try
            {
                var release=await UpdateService.Check();if(!IsVisible)return;status.Text=UpdateService.Status;if(release==null)return;
                if(!GlassDialog.Confirm($"版本 {release.Version}\n{release.Notes}\n\n仅下载有变化的文件，完成后重启。个人配置和日志会保留。","windock 更新","更新并重启"))return;
                status.Text="正在下载…";var progress=new Progress<int>(p=>status.Text=$"正在下载 {p}%");var path=await UpdateService.Download(release,progress);status.Text="校验通过，正在安装…";UpdateService.Install(path);
            }
            catch(Exception ex){status.Text="更新失败："+ex.Message;}
            finally{button.IsEnabled=true;}
        };
    }
}
