param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "src\SwMateAI.sln"
$tests = Join-Path $root "src\SwMateAI.Core.Tests\SwMateAI.Core.Tests.csproj"
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "=== SW-MATE AI automated verification ===" -ForegroundColor Cyan

if (-not $SkipBuild) {
    Write-Host "[1/2] Building full solution without COM registration..."
    & $msbuild $solution /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:XCadRegDll=false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }
}

Write-Host "[2/2] Running Core automated tests..."
dotnet test $tests -c Debug --no-restore --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Automated tests failed." }

Write-Host "PASS: build + automated Core tests." -ForegroundColor Green
