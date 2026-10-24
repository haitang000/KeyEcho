param([switch]$Publish, [switch]$Installer)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw 'Run scripts/bootstrap.ps1 first.' }
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $root
try {
    & $dotnet build KeyEcho.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $dotnet test tests/KeyEcho.Tests/KeyEcho.Tests.csproj -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if ($Publish -or $Installer) {
        & $dotnet publish src/KeyEcho.App/KeyEcho.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/release-1.2.5 -p:PublishTrimmed=false
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    }
    if ($Installer) {
        $iscc = Join-Path $root '.tools\innosetup\ISCC.exe'
        if (-not (Test-Path $iscc)) { throw 'Run scripts/bootstrap.ps1 -WithInstaller first.' }
        & $iscc /Qp '/DPublishDir=..\artifacts\release-1.2.5' installer/KeyEcho.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    }
} finally { Pop-Location }
