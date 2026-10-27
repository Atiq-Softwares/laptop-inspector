#Requires -Version 7.4
param(
  [Parameter(Mandatory)][string]$Archive,
  [string]$ChecksumFile,
  [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSha256,
  [string]$Executable,
  [switch]$RequireSignature,
  [ValidatePattern('^[a-fA-F0-9]{40}$')][string]$ExpectedSignerThumbprint
)
$ErrorActionPreference = 'Stop'
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$name = [IO.Path]::GetFileName($archivePath)
if (-not $ExpectedSha256) {
  if (-not $ChecksumFile) { $ChecksumFile = Join-Path ([IO.Path]::GetDirectoryName($archivePath)) 'SHA256SUMS.txt' }
  $checksumCandidates = @()
  foreach ($line in Get-Content -LiteralPath $ChecksumFile) {
    if ($line -match '^([a-fA-F0-9]{64})  ([^/\:]+\.zip)$' -and $Matches[2] -ceq $name) { $checksumCandidates += $Matches[1] }
  }
  if ($checksumCandidates.Count -ne 1) { throw 'Checksum manifest must identify this ZIP exactly once' }
  $ExpectedSha256 = $checksumCandidates[0]
}
$actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actual -ine $ExpectedSha256) { throw 'SHA-256 mismatch. Do not run this download.' }
Write-Host "SHA-256 integrity match: $($actual.ToLowerInvariant())"
Write-Host 'A matching checksum alone does not authenticate the publisher.'
if ($RequireSignature -and (-not $Executable -or -not $ExpectedSignerThumbprint)) { throw 'Signature enforcement requires an extracted EXE and a trusted publisher thumbprint' }
if ($Executable) {
  $exePath = (Resolve-Path -LiteralPath $Executable).Path
  Add-Type -AssemblyName System.IO.Compression
  $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
  try {
    $entries = @($zip.Entries | Where-Object { $_.FullName -ceq 'Atiq-Laptop-Inspector.exe' })
    if ($entries.Count -ne 1 -or $entries[0].Length -gt 512MB) { throw 'Unexpected application ZIP entry' }
    $stream = $entries[0].Open()
    try { $inside = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
    if ((Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash -cne $inside) { throw 'Extracted executable does not match the verified ZIP' }
  } finally { $zip.Dispose() }
  $signature = Get-AuthenticodeSignature -LiteralPath $exePath
  Write-Host "Authenticode status: $($signature.Status)"
  if ($signature.Status -eq 'Valid') { Write-Host "Certificate subject: $($signature.SignerCertificate.Subject)" }
  if ($RequireSignature) {
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ine $ExpectedSignerThumbprint) { throw 'Trusted publisher signature was not established' }
    Write-Host 'The executable has a valid signature matching the explicitly trusted publisher thumbprint.'
  } elseif ($signature.Status -ne 'Valid') { Write-Host 'This executable has no validated Authenticode publisher identity.' }
}
