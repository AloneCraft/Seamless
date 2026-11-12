$lines = Get-Content 'F:\Seamless\test-artifacts-bench-fixed-all-33\stdout.log' | ConvertFrom-Json
$grouped = $lines | Group-Object WorkloadID
$results = foreach ($g in $grouped) {
    $sys = $g.Group | Where-Object Implementation -eq 'System'
    $zlinq = $g.Group | Where-Object Implementation -eq 'ZLinq'
    $ideal = $g.Group | Where-Object Implementation -eq 'Ideal'
    $flinq = $g.Group | Where-Object Implementation -eq 'FLinq'
    
    $sysNs = if ($sys) { [math]::Round($sys.MedianNanoseconds, 1) } else { 0 }
    $flinqNs = if ($flinq) { [math]::Round($flinq.MedianNanoseconds, 1) } else { 0 }
    $idealNs = if ($ideal) { [math]::Round($ideal.MedianNanoseconds, 1) } else { 0 }
    $zlinqNs = if ($zlinq) { [math]::Round($zlinq.MedianNanoseconds, 1) } else { 0 }
    $speedup = if ($sysNs -gt 0 -and $flinqNs -gt 0) { [math]::Round($sysNs / $flinqNs, 2) } else { 0 }
    $win = if ($sysNs -gt $flinqNs) { 'WIN' } else { 'LOSE' }

    [pscustomobject]@{
        Case = $g.Name
        SystemNs = $sysNs
        ZLinqNs = $zlinqNs
        IdealNs = $idealNs
        FLinqNs = $flinqNs
        Speedup = "$speedup x"
        Result = $win
    }
}
$results | Format-Table -AutoSize
