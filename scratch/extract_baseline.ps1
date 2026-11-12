$logPath = 'C:\Users\kazuh\.gemini\antigravity\brain\9c8a2afc-0a30-425b-9efc-243ca6e935b0\.system_generated\tasks\task-27826.log'
$lines = Get-Content $logPath
$cases = @()
foreach ($l in $lines) {
    if ($l -match 'Execution\.Tests\.') {
        $cases += $l.Trim()
    }
}
$cases | Out-File 'G:\GitHub\Seamless\scratch\baseline_execution_tests.txt'
Write-Host "Extracted $($cases.Count) cases"
