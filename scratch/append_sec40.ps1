$targetPath = "docs/refactoring-designs/20261106-seamless-intermediate-spec.md"
$content = [System.IO.File]::ReadAllText($targetPath, [System.Text.Encoding]::UTF8)

# Remove the section 40 if already present
$cutIdx = $content.IndexOf("### 40.")
if ($cutIdx -gt 0) {
    $content = $content.Substring(0, $cutIdx).TrimEnd() + "`n`n"
}

$sec40 = [System.IO.File]::ReadAllText("scratch/section40.md", [System.Text.Encoding]::UTF8)
$finalContent = $content.TrimEnd() + "`n`n" + $sec40.Trim() + "`n"

[System.IO.File]::WriteAllText($targetPath, $finalContent, [System.Text.Encoding]::UTF8)
Write-Output "Appended section 40 cleanly."
