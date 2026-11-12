$json = Get-Content 'Benchmark\Benchmark.Dynamic.Core\FixedCases\chains.json' -Raw | ConvertFrom-Json
$c21 = $json | Where-Object { $_.ID -eq 'chain-21' }
Write-Host "chain-21 Expression: $($c21.Expression)"
Write-Host "chain-21 IdealBody:"
Write-Host $c21.IdealBody
