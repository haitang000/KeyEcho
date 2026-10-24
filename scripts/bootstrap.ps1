param([switch]$WithInstaller)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$toolDir = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $toolDir | Out-Null
$dotnetDir = Join-Path $toolDir 'dotnet'
if (-not (Test-Path (Join-Path $dotnetDir 'dotnet.exe'))) {
    $releases = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $sdk = $releases.releases[0].sdk
    $file = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name.EndsWith('.zip') } | Select-Object -First 1
    if (-not $file) { throw 'Could not locate the Windows x64 SDK.' }
    $archive = Join-Path $toolDir 'dotnet-sdk.zip'
    Write-Host "Downloading .NET SDK $($sdk.version) ..."
    Invoke-WebRequest $file.url -OutFile $archive
    if ((Get-FileHash $archive -Algorithm SHA512).Hash -ne $file.hash) { throw 'SDK checksum mismatch.' }
    New-Item -ItemType Directory -Force -Path $dotnetDir | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $dotnetDir -Force
}
& (Join-Path $dotnetDir 'dotnet.exe') --version
if ($WithInstaller -and -not (Test-Path (Join-Path $toolDir 'innosetup\ISCC.exe'))) {
    $installer = Join-Path $toolDir 'innosetup.exe'
    Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid') { throw "Invalid Inno Setup signature: $($signature.Status)" }
    $target = Join-Path $toolDir 'innosetup'
    $process = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',"/DIR=`"$target`"") -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Inno Setup bootstrap failed: $($process.ExitCode)" }
}
