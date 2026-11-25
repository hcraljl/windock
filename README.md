# windock

Windows 原生 WPF Dock，源码版本 1.5.6。内部命名空间和数据目录保留 SuperDock，兼容已有配置。

## 开发运行

需要 Windows 10/11 x64 和 .NET 10 SDK。

    dotnet restore SuperDock/SuperDock.csproj
    dotnet run --project SuperDock/SuperDock.csproj

## 生成安装包

安装 Inno Setup 7 或 6，然后运行：

    ./installer/build.ps1

产物为 artifacts/installer/windock-Setup-1.5.6-win-x64.exe，内置 .NET 运行时，按当前用户安装。普通构建不需要发布密钥。

源码包不包含 bin、obj、构建缓存、历史安装包、日志或个人数据；构建时会重新生成必要目录。

## 功能与依赖

应用中心、分组悬停展开、拖拽排序、窗口状态、悬停放大与预览、图标倒影、启动传送光晕、拖动图标调整顺序（拖动空白玻璃移动位置）、四向停靠、多屏同步、自动隐藏、智能避让、Alt+D 显隐、桌面组件、亮暗主题、设置搜索、消息提醒与透明度调整、日志和使用统计。

文件搜索基于 Everything，默认 Alt+Space，支持分类、大小排序、原始/文本预览。现代 Office 文件可直接提取文本；Office 原始排版和旧版 Office 文本提取需要安装 Microsoft Office。消息详情取决于系统权限及客户端可访问的信息。

## 回归检查

    dotnet run --project tests/PreviewRegression/PreviewRegression.csproj
    dotnet run --project tests/SettingsSearchCheck/SettingsSearchCheck.csproj
    dotnet run --project tests/MessageOpacityCheck/MessageOpacityCheck.csproj
    dotnet run --project tests/GroupHoverCheck/GroupHoverCheck.csproj

需要 Windows 图形会话。分组检查会临时移动鼠标；建议先退出正在运行的 windock，运行期间不要操作鼠标，结束后恢复位置。

## 用户数据

配置、日志、更新暂存：%LOCALAPPDATA%/SuperDock/
搜索偏好：%APPDATA%/SuperDock/search-prefs.json
安装目录：%LOCALAPPDATA%/Programs/SuperDock/

以上个人数据不包含在源码包中。

## 独立发布与在线更新

当前更新源为 http://8.163.110.199/JGstudio/windock/ 。修改后自行分发时，应关闭自动更新或改用自己的更新源和公钥，避免被原项目更新覆盖。

1. 同步修改 SuperDock/SuperDock.csproj 和 installer/SuperDock.iss 的版本号。
2. 运行 installer/build.ps1。
3. 使用自己的 RSA 私钥运行 installer/sign-release.ps1 -Notes '更新说明'，对应公钥放在 SuperDock/Assets/update-public.pem。
4. 私钥路径为 %LOCALAPPDATA%/windock-release/signing-private.pem，源码包只包含公钥。
5. 宝塔部署脚本 installer/deploy.ps1 需要 PowerShell 7，密钥由当前进程 BT_API_KEY 环境变量传入。独立部署须修改 Panel、Remote 参数及签名/部署脚本中的公开下载地址。

增量更新通过签名 files.json 对比 SHA-256，只下载变化文件；部署先上传并校验文件，最后发布更新清单。

## 第三方文件与素材

Everything.exe、es.exe、Everything64.dll 是搜索功能所需文件，予以保留。授权及来源见 SuperDock/Everything/License.txt、NOTICE.txt、PORT-NOTICE.txt。

消息 Logo 来源与授权见 SuperDock/Assets/NotificationLogos/。品牌 Logo 原稿和导出脚本一并保留。


## 更新与发布

更新完全手动：软件只在「设置 › 高级设置 › 关于更新 › 检查更新」时联网，启动与后台不做任何自动检查。

更新源为 GitHub Releases（`hcraljl/windock`）：

1. 主路：GitHub API `releases/latest`；
2. 兜底：匿名 API 限流（403/429）时改走 `github.com/.../releases/latest` 重定向核对版本（不受 API 限额约束）。

发布新版本的流程（推送后软件即可检查到）：

1. 把版本号提到 `SuperDock/SuperDock.csproj` 的 `<Version>` 与 `installer/SuperDock.iss` 的 `AppVersion`（保持一致，如 1.6.6）；
2. 用 `installer/build.ps1` 打出 `artifacts/installer/windock-Setup-<版本>-win-x64.exe`；
3. 在 GitHub 创建 Release：tag 形如 `v1.6.6`，附件上传该安装包（文件名保持 `windock-Setup-<版本>-win-x64.exe`）；建议在说明里附一行 `sha256: <安装包哈希>`，软件会自动校验。

安装/升级只更新程序本体；本地用户数据（入口、分组、排序、设置、使用记录、自定义图标）自动检测并保留，安装前先备份到 `%LOCALAPPDATA%\SuperDock\preinstall-backup`。