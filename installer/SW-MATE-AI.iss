; SW-MATE AI installer - Inno Setup 6, Windows x64, SOLIDWORKS add-in.
; Keep AppId unchanged across all future releases: upgrades happen in place.
#ifndef MyAppVersion
  #define MyAppVersion "0.2.0"
#endif
#define MyAppName "SW-MATE AI"
#define MyAppId "{{DDA67C15-6479-42AD-80F5-CCFE7A27D815}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=SW-MATE AI
DefaultDirName={autopf}\SW-MATE AI
DefaultGroupName=SW-MATE AI
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
OutputDir=..\dist
OutputBaseFilename=SW-MATE-AI-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=no
RestartApplications=no
UninstallDisplayName=SW-MATE AI
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription=SW-MATE AI SOLIDWORKS Add-In Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\artifacts\installer-stage\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SW-MATE AI\Kiểm tra cập nhật"; Filename: "{app}\SwMateAI.Updater.exe"; Parameters: "--check"
Name: "{autoprograms}\SW-MATE AI\Gỡ cài đặt"; Filename: "{uninstallexe}"

[UninstallRun]
; Unregister while the DLLs still exist. Never delete the user's CAD documents.
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: "/nologo /unregister ""{app}\SwMateAI.AddIn.dll"""; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterSWMateAI"
; SOLIDWORKS per-user auto-load preference is left under user control.

[Code]
function IsSolidWorksRunning(): Boolean;
var
  Locator, Services, Processes: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Services := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Services.ExecQuery('SELECT * FROM Win32_Process WHERE Name = "SLDWORKS.exe"');
    Result := Processes.Count > 0;
  except
    ; { If WMI is unavailable, the installer will not forcibly close SOLIDWORKS. }
  end;
end;

function InitializeSetup(): Boolean;
var
  ReleaseNumber: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', ReleaseNumber) and (ReleaseNumber >= 528040);
  if not Result then
    MsgBox('SW-MATE AI yêu cầu .NET Framework 4.8 trở lên trên Windows 64-bit.',
      mbCriticalError, MB_OK);
end;

function InitializeUninstall(): Boolean;
begin
  Result := not IsSolidWorksRunning();
  if not Result then
    MsgBox('Hãy lưu tài liệu và đóng SOLIDWORKS trước khi gỡ SW-MATE AI.',
      mbCriticalError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if IsSolidWorksRunning() then
    Result := 'Hãy lưu tài liệu và đóng SOLIDWORKS trước khi cài hoặc cập nhật SW-MATE AI.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  RegAsmPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    RegAsmPath := ExpandConstant('{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe');
    if not Exec(RegAsmPath,
      '/nologo /codebase "' + ExpandConstant('{app}\SwMateAI.AddIn.dll') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
      RaiseException('Không đăng ký được SW-MATE AI với SOLIDWORKS. RegAsm exit code: ' +
        IntToStr(ExitCode));
  end;
end;
