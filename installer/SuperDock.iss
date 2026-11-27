#define AppVersion "1.8.7"
[Setup]
AppId={{FD5D931A-CEEA-4524-9F1A-ED650EB24EAB}
AppName=windock
AppVersion={#AppVersion}
AppPublisher=windock
DefaultDirName={localappdata}\Programs\SuperDock
DefaultGroupName=windock
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\installer
OutputBaseFilename=windock-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\windock-icon-v2.ico
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
SetupIconFile=..\SuperDock\Assets\logo.ico

; 用户数据（%LOCALAPPDATA%\SuperDock：settings.json、使用记录、自定义图标）
; 位于程序目录之外，本脚本从不写入、删除或重置它；下面的 [Code] 还会
; 在安装前做一次快照备份，万一安装过程中数据丢失可自动恢复。
; 卸载同样保留用户数据，因此这里不使用 [UninstallDelete]。

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
Name: "startup"; Description: "登录时启动windock"; GroupDescription: "启动选项："; Flags: unchecked

[InstallDelete]
Type: files; Name: "{userprograms}\栖 Dock\栖 Dock.lnk"
Type: files; Name: "{userdesktop}\栖 Dock.lnk"
Type: files; Name: "{app}\SuperDock.exe"
Type: files; Name: "{app}\SuperDock.dll"
Type: files; Name: "{app}\SuperDock.deps.json"
Type: files; Name: "{app}\SuperDock.runtimeconfig.json"
[Files]
Source: "..\artifacts\publish\windock-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\windock"; Filename: "{app}\Windock.exe"; WorkingDir: "{app}"; IconFilename: "{app}\windock-icon-v2.ico"
Name: "{autodesktop}\windock"; Filename: "{app}\Windock.exe"; WorkingDir: "{app}"; IconFilename: "{app}\windock-icon-v2.ico"; Check: ShouldWriteDesktopIcon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SuperDock"; ValueData: """{app}\Windock.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Windock.exe"; Description: "启动windock"; Flags: nowait postinstall

[Code]
var
  DataPageID: Integer;

function UserDataDir: String;
begin
  Result := ExpandConstant('{localappdata}\SuperDock');
end;

function HasExistingInstall: Boolean;
begin
  try
    Result := FileExists(ExpandConstant('{app}\Windock.exe'));
  except
    Result := False;
  end;
end;

function HasUserData: Boolean;
begin
  Result :=
    (FileExists(UserDataDir + '\settings.json') or
     DirExists(UserDataDir + '\history') or
     DirExists(UserDataDir + '\custom-icons') or
     DirExists(UserDataDir + '\group-icons'));
end;

procedure CopyDirTree(const Src, Dest: String);
var
  Sr: TFindRec;
  From, Target: String;
begin
  if not DirExists(Src) then Exit;
  ForceDirectories(Dest);
  if FindFirst(Src + '\*', Sr) then
  try
    repeat
      if (Sr.Name <> '.') and (Sr.Name <> '..') then
      begin
        From := Src + '\' + Sr.Name;
        Target := Dest + '\' + Sr.Name;
        if (Sr.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          CopyDirTree(From, Target)
        else
          FileCopy(From, Target, False);
      end;
    until not FindNext(Sr);
  finally
    FindClose(Sr);
  end;
end;

procedure InitializeWizard;
var
  Page: TOutputMsgWizardPage;
begin
  if HasUserData then
  begin
    Page := CreateOutputMsgPage(wpWelcome,
      '检测到已有安装',
      '本机已存在 windock 及你的个人数据。',
      '安装程序检测到以下已有数据：' #13#10 #13#10 +
      '· Dock 中添加/移除过的软件与分组' #13#10 +
      '· 图标顺序调整' #13#10 +
      '· 设置页中的全部开关与参数' #13#10 +
      '· 使用记录和自定义图标' #13#10 #13#10 +
      '本次只更新程序本体，上述数据将完整保留，不会被覆盖或重置。' #13#10 +
      '安装前会自动做一次备份（' + UserDataDir + '\preinstall-backup），' #13#10 +
      '如安装过程中数据意外丢失会自动恢复。');
    DataPageID := Page.ID;
  end
  else
    DataPageID := -1;
end;

function ShouldWriteDesktopIcon: Boolean;
begin
  Result := WizardIsTaskSelected('desktopicon') or FileExists(ExpandConstant('{autodesktop}\windock.lnk'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  BackupDir, Src, Dest: String;
begin
  if CurStep = ssInstall then
  begin
    // Snapshot user data before a single program file is touched.
    if HasUserData then
    begin
      BackupDir := UserDataDir + '\preinstall-backup';
      ForceDirectories(BackupDir);
      if FileExists(UserDataDir + '\settings.json') then
        FileCopy(UserDataDir + '\settings.json', BackupDir + '\settings.json', False);
      CopyDirTree(UserDataDir + '\history', BackupDir + '\history');
      CopyDirTree(UserDataDir + '\custom-icons', BackupDir + '\custom-icons');
      CopyDirTree(UserDataDir + '\group-icons', BackupDir + '\group-icons');
      Log('User data backed up to ' + BackupDir);
    end;
  end;
  if CurStep = ssPostInstall then
  begin
    // Paranoia net: if the configuration vanished during the update, restore it.
    if (not FileExists(UserDataDir + '\settings.json')) and
       FileExists(UserDataDir + '\preinstall-backup\settings.json') then
    begin
      Src := UserDataDir + '\preinstall-backup\settings.json';
      Dest := UserDataDir + '\settings.json';
      FileCopy(Src, Dest, False);
      Log('settings.json restored from preinstall backup');
    end;
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SuperDock', Src) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SuperDock', '"' + ExpandConstant('{app}\Windock.exe') + '"');
  end;
end;
