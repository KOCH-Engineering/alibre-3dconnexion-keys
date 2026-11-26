# Removes the KOCH 3Dconnexion Keys add-on (KOCH Engineering).
# Your settings (%APPDATA%\Koch Engineering\SpaceMouse) and 3DxWare profile backups are kept.
$ErrorActionPreference = 'Stop'
$AddOnId    = '{C79D2D04-31E5-4887-83CC-6B2F46C2E768}'
$InstallDir = Join-Path $env:ProgramData 'Alibre AddOns\KochSpaceMouse'

if (Get-Process -Name 'Alibre Design' -ErrorAction SilentlyContinue) { throw 'Close Alibre Design first.' }

$key = 'HKLM:\SOFTWARE\Alibre Design Add-Ons'
if (Get-ItemProperty -Path $key -Name $AddOnId -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $key -Name $AddOnId
    Write-Host "Unregistered $AddOnId"
}
if (Test-Path $InstallDir) {
    Remove-Item -Recurse -Force $InstallDir
    Write-Host "Removed $InstallDir"
}

# Take the KOCH_ entries out of the 3DxWare profile, so the keys go back to the 3DxWare defaults.
$cfg = Join-Path $env:APPDATA '3Dconnexion\3DxWare\Cfg'
Get-ChildItem $cfg -Filter *.xml -ErrorAction SilentlyContinue | ForEach-Object {
    $text = Get-Content -Raw -Encoding UTF8 $_.FullName
    if ($text -notmatch 'KOCH_') { return }
    [xml]$x = $text
    $removed = 0
    foreach ($n in @($x.SelectNodes("//*[starts-with(normalize-space(ID),'KOCH_')]"))) { [void]$n.ParentNode.RemoveChild($n); $removed++ }
    foreach ($n in @($x.SelectNodes("//Button[starts-with(normalize-space(Output/ActionID),'KOCH_')]"))) { [void]$n.ParentNode.RemoveChild($n); $removed++ }
    if ($removed -gt 0) {
        Copy-Item $_.FullName ($_.FullName + '.before-uninstall.bak') -Force
        $x.Save($_.FullName)
        Write-Host "Cleaned $removed KOCH entries from $($_.Name)"
    }
}
Write-Host 'Done. Settings in %APPDATA%\Koch Engineering\SpaceMouse were kept - delete that folder if you do not need them.'
