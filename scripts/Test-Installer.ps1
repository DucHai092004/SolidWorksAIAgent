# Offline package verification. Does not install or change COM registry.
param([string]$Version = '0.2.0')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $repo "dist\SW-MATE-AI-Setup-$Version.exe"
$hashFile = "$exe.sha256"
$updater = Join-Path $repo 'artifacts\installer-stage\SwMateAI.Updater.exe'
$addin = Join-Path $repo 'artifacts\installer-stage\SwMateAI.AddIn.dll'
$results = New-Object 'System.Collections.Generic.List[string]'
function Check([bool]$ok, [string]$name) {
    $script:results.Add(($(if ($ok) {'PASS'} else {'FAIL'}) + ' ' + $name))
    if (!$ok) { throw "FAILED: $name" }
}
Check (Test-Path $exe) 'Setup EXE exists'
Check (Test-Path $hashFile) 'SHA256 sidecar exists'
Check (Test-Path $updater) 'Updater included'
Check (Test-Path $addin) 'AddIn included'
Check ((Get-Item $exe).Length -gt 1000000) 'Setup EXE has plausible size'
$expected = ((Get-Content $hashFile -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Check ($expected -eq $actual) 'Setup SHA256 matches'
$addinVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($addin).FileVersion
$updaterVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($updater).FileVersion
Check ($addinVersion.StartsWith("$Version.")) 'AddIn version matches setup'
Check ($updaterVersion.StartsWith("$Version.")) 'Updater version matches setup'
$testProcess = Start-Process -FilePath $updater -ArgumentList '--self-test' -Wait -PassThru
Check ($testProcess.ExitCode -eq 0) 'Updater parser, unsafe URL and SHA256 self-test'
$iss = Get-Content (Join-Path $repo 'installer\SW-MATE-AI.iss') -Raw
Check ($iss.Contains('AppId={#MyAppId}')) 'Stable AppId configured'
Check ($iss.Contains('Framework64\v4.0.30319\RegAsm.exe')) 'x64 COM registration configured'
Check ($iss.Contains('CloseApplications=no')) 'Installer will not forcibly close SOLIDWORKS'
Check ($iss.Contains('IsSolidWorksRunning()')) 'Running SOLIDWORKS is checked'
$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe'
$regfile = Join-Path $repo 'dist\SwMateAI.AddIn.reg'
& $regasm $addin "/regfile:$regfile" | Out-Null
Check ($LASTEXITCODE -eq 0) 'RegAsm dry-run generates COM registry script'
Check ([bool](Select-String -Path $regfile -Pattern '5C9A5E3B-4F8A-4B2C-9D1E-A7F2B3C4D5E6' -Quiet)) 'Expected AddIn COM GUID present'
$report = Join-Path $repo 'dist\INSTALLER_TEST_REPORT.txt'
$results.Add('PENDING: COM registration, SolidWorks runtime, in-place upgrade, live GitHub Release')
$results | Set-Content -Path $report -Encoding UTF8
Get-Content $report
