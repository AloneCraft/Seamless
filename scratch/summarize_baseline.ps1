$lines = Get-Content 'G:\GitHub\Seamless\scratch\baseline_execution_tests.txt'
$failed = @()
$passed = @()
foreach ($l in $lines) {
    if ($l -match 'Execution\.Tests\.([^\s\(\]]+)') {
        $name = $matches[1]
        if ($l -match '\[FAIL\]') {
            $failed += $name
        } elseif ($l -match '謌仙粥' -or $l -match 'Passed') {
            $passed += $name
        }
    }
}
Write-Host "Unique Failed: $(($failed | Select-Object -Unique).Count), Total Failed Instances: $($failed.Count)"
Write-Host "Unique Passed: $(($passed | Select-Object -Unique).Count), Total Passed Instances: $($passed.Count)"
