#Requires -Version 7.4
# Maintainer operation only: never use this script to repair locks inside CI.
param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true') {
  throw 'Lock regeneration is a local maintainer operation. CI must validate committed lockfiles.'
}
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  function Invoke-DotNet { & $DotNet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" } }
  $locks = @('src/Inspector.Core/packages.lock.json','src/Inspector.App/packages.lock.json','tests/Inspector.Tests/packages.lock.json')
  $before = @{}
  foreach ($path in $locks) { $before[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
  # A changed app dependency also changes the test project's referenced dependency graph.
  Invoke-DotNet restore LaptopInspector.slnx --force-evaluate '-p:RestoreLockedMode=false'
  Invoke-DotNet restore src/Inspector.App/Inspector.App.csproj --force-evaluate -r win-x64 '-p:SelfContained=true' '-p:RestoreLockedMode=false'
  & (Join-Path $PSScriptRoot 'Restore-LockedDependencies.ps1') -DotNet $DotNet
  Invoke-DotNet restore src/Inspector.App/Inspector.App.csproj --locked-mode --force -r win-x64 '-p:SelfContained=true'
  foreach ($path in $locks) {
    $after = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($before[$path] -cne $after) { Write-Host "Review changed lockfile: $path" }
  }
  Write-Host 'Locked restores passed. Review licences/notices, diff all lockfiles, then run the full test/package checks before committing.'
} finally { Pop-Location }
