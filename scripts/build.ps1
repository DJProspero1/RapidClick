param(
    [string]$DotnetPath = "dotnet",
    [switch]$NativeTests
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if ($DotnetPath -eq "dotnet") {
        $localSdk = Join-Path $env:LOCALAPPDATA "RapidClickBuild\dotnet\dotnet.exe"
        if (Test-Path -LiteralPath $localSdk) { $DotnetPath = $localSdk }
    }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    & $DotnetPath build RapidClick.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    $testArgs = @("run", "--project", "tests/RapidClick.Tests", "-c", "Release", "--no-build")
    if ($NativeTests) { $testArgs += @("--", "--native") }
    & $DotnetPath @testArgs
    if ($LASTEXITCODE -ne 0) { throw "Verification failed." }
    & $DotnetPath publish src/RapidClick/RapidClick.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    $exe = Join-Path $projectRoot "artifacts/win-x64/RapidClick.exe"
    $smoke = Start-Process -FilePath $exe -ArgumentList @("--smoke-test", "--screenshot", "test-results/release-ui.png") -PassThru -Wait
    if ($smoke.ExitCode -ne 0) { throw "Packaged application smoke test failed." }
    New-Item -ItemType Directory -Path artifacts/release -Force | Out-Null
    Copy-Item -LiteralPath $exe -Destination artifacts/release/RapidClick-1.0.0-win-x64.exe -Force
    Compress-Archive -LiteralPath artifacts/release/RapidClick-1.0.0-win-x64.exe,README.md,LICENSE -DestinationPath artifacts/release/RapidClick-1.0.0-win-x64.zip -Force
    Get-ChildItem artifacts/release -File | Where-Object Extension -In ".exe", ".zip" | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), $_.Name
    } | Set-Content artifacts/release/SHA256SUMS.txt -Encoding ascii
    Write-Host "Local release ready in artifacts/release. Nothing was uploaded."
} finally { Pop-Location }
