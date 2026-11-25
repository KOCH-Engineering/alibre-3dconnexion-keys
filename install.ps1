# Builds and installs the "KOCH 3Dconnexion Keys" add-on for Alibre Design (KOCH Engineering).
# Easiest: double-click install.cmd. Requires the .NET SDK.
#
# If Alibre Design is running, only the core is updated (no restart needed):
# then click Add-on > SpaceMouse > Reload add-on in Alibre.

$ErrorActionPreference = 'Stop'

$AddOnId    = '{C79D2D04-31E5-4887-83CC-6B2F46C2E768}'
$InstallDir = Join-Path $env:ProgramData 'Alibre AddOns\KochSpaceMouse'
$Source     = Join-Path $PSScriptRoot 'Source'
$LoaderOut  = Join-Path $Source 'Loader\bin\Release\net48'
$CoreOut    = Join-Path $Source 'Core\bin\Release\net48'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run as administrator (the add-on is installed under ProgramData and registered under HKLM).' }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is missing. Install it from https://dotnet.microsoft.com/download and run this again.'
}

$alibreRunning = [bool](Get-Process -Name 'Alibre Design' -ErrorAction SilentlyContinue)
$loaderInstalled = Test-Path (Join-Path $InstallDir 'KochSpaceMouse.dll')
$quick = $alibreRunning -and $loaderInstalled

# --- core (never locked by Alibre) ---
Write-Host 'Building core...' -ForegroundColor Cyan
dotnet build (Join-Path $Source 'Core\KochSpaceMouse.Core.vbproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed - see the output above.' }

if (-not $quick) {
    if ($alibreRunning) { throw 'First install: close Alibre Design and run this again.' }

    # Find the newest Alibre installation
    $alibre = Get-ChildItem (Join-Path $env:ProgramFiles 'Alibre Design*') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'Program\AlibreAddOn.dll') } |
        Sort-Object { [version](($_.Name -replace '^Alibre Design\s*','') -replace '[^0-9.]','') } -ErrorAction SilentlyContinue |
        Select-Object -Last 1
    if (-not $alibre) { throw 'No Alibre Design installation was found under Program Files.' }
    $progPath = (Join-Path $alibre.FullName 'Program') + '\'
    Write-Host "Building loader against $progPath" -ForegroundColor Cyan
    dotnet build (Join-Path $Source 'Loader\KochSpaceMouse.vbproj') -c Release ("-p:AlibreProgramPath=" + $progPath + '\')   # extra \ so the trailing \ does not escape the quote
    if ($LASTEXITCODE -ne 0) { throw 'Build failed - see the output above.' }
}

Write-Host "Copying to $InstallDir" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item (Join-Path $CoreOut 'KochSpaceMouse.Core.dll') $InstallDir -Force
Copy-Item (Join-Path $CoreOut 'defaults') $InstallDir -Recurse -Force

if ($quick) {
    Write-Host ''
    Write-Host 'Alibre Design is running: the core was updated.' -ForegroundColor Green
    Write-Host 'In Alibre click  Add-on > SpaceMouse > Reload add-on  to use it.' -ForegroundColor Green
    exit 0
}

Copy-Item (Join-Path $LoaderOut 'KochSpaceMouse.dll') $InstallDir -Force
Copy-Item (Join-Path $LoaderOut 'KochSpaceMouse.adc') $InstallDir -Force
Copy-Item (Join-Path $LoaderOut 'SpaceMouse.ico') $InstallDir -Force
# leftovers from the prototype
Remove-Item (Join-Path $InstallDir 'images') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'Registering the add-on with Alibre...' -ForegroundColor Cyan
$key = 'HKLM:\SOFTWARE\Alibre Design Add-Ons'
if (-not (Test-Path $key)) { New-Item -Path $key -Force | Out-Null }
New-ItemProperty -Path $key -Name $AddOnId -Value $InstallDir -PropertyType String -Force | Out-Null

Write-Host ''
Write-Host 'Done. Start Alibre Design and click  Add-on > 3Dconnexion > 3Dconnexion keys...' -ForegroundColor Green
