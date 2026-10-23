param([string]$ZigPath = '', [switch]$GlobalInputProbe)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if (-not $ZigPath) {
        $localZig = Join-Path $env:LOCALAPPDATA 'RapidClickBuild\native-tools\zig-x86_64-windows-0.15.2\zig.exe'
        if (Test-Path -LiteralPath $localZig) { $ZigPath = $localZig }
        elseif (Get-Command zig -ErrorAction SilentlyContinue) { $ZigPath = (Get-Command zig).Source }
        else { throw 'Install Zig 0.15.2 or run scripts/bootstrap-native.ps1 first.' }
    }
    New-Item -ItemType Directory -Path artifacts/native,artifacts/release-v2,test-results -Force | Out-Null
    Push-Location native
    try { & $ZigPath rc /fo ../artifacts/native/resources.o /:output-format coff resources.rc }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'Resource build failed.' }
    & $ZigPath cc native/app.c native/engine.c artifacts/native/resources.o -std=c11 -Os -s -Wall -Wextra -Werror -municode '-Wl,--subsystem,windows' -o artifacts/native/RapidClick.exe -luser32 -lgdi32 -ldwmapi -lcomctl32 -ladvapi32
    if ($LASTEXITCODE -ne 0) { throw 'Native application build failed.' }
    & $ZigPath cc native/tests.c native/engine.c -std=c11 -O2 -Wall -Wextra -Werror -o artifacts/native/tests.exe -luser32
    if ($LASTEXITCODE -ne 0) { throw 'Native test build failed.' }
    & artifacts/native/tests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Silent native tests failed.' }
    python scripts/verify_ui.py artifacts/native/RapidClick.exe
    if ($LASTEXITCODE -ne 0) { throw 'Packaged UI checks failed.' }
    if ($GlobalInputProbe) {
        & $ZigPath cc native/probe.c -std=c11 -O2 -o artifacts/native/probe.exe -luser32 -lgdi32
        if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
        & artifacts/native/probe.exe | Set-Content test-results/global-input-probe.json -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw 'Global input probe canceled or failed.' }
    }
    $exe = 'artifacts/release-v2/RapidClick-2.0.0-win-x64.exe'
    $zip = 'artifacts/release-v2/RapidClick-2.0.0-win-x64.zip'
    Copy-Item -LiteralPath artifacts/native/RapidClick.exe -Destination $exe -Force
    Compress-Archive -LiteralPath $exe,README.md,LICENSE -DestinationPath $zip -Force
    @($exe,$zip) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; '{0}  {1}' -f $hash.Hash.ToLowerInvariant(),(Split-Path $_ -Leaf) } | Set-Content artifacts/release-v2/SHA256SUMS.txt -Encoding ascii
    Write-Host 'Local native release verified: artifacts/release-v2'
} finally { Pop-Location }
