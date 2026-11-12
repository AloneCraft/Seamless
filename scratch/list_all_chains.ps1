$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
foreach ($c in $json) {
    $hasIdeal = if ([string]::IsNullOrEmpty($c.IdealBody)) { "NO_IDEAL" } else { "HAS_IDEAL" }
    Write-Output ("{0,-30} | {1} | {2}" -f $c.ID, $hasIdeal, $c.Expression)
}
