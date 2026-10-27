#Requires -Version 7.4
param([Parameter(Mandatory)][string]$Path, [string]$PolicyPath = (Join-Path $PSScriptRoot '../policy/release-payload.json'))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Redirected payload roots are not allowed' }
$policy = Get-Content -LiteralPath $PolicyPath -Raw | ConvertFrom-Json
if ($policy.schema -ne 1 -or $policy.product -ne 'Atiq Laptop Inspector') { throw 'Unknown release policy' }
$expected = @{}
foreach ($entry in $policy.staticFiles) { $expected[$entry.path] = $entry }
$allowed = @($expected.Keys) + @($policy.executables) + @($policy.generatedFiles)
$found = @{}
$pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($root)
while ($pending.Count) {
  foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Redirected payload item: $($entry.Name)" }
    $relative = [IO.Path]::GetRelativePath($root, $entry.FullName).Replace('\','/')
    if ($entry.PSIsContainer) {
      if (-not ($allowed | Where-Object { $_.StartsWith($relative + '/', [StringComparison]::Ordinal) })) { throw "Unexpected payload directory: $relative" }
      $pending.Push($entry.FullName); continue
    }
    if ([IO.Path]::GetExtension($relative).ToLowerInvariant() -in @('.cs','.vb','.fs','.pdb','.snupkg','.pfx','.p12','.snk','.key','.pem','.map','.user','.suo','.csproj','.props','.targets','.sln','.slnx','.yml','.yaml') -or $entry.Name -like '.env*') { throw "Forbidden source, symbol, key or build file: $relative" }
    if ($relative -cnotin $allowed) { throw "Unapproved release file: $relative" }
    $found[$relative] = $true
    if ($expected.ContainsKey($relative)) {
      $rule = $expected[$relative]
      if ($rule.mode -eq 'bytes') { $hash = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash }
      elseif ($rule.mode -eq 'text-lf') {
        $bytes = [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($entry.FullName).Replace("`r`n","`n"))
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
      } else { throw 'Unknown integrity mode' }
      if ($hash.ToLowerInvariant() -cne $rule.sha256) { throw "Changed required legal/vendor file: $relative" }
    }
  }
}
foreach ($required in @($expected.Keys) + @($policy.executables) + @('README.txt')) { if (-not $found.ContainsKey($required)) { throw "Missing required payload file: $required" } }
Write-Host "Payload allowlist and required third-party notices verified ($($found.Count) files)."
