#Requires -Version 7.4
param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  # --force rechecks assets on warm workspaces; it does not refresh trusted locks.
  & $DotNet restore LaptopInspector.slnx --locked-mode --force
  if ($LASTEXITCODE -ne 0) {
    Write-Host 'Restore failed. CI will not regenerate or bypass package lockfiles.'
    Write-Host 'For NU1004 after a reviewed dependency change, refresh the entire solution locally:'
    Write-Host './scripts/Update-DependencyLocks.ps1'
    Write-Host 'Review every changed lockfile, dependency licence and release notice; commit the reviewed changes to the same PR.'
    throw 'Locked dependency validation failed. Resolve the reported error before merging.'
  }
} finally { Pop-Location }
