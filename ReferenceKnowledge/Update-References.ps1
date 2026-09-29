$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'sources'
New-Item -ItemType Directory -Force -Path $root | Out-Null

$repos = [ordered]@{
  'solidworks-automation-skill' = 'https://github.com/wzyn20051216/solidworks-automation-skill.git'
  'mcp-server-solidworks' = 'https://github.com/eyfel/mcp-server-solidworks.git'
  'SolidworksMCP-python' = 'https://github.com/andrewbartels1/SolidworksMCP-python.git'
  'SolidworksMCP-TS' = 'https://github.com/vespo92/SolidworksMCP-TS.git'
  'xcad-examples' = 'https://github.com/xarial/xcad-examples.git'
  'solidworks-api-angelsix' = 'https://github.com/angelsix/solidworks-api.git'
  'AgentSCAD' = 'https://github.com/Kevoyuan/AgentSCAD.git'
  'Text2CAD' = 'https://github.com/MLYengineering/Text2CAD.git'
  'freecad-robust-mcp' = 'https://github.com/spkane/freecad-addon-robust-mcp-server.git'
}

foreach ($name in $repos.Keys) {
  $dest = Join-Path $root $name
  if (Test-Path (Join-Path $dest '.git')) {
    Write-Host "UPDATE $name"
    git -C $dest fetch --depth 1 origin
    git -C $dest reset --hard origin/HEAD
  } else {
    Write-Host "CLONE $name"
    git clone --depth 1 $repos[$name] $dest
  }
}
