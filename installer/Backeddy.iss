; Backeddy for Windows — instalador (Inno Setup 6). Montado pelo GitHub Actions: ISCC /DAppVersion=X Backeddy.iss
; Instala o app (bandeja) e o serviço "Backeddy" (início automático, como sistema).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{5B7E2C1A-3F4D-4E8B-9A6C-BACCEDD70001}
AppName=Backeddy
AppVersion={#AppVersion}
AppVerName=Backeddy {#AppVersion}
AppPublisher=Eddy Antonini
AppPublisherURL=https://github.com/BackEddy/windows
AppSupportURL=https://github.com/BackEddy/windows/issues
DefaultDirName={autopf}\Backeddy
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=..\out
OutputBaseFilename=Backeddy-Setup-{#AppVersion}
SetupIconFile=..\assets\Backeddy.ico
UninstallDisplayIcon={app}\app\Backeddy.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0.19045
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no
ShowLanguageDialog=auto

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
pt.ServiceDesc=Backups automáticos do Backeddy
en.ServiceDesc=Backeddy automatic backups
es.ServiceDesc=Backups automáticos de Backeddy

[Files]
Source: "..\out\app\*"; DestDir: "{app}\app"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "..\out\service\*"; DestDir: "{app}\service"; Flags: recursesubdirs ignoreversion createallsubdirs

[Icons]
Name: "{autoprograms}\Backeddy"; Filename: "{app}\app\Backeddy.exe"

[Registry]
; o ícone da bandeja abre junto com o Windows, para todos os usuários
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Backeddy"; \
  ValueData: """{app}\app\Backeddy.exe"" --tray"; Flags: uninsdeletevalue

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create Backeddy binPath= ""{app}\service\Backeddy.Service.exe"" start= delayed-auto DisplayName= ""Backeddy"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "config Backeddy binPath= ""{app}\service\Backeddy.Service.exe"" start= delayed-auto"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description Backeddy ""{cm:ServiceDesc}"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "failure Backeddy reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start Backeddy"; Flags: runhidden
Filename: "{app}\app\Backeddy.exe"; Description: "{cm:LaunchProgram,Backeddy}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM Backeddy.exe /F"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "{sys}\sc.exe"; Parameters: "stop Backeddy"; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete Backeddy"; Flags: runhidden; RunOnceId: "DeleteService"

[Code]
// atualização: para o serviço e fecha o app antes de trocar os arquivos
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop Backeddy', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM Backeddy.exe /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(1500);
  Result := '';
end;
