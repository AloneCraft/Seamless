$targets = @(
    'Test.Persisted.Linq.Enumerable.FLinq',
    'Test.Persisted.Linq.Enumerable.IEnumerable.FLinq',
    'Test.System.Linq.Enumerable.FLinq',
    'Test.Tsavorite.Linq.Enumerable.FLinq',
    'Test.Tsavorite.Linq.Enumerable.IEnumerable.FLinq'
)

$baseDir = (Get-Item .).FullName
foreach ($t in $targets) {
    $full = Join-Path $baseDir $t
    if ([System.IO.Directory]::Exists($full)) {
        [System.IO.Directory]::Delete("\\?\$full", $true)
        Write-Host "Deleted with long path prefix: $t"
    }
}
