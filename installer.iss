; Per-user installer for JouleDesk. Publish the application first:
;   dotnet publish JouleDesk.csproj -c Release -r win-x64 -o publish
; then compile this script with Inno Setup 7 (ISCC.exe installer.iss).
;
; The installer test compiles a copy with a different identity, by passing --define=Variant=<name>,
; so it never touches a real installation.

#ifndef Variant
  #define Variant ""
#endif

#define AppName "JouleDesk" + Variant
#define AppExe "JouleDesk.exe"
#define PublishedExe "publish\" + AppExe

; The version is read from the published executable, which gets it from JouleDesk.csproj.
#define FileVersion GetVersionNumbersString(PublishedExe)
#define AppVersion Copy(FileVersion, 1, RPos(".", FileVersion) - 1)

#if Variant == ""
  #define AppGuid "{{7FCB5D15-E254-4C25-8B73-AC9E44F1B079}"
#else
  #define AppGuid "{{311AD220-9694-4A5B-AD03-B498C939EE31}"
#endif

[Setup]
AppId={#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Paula Karhi
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer-output
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes
LanguageDetectionMethod=none
UsePreviousLanguage=no
AppMutex=Local\{#AppName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fi"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "sv"; MessagesFile: "compiler:Languages\Swedish.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:BackgroundStartup}"; GroupDescription: "{cm:Options}"

[Files]
Source: "{#PublishedExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "catalog-example.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
Source: "docs\UserGuide.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; AppUserModelID: "{#AppName}.Desktop"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "{#AppName}.Desktop"

[Registry]
; The language chosen here is read by the application at start.
Root: HKCU; Subkey: "Software\{#AppName}"; ValueType: string; ValueName: "Language"; ValueData: "{language}"; Flags: uninsdeletevalue uninsdeletekeyifempty
; Starting at sign-in is a task of the installer; unchecking it on a reinstall removes the entry.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; Flags: deletevalue; Tasks: not startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppName}.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
fi.BackgroundStartup=Käynnistä taustaseuranta automaattisesti Windowsiin kirjautuessa
sv.BackgroundStartup=Starta bakgrundsmätning automatiskt vid Windows-inloggning
en.BackgroundStartup=Start background monitoring automatically when signing in to Windows
es.BackgroundStartup=Iniciar seguimiento en segundo plano al iniciar sesión en Windows
fr.BackgroundStartup=Démarrer le suivi en arrière-plan à la connexion Windows
de.BackgroundStartup=Hintergrundüberwachung bei der Windows-Anmeldung starten
pt.BackgroundStartup=Iniciar monitorização em segundo plano ao entrar no Windows
zh.BackgroundStartup=登录 Windows 时自动启动后台监测
ja.BackgroundStartup=Windowsログイン時にバックグラウンド監視を自動開始
ko.BackgroundStartup=Windows 로그인 시 백그라운드 모니터링 자동 시작
it.BackgroundStartup=Avvia monitoraggio in background all'accesso a Windows
ru.BackgroundStartup=Автоматически запускать фоновый мониторинг при входе в Windows
fi.Options=Seuranta:
sv.Options=Övervakning:
en.Options=Monitoring:
es.Options=Seguimiento:
fr.Options=Suivi :
de.Options=Überwachung:
pt.Options=Monitorização:
zh.Options=监测：
ja.Options=監視：
ko.Options=모니터링:
it.Options=Monitoraggio:
ru.Options=Мониторинг:
