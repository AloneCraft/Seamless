$dir = '\\?\F:\Seamless\TestCache\native-auto-build-db99f7465698402eacdcc6700028e53a\generated\0149c384e33f45b2b6e16adfcce4d074\generated\Seamless\FLinq\Generator\Seamless.FLinq.Generator.FLinqIncrementalGenerator'
$files = [System.IO.Directory]::GetFiles($dir, "*Query_cs.g.cs")
Write-Host "Files count: $($files.Length)"
foreach ($path in $files) {
    Write-Host "File: $path"
    $content = [System.IO.File]::ReadAllText($path)
    Write-Host "Length: $($content.Length)"
    $content -split "`r?`n" | Where-Object { $_ -match 'OrderBy|ThenBy|Sort|QueryOrdering' } | ForEach-Object { Write-Host $_ }
}
