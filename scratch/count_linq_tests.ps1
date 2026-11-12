Get-ChildItem -Directory -Filter "Test.*.Linq.*" | ForEach-Object {
    $p = $_.FullName
    $files = (Get-ChildItem -Path $p -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "obj|bin" }).Count
    [pscustomobject]@{ Project = $_.Name; CsFileCount = $files }
}
