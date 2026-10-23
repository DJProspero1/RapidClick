param([string]$Destination = "$env:LOCALAPPDATA\RapidClickBuild\native-tools")
$ErrorActionPreference = 'Stop'
$version = '0.15.2'
$expected = '3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c'
$archive = Join-Path $env:TEMP "rapidclick-zig-$version.zip"
Invoke-WebRequest "https://ziglang.org/download/$version/zig-x86_64-windows-$version.zip" -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Zig download checksum mismatch.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
tar -xf $archive -C $Destination
if ($LASTEXITCODE -ne 0) { throw 'Zig extraction failed.' }
& (Join-Path $Destination "zig-x86_64-windows-$version\zig.exe") version
