using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace SuperDock;
internal sealed partial class AppearanceWindow
{
    TextBox? settingsSearch;bool changingSearch;string searchReturn="桌面偏好";string recordsTab="应用统计";
    static readonly (string Title,string Page,string Tab,string Keywords,string Anchor)[] SearchIndex=[
        ("文件搜索入口","桌面偏好","常用入口","Everything 搜索 查找 文件 磁盘","文件搜索"),
        ("文件搜索快捷键","桌面偏好","布局交互","Everything Alt 空格 Space Ctrl+Alt+F 热键","文件搜索快捷键"),
        ("自动隐藏","桌面偏好","布局交互","贴边 显示 隐藏 行为","自动隐藏"),
        ("拖动 Dock","桌面偏好","布局交互","拖动 拖拽 移动 位置 交互 停靠 图标 Ctrl","拖动 Dock"),
        ("Dock 功能入口","桌面偏好","常用入口","退出 按钮 位置 入口 方向","Dock 功能入口"),
        ("窗口预览","桌面偏好","布局交互","悬浮 预览 鼠标","悬浮预览"),
        ("停靠位置","桌面偏好","布局交互","上下左右 屏幕 方向 显示器","停靠位置"),
        ("多屏同步","桌面偏好","布局交互","显示器 多屏 多个 屏幕 主屏 副屏 同步 热插拔","显示器"),
        ("界面主题","桌面偏好","外观特效","亮色 暗色 液态玻璃 外观","界面主题"),
                ("图标大小","桌面偏好","外观特效","尺寸 图标 大小 缩小","图标大小"),
        ("图标间距","桌面偏好","外观特效","间距 间隔 紧凑 宽松","图标间距"),
        ("悬停放大","桌面偏好","外观特效","悬浮 鼠标 放大 倍数 尺寸 范围 平滑 拖放 暂停 效果","悬停放大"),
        ("栏体高度","桌面偏好","外观特效","Dock 高度 宽度 尺寸","栏体高度"),
        ("圆角","桌面偏好","外观特效","边框 圆角 栏体","圆角"),
        ("玻璃不透明度","桌面偏好","外观特效","透明度 透明 不透明 玻璃","玻璃不透明度"),
        ("氛围特效","桌面偏好","外观特效","扫光 呼吸 动画 特效 模糊","氛围特效"),
        ("图标倒影","桌面偏好","外观特效","倒影 镜像 反射 特效 图标 比例","图标倒影"),
        ("启动传送","桌面偏好","外观特效","启动 传送 着色 光晕 弹跳 点击 跳动 动画 效果 Hot-Track","启动传送"),
                ("城市天气","桌面偏好","桌面组件","天气 城市 气温 地点 搜索","城市天气"),
        ("性能监测","桌面偏好","桌面组件","性能 CPU GPU 内存 网络","性能监测"),
        ("时钟与日历","桌面偏好","桌面组件","时间 时钟 日期 日历","时钟与日历"),
        ("磁盘空间","桌面偏好","桌面组件","磁盘 硬盘 容量 存储","磁盘空间"),
        ("轮播设置","桌面偏好","桌面组件","轮播 间隔 秒 自定义 组件切换","轮播设置"),
        ("应用中心模式","应用管理","","小窗 全屏 启动台","应用中心效果"),
        ("应用分组","应用管理","","分组 图标 工作 游戏 AI 新建 重命名","应用分组"),
        ("常用入口","桌面偏好","常用入口","内置 系统 快捷 入口 回收站 下载 文件夹 设置 计算器","常用系统入口"),
        ("应用使用统计","使用记录","应用统计","热门 排行 次数 每天 每周 每月 数据中心 统计","热门应用"),
        ("运行日志","使用记录","运行日志","错误 异常 失败 日志中心 导出 记录",""),
        ("提醒透明度","高级设置","消息提醒","消息 气泡 通知 透明 不透明 玻璃","提醒透明度"),
        ("预览时长","高级设置","消息提醒","预览 时长 秒 一直显示 常驻 气泡 消失 收起","预览时长"),
        ("消息提醒","高级设置","消息提醒","QQ 微信 企业微信 企微 钉钉 飞书 通知 正文 详情","提醒设置"),
        ("全局快捷键","桌面偏好","布局交互","Alt+D 显示 隐藏 快捷键 热键","全局快捷键"),
        ("登录时启动","高级设置","系统维护","开机 启动 自启","运行与启动"),
        ("配置备份与恢复","高级设置","系统维护","导入 导出 恢复 备份 数据","配置备份"),
        ("关于与更新","高级设置","关于更新","版本 关于 升级 更新","软件更新"),
        ("退出程序","高级设置","系统维护","退出 关闭 结束 windock","退出windock"),
        ("任务栏设置","桌面偏好","布局交互","Windows 系统 任务栏","任务栏设置")
        ,("启动自动更新","高级设置","关于更新","联网 网络 在线 升级 更新 版本 启动 自动 热更新","软件更新")
    ];
    void BuildSettingsSearch(StackPanel brand)
    {
        var field=new Grid();field.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});field.ColumnDefinitions.Add(new ColumnDefinition());
        settingsSearch=new TextBox{MinHeight=0,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,HorizontalContentAlignment=HorizontalAlignment.Left,FontSize=13,TextWrapping=TextWrapping.NoWrap,AcceptsReturn=false,ToolTip="搜索设置 · Ctrl+F"};
        // The surrounding border owns all spacing; keep the editor viewport free of duplicated padding.
        settingsSearch.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"TextBox\"><ScrollViewer x:Name=\"PART_ContentHost\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" Focusable=\"False\" HorizontalScrollBarVisibility=\"Hidden\" VerticalScrollBarVisibility=\"Hidden\"/></ControlTemplate>");
        Grid.SetColumn(settingsSearch,1);field.Children.Add(settingsSearch);
        var hint=new TextBlock{Text="搜索设置",Foreground=Theme.Muted,FontSize=13,IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(hint,1);field.Children.Add(hint);
        field.Children.Add(new TextBlock{Text="\uE721",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=12,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
        var frame=new Border{Height=34,Padding=new Thickness(9,0,9,0),Margin=new Thickness(0,14,0,0),Background=Theme.Card,BorderBrush=Theme.Stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Child=field};
        settingsSearch.GotKeyboardFocus+=(_,_)=>frame.BorderBrush=new SolidColorBrush(Color.FromRgb(80,140,211));settingsSearch.LostKeyboardFocus+=(_,_)=>frame.BorderBrush=Theme.Stroke;brand.Children.Add(frame);        settingsSearch.TextChanged+=(_,_)=>{hint.Visibility=settingsSearch.Text.Length==0?Visibility.Visible:Visibility.Collapsed;if(changingSearch)return;if(string.IsNullOrWhiteSpace(settingsSearch.Text)){Navigate(searchReturn);return;}if(page!="搜索设置")searchReturn=page;SearchSettings(settingsSearch.Text);};
    }
    void SearchSettings(string text)
    {
        page="搜索设置";pageTitle.Text="搜索设置";tabHost.Children.Clear();content.Children.Clear();scroller.ScrollToTop();foreach(var button in navigation.Values)button.Background=Brushes.Transparent;
        var words=text.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var results=SearchIndex.Where(item=>words.All(word=>$"{item.Title} {item.Page} {item.Tab} {item.Keywords}".Contains(word,StringComparison.OrdinalIgnoreCase))).OrderByDescending(item=>words.Sum(word=>item.Title.Contains(word,StringComparison.OrdinalIgnoreCase)?100:item.Anchor.Contains(word,StringComparison.OrdinalIgnoreCase)?60:item.Keywords.Contains(word,StringComparison.OrdinalIgnoreCase)?30:10)).ToArray();
        content.Children.Add(Text($"“{text.Trim()}” · 找到 {results.Length} 项设置",13));content.Children.Add(Text("点击结果前往设置，目标项会短暂高亮。",11));
        foreach(var result in results)
        {
            var button=Button("",()=>{if(result.Page=="桌面偏好")desktopTab=result.Tab;if(result.Page=="使用记录")recordsTab=result.Tab;if(result.Page=="高级设置")generalTab=result.Tab;Navigate(result.Page);if(result.Anchor.Length>0)Dispatcher.BeginInvoke(new Action(()=>{content.UpdateLayout();var target=FindLabel(content,result.Anchor);if(target!=null){target.BringIntoView();if(target is TextBlock label){var highlight=new SolidColorBrush(Color.FromArgb(125,76,156,255));label.Background=highlight;highlight.BeginAnimation(SolidColorBrush.ColorProperty,new System.Windows.Media.Animation.ColorAnimation(Colors.Transparent,TimeSpan.FromSeconds(2.5)));}}}));});
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            row.Children.Add(new Border{Width=30,Height=30,CornerRadius=new CornerRadius(8),Background=Theme.Selected,VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock{Text=result.Page switch{"桌面偏好"=>"◒","应用管理"=>"▦","使用记录"=>"▥",_=>"◇"},FontSize=16,Foreground=Theme.Text,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center}});
            var labels=new StackPanel();var title=new TextBlock{Foreground=Theme.Text,FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};string term=words.FirstOrDefault(w=>result.Title.Contains(w,StringComparison.OrdinalIgnoreCase))??"";int at=term.Length>0?result.Title.IndexOf(term,StringComparison.OrdinalIgnoreCase):-1;
            if(at>=0){title.Inlines.Add(result.Title[..at]);title.Inlines.Add(new System.Windows.Documents.Run(result.Title.Substring(at,term.Length)){Foreground=new SolidColorBrush(Theme.Dark?Color.FromRgb(139,199,255):Color.FromRgb(24,103,207))});title.Inlines.Add(result.Title[(at+term.Length)..]);}else title.Text=result.Title;
            labels.Children.Add(title);labels.Children.Add(new TextBlock{Text=result.Page+(result.Tab.Length>0?"  ›  "+result.Tab:""),Foreground=Theme.Muted,FontSize=12,Margin=new Thickness(0,4,0,0)});Grid.SetColumn(labels,1);row.Children.Add(labels);
            var go=new TextBlock{Text="前往设置  ›",Foreground=Theme.Muted,FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,0,0)};Grid.SetColumn(go,2);row.Children.Add(go);
            button.Content=row;button.Padding=new Thickness(12);button.BorderThickness=new Thickness(1);button.Margin=new Thickness(0,0,0,10);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.ToolTip="前往 "+result.Page+" / "+result.Title;content.Children.Add(button);        }
        if(results.Length==0)content.Children.Add(Text("没有匹配设置，试试“快捷键”“天气”或“自动隐藏”。"));
    }
    static FrameworkElement? FindLabel(DependencyObject root,string text)
    {
        if(root is TextBlock label&&label.Text==text)return label;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var result=FindLabel(VisualTreeHelper.GetChild(root,i),text);if(result!=null)return result;}return null;
    }
    void Records()
    {
        Tabs(["应用统计","运行日志"],recordsTab,value=>{recordsTab=value;Navigate("使用记录");});
        if(recordsTab=="应用统计")Analytics();else Logs();
    }
}




