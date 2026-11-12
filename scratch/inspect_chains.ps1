$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
foreach ($c in $json) {
    Write-Host "[$($c.ID)] Methods: $($c.Methods -join ',')"
    Write-Host "  Expression: $($c.Expression)"
    if ($c.IdealBody) {
        Write-Host "  IdealBody: $($c.IdealBody.Replace("`n", ' '))"
    }
}
