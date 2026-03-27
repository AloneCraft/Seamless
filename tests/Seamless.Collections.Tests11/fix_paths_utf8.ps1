$ErrorActionPreference = "Stop"

$files = Get-ChildItem -Path "." -Recurse -Filter "*.cs"

foreach ($file in $files) {
    if ($file.Name -match 'TestTsavorite.cs') { continue }
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $original = $content
    
    # 1. LogDevice with Path.Combine
    $content = [regex]::Replace($content, '(?m)^(\s*)(using )?var LogDevice\s*=\s*new (Managed)?LocalStorageDevice\(Path\.Combine\((folder|Folder)\s*,\s*"log\.data"\)', '${1}Directory.CreateDirectory($4);`r`n${1}${2}var LogDevice = new $3LocalStorageDevice(Path.Combine($4, "log.data")')

    # 2. ObjectLogDevice with Path.Combine
    $content = [regex]::Replace($content, '(?m)^(\s*)(using )?var ObjectLogDevice\s*=\s*new (Managed)?LocalStorageDevice\(Path\.Combine\((folder|Folder)\s*\+\s*"_obj"\s*,\s*"log\.data"\)', '${1}Directory.CreateDirectory($4 + "_obj");`r`n${1}${2}var ObjectLogDevice = new $3LocalStorageDevice(Path.Combine($4 + "_obj", "log.data")')

    # 3. LogDevice without Path.Combine
    $content = [regex]::Replace($content, '(?m)^(\s*)(using )?var LogDevice\s*=\s*new (Managed)?LocalStorageDevice\((folder|Folder)\)', '${1}Directory.CreateDirectory($4);`r`n${1}${2}var LogDevice = new $3LocalStorageDevice($4)')

    # 4. ObjectLogDevice without Path.Combine
    $content = [regex]::Replace($content, '(?m)^(\s*)(using )?var ObjectLogDevice\s*=\s*new (Managed)?LocalStorageDevice\((folder|Folder)\s*\+\s*"_obj"\)', '${1}Directory.CreateDirectory($4 + "_obj");`r`n${1}${2}var ObjectLogDevice = new $3LocalStorageDevice($4 + "_obj")')

    if ($content -cne $original) {
        Write-Host "Updated $($file.FullName)"
        $utf8bom = New-Object System.Text.UTF8Encoding $true
        [System.IO.File]::WriteAllText($file.FullName, $content, $utf8bom)
    }
}
Write-Host "Done"
