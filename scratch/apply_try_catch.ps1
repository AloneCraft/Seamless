$content = [System.IO.File]::ReadAllText('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', [System.Text.Encoding]::UTF8)
$target = '        $source=[IO.Path]::GetFullPath((Join-Path $dir $raw))'
$replacement = @'
        try {
            $source=[IO.Path]::GetFullPath((Join-Path $dir $raw))
        } catch { continue }
'@
$newContent = $content.Replace($target, $replacement)
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', $newContent, $utf8Bom)
Write-Host "Replaced with try-catch."
