# Stage a DRAFT GitHub Release after the code has been pushed and package tests pass.
# Publishing to production must be a separate explicit action after runtime tests.
param([string]$Version = '0.2.0')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $repo "dist\SW-MATE-AI-Setup-$Version.exe"
if (!(Test-Path $exe) -or !(Test-Path "$exe.sha256")) { throw 'Build and test the installer first.' }
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI gh not found.' }
Push-Location $repo
try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File '.\scripts\Test-Installer.ps1' -Version $Version
    if ($LASTEXITCODE -ne 0) { throw 'Installer offline tests failed.' }
    $tag = "v$Version"
    & gh release view $tag --repo DucHai092004/SolidWorksAIAgent 2>$null
    if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists. Do not overwrite without review." }
    $commit = (git rev-parse HEAD).Trim()
    & gh release create $tag $exe "$exe.sha256" --repo DucHai092004/SolidWorksAIAgent --target $commit --draft --title "SW-MATE AI $tag (installer preview)" --notes "Installer EXE x64 for SOLIDWORKS 2021. Draft pending COM registration, runtime installation and upgrade verification."
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage draft release.' }
    Write-Host "DRAFT RELEASE STAGED: $tag. Do not publish until live install/upgrade tests pass."
}
finally { Pop-Location }
