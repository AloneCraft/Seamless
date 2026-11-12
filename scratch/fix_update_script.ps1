$content = Get-Content 'Update-FocusedSolutions.ps1' -Raw
$newContent = $content -replace '(\$source=\[IO\.Path\]::GetFullPath)', 'try { $source=[IO.Path]::GetFullPath((Join-Path $dir $raw)) } catch { Write-Host "Invalid raw in $relPath : ''$raw''"; continue }; continue # $1'
# 実際には try-catch で囲む
