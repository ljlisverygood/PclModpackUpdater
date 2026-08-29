; Inno Setup 6 打包脚本 —— PCL 整合包更新器
; 本地打包：ISCC.exe installer\PclModpackUpdater.iss
; 前置：先将 dotnet publish 的输出复制到仓库根目录的 publish\ 文件夹
; （Release 工作流会自动完成 publish、复制与打包）

#define MyAppName "PCL 整合包更新器"
#define MyAppNameEn "PclModpackUpdater"
#define MyAppExeName "PclModpackUpdater.exe"
#define MyAppVersion GetVersionNumbersString("..\publish\" + MyAppExeName)
#define MyAppPublisher "LiuJiLing"

[Setup]
AppId={{B3A7E1F0-9C2D-4E8A-A6B1-5D4F0C8E2A73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist
OutputBaseFilename=PclModpackUpdater-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; publish 输出包含主程序、依赖与 en-US、zh-Hans 等多语言资源文件夹，需整体递归打包
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
