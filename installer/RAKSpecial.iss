; RAK Special - Windows setup wizard (Inno Setup 6: https://jrsoftware.org/isinfo.php)
; Built by publish.bat, or by hand:  ISCC installer\RAKSpecial.iss   (after publish\RAKSpecial.exe exists)
; Output: Share\RAKSpecial-Setup-<version>.exe
;
; - Installs for the current user by default: no administrator rights needed.
;   The first page lets an administrator install for all users instead.
; - Same folder and shortcut names as the old INSTALL.bat, so it updates those installs in place.
; - Uninstall from Windows Settings > Apps. Cheque register, payees and layouts in %APPDATA%\RAK Special are kept.

#define AppName "RAK Special"
#define AppExe "RAKSpecial.exe"
#define AppPublisher "Karthick Raja"
#define SourceExe AddBackslash(SourcePath) + "..\publish\" + AppExe

#if !FileExists(SourceExe)
  #error publish\RAKSpecial.exe is missing. Run publish.bat first.
#endif

; Version comes from the exe (<Version> in src\ChequeWriter\ChequeWriter.csproj), e.g. 2.0.0
#define VerMajor
#define VerMinor
#define VerPatch
#define VerBuild
#expr GetVersionComponents(SourceExe, VerMajor, VerMinor, VerPatch, VerBuild)
#define AppVersion Str(VerMajor) + "." + Str(VerMinor) + "." + Str(VerPatch)

[Setup]
; Never change AppId: Windows uses it to recognise updates of the same program.
AppId={{6F1C2B7E-4E0A-4F3B-9A51-7C2D8B9E3A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 {#AppPublisher}
AppComments=RAK Bank cheque printing: amount in words with fils, PDC series, cheque register, analytics.
; Shown in Windows Settings > Apps > RAK Special
AppPublisherURL=https://github.com/KarthickRaja46/RakSpecial
AppSupportURL=https://github.com/KarthickRaja46/RakSpecial#support
AppUpdatesURL=https://github.com/KarthickRaja46/RakSpecial/releases
AppContact=karthickraja232205@gmail.com
AppSupportPhone=+971 50 256 7444 (UAE) / +91 63832 68445 (India)
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} setup

; Per-user install by default ({autopf} = %LOCALAPPDATA%\Programs), all users if the user chooses it.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes

; The exe is 64-bit and needs Windows 10 or 11.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Close a running RAK Special before updating it.
CloseApplications=yes
RestartApplications=no
; Tells Windows to reload icons at the end. Without it, an update that changes the logo can keep
; showing the old icon on existing shortcuts (Windows caches icons by file path).
ChangesAssociations=yes

WizardStyle=modern
WizardImageFile=wizard-100.bmp,wizard-150.bmp,wizard-200.bmp
WizardSmallImageFile=wizard-small-100.bmp,wizard-small-150.bmp,wizard-small-200.bmp
SetupIconFile=..\src\ChequeWriter\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
InfoAfterFile=first-steps.txt

OutputDir=..\Share
OutputBaseFilename=RAKSpecial-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "RAK Bank cheque printing"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "RAK Bank cheque printing"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName} now"; Flags: nowait postinstall skipifsilent
; In-app updates run this setup silently: reopen RAK Special when it has finished.
Filename: "{app}\{#AppExe}"; Flags: nowait skipifnotsilent
