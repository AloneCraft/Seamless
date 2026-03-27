$files = Get-ChildItem -Path '.' -Filter '*.cs' -Recurse
foreach ($file in $files) {
    if ($file.Name -eq 'CoverageDictionary.cs' -or $file.Name -eq 'TestTsavorite.cs') { continue }
    $content = Get-Content -Raw $file.FullName
    
    # We use a MatchEvaluator-like approach or just regex replace
    $pattern = '(var\s+([fF]older)\s*=\s*Path\.Combine\(Path\.GetTempPath\(\)\s*,\s*"SeamlessTest_"\s*\+\s*Guid\.NewGuid\(\)\.ToString\("N"\)\);)'
    
    # The replacement string in .NET Regex uses $1, $2 etc. 
    # In PowerShell single quotes, $1 is passed literally to the .NET Regex engine
    $replacement = '$1' + "`r`n        System.IO.Directory.CreateDirectory(`$2);" + "`r`n        System.IO.Directory.CreateDirectory(`$2 + `"_obj`");"
    
    $newContent = [regex]::Replace($content, $pattern, $replacement)
    
    if ($content -ne $newContent) {
        [System.IO.File]::WriteAllText($file.FullName, $newContent)
        Write-Host "Updated $($file.Name)"
    }
}
