$bytes = [System.IO.File]::ReadAllBytes('G:\GitHub\Seamless\Update-FocusedSolutions.ps1')
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
# 改行を `r`n に正規化して UTF8 BOM付きで書き直す
$text = $text -replace "`r?`n", "`r`n"
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', $text, $utf8Bom)
Write-Host "Normalized encoding and line endings."
