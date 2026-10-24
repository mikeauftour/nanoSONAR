$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Das .NET 8 SDK wird zum einmaligen Erstellen benoetigt.' }
$project = Join-Path $root 'NanoSonarController.csproj'
$output = Join-Path $root 'dist/dotnet'
$zip = Join-Path $root 'dist/releases/nanoSONAR-v1.2-dotnet-win-x64.zip'
New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained false -p:UseAppHost=false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
$dll = Join-Path $output 'nanoSONAR.dll'
if (-not (Test-Path $dll)) { throw 'nanoSONAR.dll wurde nicht erstellt.' }
$version = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version
if ($version.Major -ne 1 -or $version.Minor -ne 2) { throw "Falsche Version: $version" }
$launcher = '@echo off' + "`r`n" + 'cd /d "%~dp0"' + "`r`n" + 'dotnet --list-runtimes | findstr /R /C:"Microsoft.WindowsDesktop.App 8\." >nul' + "`r`n" + 'if errorlevel 1 (' + "`r`n" + '  echo Die .NET 8 Desktop Runtime x64 fehlt.' + "`r`n" + '  pause' + "`r`n" + '  exit /b 1' + "`r`n" + ')' + "`r`n" + 'dotnet "%~dp0nanoSONAR.dll"' + "`r`n" + 'if errorlevel 1 pause' + "`r`n"
[System.IO.File]::WriteAllText((Join-Path $output 'nanoSONAR-starten.cmd'), $launcher, [System.Text.Encoding]::ASCII)
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Fertig: $zip" -ForegroundColor Green
Write-Host "Starten ohne Build: $output\nanoSONAR-starten.cmd" -ForegroundColor Green
