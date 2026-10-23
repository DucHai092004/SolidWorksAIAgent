# Build SW-MATE AI AddIn + updater and compile a single x64 setup EXE.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1 -Version 0.2.0
param([string]$Version = '0.2.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Version must be numeric MAJOR.MINOR.PATCH, e.g. 0.2.0'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (!(Test-Path $msbuild)) { throw 'Visual Studio 2022 MSBuild not found.' }
if (!(Test-Path $iscc)) {
    $iscc = Get-ChildItem @('C:\Program Files (x86)','C:\Program Files',$env:LOCALAPPDATA) -Filter ISCC.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
}
if (!$iscc -or !(Test-Path $iscc)) { throw 'Inno Setup 6 compiler ISCC.exe not found.' }
$stage = Join-Path $repo 'artifacts\installer-stage'
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
Push-Location $repo
try {
    & $msbuild '.\src\SwMateAI.AddIn\SwMateAI.AddIn.csproj' /restore /t:Rebuild /p:Configuration=Release /p:Platform=x64 /p:XCadRegDll=false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'AddIn Release build FAILED' }
    & $msbuild '.\src\SwMateAI.Updater\SwMateAI.Updater.csproj' /restore /t:Rebuild /p:Configuration=Release /p:Platform=x64 /p:XCadRegDll=false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Updater Release build FAILED' }

    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $addinOut = Join-Path $repo 'src\SwMateAI.AddIn\bin\x64\Release\net48'
    $updaterOut = Join-Path $repo 'src\SwMateAI.Updater\bin\x64\Release\net48'
    if (!(Test-Path $addinOut)) { throw "Missing AddIn output: $addinOut" }
    Get-ChildItem $addinOut -File | Where-Object { $_.Extension -notin @('.pdb','.xml') } | Copy-Item -Destination $stage -Force
    Copy-Item (Join-Path $updaterOut 'SwMateAI.Updater.exe') $stage -Force
    $config = Join-Path $updaterOut 'SwMateAI.Updater.exe.config'
    if (Test-Path $config) { Copy-Item $config $stage -Force }
    foreach ($file in @('SwMateAI.AddIn.dll','SwMateAI.Core.dll','SwMateAI.UI.dll','SwMateAI.Updater.exe')) {
        if (!(Test-Path (Join-Path $stage $file))) { throw "Missing staged dependency: $file" }
    }

    & $iscc "/DMyAppVersion=$Version" '.\installer\SW-MATE-AI.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation FAILED' }
    $exe = Join-Path $dist "SW-MATE-AI-Setup-$Version.exe"
    if (!(Test-Path $exe)) { throw "Installer EXE missing: $exe" }
    $sha = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$exe.sha256" -Value "$sha  $(Split-Path $exe -Leaf)" -Encoding ASCII
    Write-Host "INSTALLER PASS: $exe"
    Write-Host "SHA256: $sha"
}
finally { Pop-Location }
