#Requires -Version 7.4
param([ValidateLength(3,48)][ValidatePattern('^[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:-[A-Za-z0-9.-]+)?$')][string]$Version = '1.1', [string]$DotNet = 'dotnet', [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
  function Invoke-DotNet { & $DotNet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" } }
  Invoke-DotNet restore LaptopInspector.slnx --locked-mode
  if (-not $SkipTests) { Invoke-DotNet test LaptopInspector.slnx -c Release --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/TestResults }
  $stage = Join-Path $root 'artifacts/portable'
  if (Test-Path -LiteralPath $stage) {
    $resolved = (Resolve-Path -LiteralPath $stage).Path
    $allowed = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
  }
  Invoke-DotNet restore src/Inspector.App/Inspector.App.csproj --locked-mode -r win-x64 '-p:SelfContained=true'
  Invoke-DotNet publish src/Inspector.App/Inspector.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o $stage
  Copy-Item -LiteralPath LICENSE,THIRD-PARTY-NOTICES.txt -Destination $stage
  Copy-Item -LiteralPath docs/PORTABLE-README.txt -Destination (Join-Path $stage 'README.txt')
  New-Item -ItemType Directory -Force -Path (Join-Path $stage 'licenses') | Out-Null
  Copy-Item -Path 'licenses/*' -Destination (Join-Path $stage 'licenses')
  $exe = Join-Path $stage 'Atiq-Laptop-Inspector.exe'
  $process = Start-Process -FilePath $exe -ArgumentList '--engine-check' -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Drive engine integrity check timed out' }
  if ($process.ExitCode -ne 0) { throw 'Bundled SMART engine or drive database is missing or has changed' }
  $process = Start-Process -FilePath $exe -ArgumentList '--pipe-self-test' -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Same-user diagnostic channel check timed out' }
  if ($process.ExitCode -ne 0) { throw 'The one-shot drive diagnostic channel failed' }
  $process = Start-Process -FilePath $exe -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Packaged startup timed out' }
  if ($process.ExitCode -ne 0) { throw "Packaged startup failed: $($process.ExitCode)" }
  $process = Start-Process -FilePath $exe -ArgumentList @('--demo','--smoke-test') -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Sample view smoke test timed out' }
  if ($process.ExitCode -ne 0) { throw 'Sample views or theme switching failed' }
  $sample = Join-Path $root 'artifacts/sample'
  New-Item -ItemType Directory -Force -Path $sample | Out-Null
  $process = Start-Process -FilePath $exe -ArgumentList @('--sample-report', ('"' + $sample + '"')) -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Sample report generation timed out' }
  if ($process.ExitCode -ne 0) { throw "Sample report generation failed: $($process.ExitCode)" }
  foreach ($extension in @('pdf','html','json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sample "Sample-Inspection-Report.$extension"))) { throw "Missing $extension export" }
  }
  & (Join-Path $PSScriptRoot 'Test-ReleasePayload.ps1') -Path $stage
  $payload = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); bytes = $_.Length } })
  $signature = Get-AuthenticodeSignature -LiteralPath $exe
  @{schema=1;product='Atiq Laptop Inspector';version=$Version;authenticodeStatus=$signature.Status.ToString();files=$payload} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'PAYLOAD-SHA256.json') -Encoding utf8NoBOM
  & (Join-Path $PSScriptRoot 'Test-ReleasePayload.ps1') -Path $stage
  $zip = Join-Path $root "artifacts/Atiq-Laptop-Inspector-v$Version-Windows-x64.zip"
  Compress-Archive -Path "$stage/*" -DestinationPath $zip -Force
  # Validate the ZIP by extracting to a fresh directory and starting the extracted executable.
  $verify = Join-Path $root ('artifacts/verify-' + [guid]::NewGuid().ToString('N'))
  Expand-Archive -LiteralPath $zip -DestinationPath $verify
  $process = Start-Process -FilePath (Join-Path $verify 'Atiq-Laptop-Inspector.exe') -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Extracted startup timed out' }
  if ($process.ExitCode -ne 0) { throw 'Extracted application did not start' }
  $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
  "$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath (Join-Path $root 'artifacts/SHA256SUMS.txt') -Encoding ascii
  & (Join-Path $PSScriptRoot 'Verify-Release.ps1') -Archive $zip -ChecksumFile (Join-Path $root 'artifacts/SHA256SUMS.txt') -Executable (Join-Path $verify 'Atiq-Laptop-Inspector.exe')
  Write-Host "Portable package verified locally; this script does not publish releases: $zip"
} finally { Pop-Location }
