$targetPath = "docs/refactoring-designs/20261106-seamless-intermediate-spec.md"
$content = [System.IO.File]::ReadAllText($targetPath, [System.Text.Encoding]::UTF8)

# Remove the garbled section 39 if present
$cutIdx = $content.IndexOf("### 39.")
if ($cutIdx -gt 0) {
    $content = $content.Substring(0, $cutIdx).TrimEnd() + "`n`n"
}

$sec39 = [System.IO.File]::ReadAllText("scratch/section39.md", [System.Text.Encoding]::UTF8)
$finalContent = $content + $sec39.Trim() + "`n"

[System.IO.File]::WriteAllText($targetPath, $finalContent, [System.Text.Encoding]::UTF8)
Write-Output "Fixed and appended cleanly."
