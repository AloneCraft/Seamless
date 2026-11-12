$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
foreach ($c in $json) {
    if ($c.ID -match '^(chain-02|chain-04|chain-16|group-count-sum)$') {
        Write-Output ("ID: " + $c.ID)
        Write-Output ("Expr: " + $c.Expression)
        Write-Output ("IdealBody:`n" + $c.IdealBody)
        Write-Output "------------------------"
    }
}
