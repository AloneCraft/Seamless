$content = [System.IO.File]::ReadAllText('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', [System.Text.Encoding]::UTF8)
$target = '                    $pattern=[IO.Path]::GetFullPath((Join-Path $owner.FullName $pattern))'
$replacement = @'
                    try {
                        $pattern=[IO.Path]::GetFullPath((Join-Path $owner.FullName $pattern))
                    } catch { continue }
'@
$newContent = $content.Replace($target, $replacement)
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', $newContent, $utf8Bom)
Write-Host "Replaced pattern GetFullPath with try-catch."
