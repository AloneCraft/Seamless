$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
foreach ($c in $json) {
    $hasIdeal = if ($c.IdealBody) { "YES" } else { "NO" }
    Write-Host "$($c.ID): Ideal=$hasIdeal, Methods=$($c.Methods -join '->')"
    Write-Host "   Expr: $($c.Expression)"
}
