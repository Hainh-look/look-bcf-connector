; =====================================================================
; Look BCF Revit Add-In Installer Script (Inno Setup 6)
; Supports Autodesk Revit 2021 - 2026 (net48 and net8.0-windows)
; =====================================================================

#define MyAppName      "Look BCF"
#define MyAppFullName  "Look BCF for Autodesk Revit"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "LookSpace BIM"
#define MyAppURL       "https://bim.lookbim.com"

[Setup]
AppId={{5F96A79F-0E28-4D02-BE10-251C8032A270}
AppName={#MyAppFullName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Install to current user's Revit Addins directory (no admin privileges needed)
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DefaultGroupName={#MyAppFullName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=..\dist
OutputBaseFilename=LookBcf-Setup-v{#MyAppVersion}
SetupIconFile=..\Assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
ChangesAssociations=no
CloseApplications=yes
CloseApplicationsFilter=Revit.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Install for all detected Revit versions"
Name: "custom"; Description: "Custom selection"; Flags: iscustom

[Components]
Name: "revit2021"; Description: "Autodesk Revit 2021 (.NET Framework 4.8)"; Types: full custom; Flags: checkablealone
Name: "revit2022"; Description: "Autodesk Revit 2022 (.NET Framework 4.8)"; Types: full custom; Flags: checkablealone
Name: "revit2023"; Description: "Autodesk Revit 2023 (.NET Framework 4.8)"; Types: full custom; Flags: checkablealone
Name: "revit2024"; Description: "Autodesk Revit 2024 (.NET Framework 4.8)"; Types: full custom; Flags: checkablealone
Name: "revit2025"; Description: "Autodesk Revit 2025 (.NET 8.0 Windows)"; Types: full custom; Flags: checkablealone
Name: "revit2026"; Description: "Autodesk Revit 2026 (.NET 8.0 Windows)"; Types: full custom; Flags: checkablealone

[Files]
; Revit 2021 (.NET 4.8)
Source: "..\src\OpenProject.Revit\bin\Release\net48\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2021
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2021"; Flags: ignoreversion; Components: revit2021

; Revit 2022 (.NET 4.8)
Source: "..\src\OpenProject.Revit\bin\Release\net48\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2022
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion; Components: revit2022

; Revit 2023 (.NET 4.8)
Source: "..\src\OpenProject.Revit\bin\Release\net48\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2023
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion; Components: revit2023

; Revit 2024 (.NET 4.8)
Source: "..\src\OpenProject.Revit\bin\Release\net48\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2024
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion; Components: revit2024

; Revit 2025 (.NET 8.0)
Source: "..\src\OpenProject.Revit\bin\Release\net8.0-windows\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2025
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion; Components: revit2025

; Revit 2026 (.NET 8.0)
Source: "..\src\OpenProject.Revit\bin\Release\net8.0-windows\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\LookBcf"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: revit2026
Source: "..\InnoSetup\LookBcf.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Components: revit2026

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2021\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2021\LookBcf.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2022\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2022\LookBcf.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2023\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2023\LookBcf.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\LookBcf.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\LookBcf.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\LookBcf"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\LookBcf.addin"

[Code]
// Check if Autodesk Revit is currently running using WMI
function IsRevitRunning(): Boolean;
var
  FSWbemLocator: Variant;
  FWMIService: Variant;
  FWbemObjectSet: Variant;
begin
  Result := False;
  try
    FSWbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    FWbemObjectSet := FWMIService.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = "Revit.exe"');
    Result := (FWbemObjectSet.Count > 0);
  except
    Result := False;
  end;
end;

// Pre-flight check: ensure Revit is closed before installing to prevent file locking
function InitializeSetup(): Boolean;
begin
  while IsRevitRunning() do
  begin
    if MsgBox('Phan mem Autodesk Revit dang chay.'#13#13 +
              'Vui long luu cong viec va dong Revit truoc khi tiep tuc cai dat Look BCF.'#13#13 +
              'Nhan "Retry" sau khi da dong Revit, hoac "Cancel" de thoat.',
              mbConfirmation, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;

