param(
    [string]$ProjectRoot = 'E:\SolidWorksAIAgent'
)

$guid = '{5C9A5E3B-4F8A-4B2C-9D1E-A7F2B3C4D5E6}'
$dll = Join-Path $ProjectRoot 'src\SwMateAI.AddIn\bin\x64\Debug\net48\SwMateAI.AddIn.dll'
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

$codeBase = 'file:///' + ($dll -replace '\\','/')
$base = "Registry::HKEY_CURRENT_USER\Software\Classes\CLSID\$guid\InprocServer32"
$version = Join-Path $base '0.1.0.0'
if (-not (Test-Path $base)) { throw "SW-MATE AI COM registration not found in HKCU." }

Set-ItemProperty -Path $base -Name CodeBase -Value $codeBase
if (Test-Path $version) { Set-ItemProperty -Path $version -Name CodeBase -Value $codeBase }

Write-Host "SW-MATE AI development CodeBase: $codeBase"
Write-Host 'Restart or reload the add-in in SOLIDWORKS to load the rebuilt DLLs.'