$data = 'F:\Seamless\TestData\native-auto-build-db99f7465698402eacdcc6700028e53a'
$genFiles = Get-ChildItem $data -Recurse -Filter '*.g.cs'
Write-Host "Found files: $($genFiles.Count)"
foreach ($f in $genFiles) {
    $content = Get-Content $f.FullName -Raw
    if ($content.Contains('provider=DatabaseRows')) {
        Write-Host "File: $($f.FullName)"
        Write-Host "Length: $($content.Length)"
        # Write first 50 lines or matching lines
        $content -split "`r?`n" | Where-Object { $_ -match 'OrderBy|ThenBy|Sort|QueryOrdering' } | ForEach-Object { Write-Host $_ }
    }
}
