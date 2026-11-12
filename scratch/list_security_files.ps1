$files = Get-ChildItem -Path @('Seamless.Security', 'Seamless.Authentication') -Recurse -File -Filter '*.cs'
foreach ($f in $files) {
    if ($f.FullName -match '\\(obj|bin)\\\\') { continue }
    Write-Host $f.FullName
}
