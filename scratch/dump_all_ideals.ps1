$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
foreach ($c in $json) {
    Write-Output "========================================"
    Write-Output ("ID: " + $c.ID + " | Expr: " + $c.Expression)
    Write-Output $c.IdealBody
}
