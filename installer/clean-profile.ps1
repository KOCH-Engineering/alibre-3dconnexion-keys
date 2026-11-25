# KOCH 3Dconnexion Keys (KOCH Engineering) - removes the KOCH_ entries from the user's 3DxWare profiles,
# so the keys go back to the 3DxWare defaults. A .before-uninstall.bak copy is kept.
$cfg = Join-Path $env:APPDATA '3Dconnexion\3DxWare\Cfg'
Get-ChildItem $cfg -Filter *.xml -ErrorAction SilentlyContinue | ForEach-Object {
    try {
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
    } catch { Write-Host "Skipped $($_.Name): $_" }
}
