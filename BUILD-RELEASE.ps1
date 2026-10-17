$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Das .NET 8 SDK fehlt. Bitte installieren und erneut starten.' }
$project = Join-Path $PSScriptRoot 'NanoSonarController.csproj'
$publish = Join-Path $PSScriptRoot 'publish'
$release = Join-Path $PSScriptRoot 'nanoSONAR-v1.1-win-x64.zip'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen. Siehe Fehlermeldungen oben.' }
if (-not (Test-Path (Join-Path $publish 'nanoSONAR.exe'))) { throw 'nanoSONAR.exe wurde nicht erstellt.' }
if (Test-Path $release) { Remove-Item $release -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $release -CompressionLevel Optimal
Write-Host "Fertig: $release" -ForegroundColor Green
