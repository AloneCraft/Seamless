Get-ChildItem -Path "Execution.Tests" -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch 'obj|bin' } | ForEach-Object {
    $_.FullName
}
