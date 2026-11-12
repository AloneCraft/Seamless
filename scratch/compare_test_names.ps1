$baseline = Get-Content 'G:\GitHub\Seamless\scratch\baseline_test_names.txt' | Sort-Object
$migrated = Get-Content 'G:\GitHub\Seamless\scratch\migrated_test_names.txt' | Sort-Object

Write-Host "Baseline Count: $($baseline.Count)"
Write-Host "Migrated Count: $($migrated.Count)"

$diff = Compare-Object $baseline $migrated
if ($diff) {
    Write-Host "Differences found:"
    $diff | Format-Table -AutoSize
} else {
    Write-Host "100% EXACT MATCH! Zero dropped, zero extra."
}
