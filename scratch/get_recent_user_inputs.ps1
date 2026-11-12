$lines = Get-Content 'C:\Users\kazuh\.gemini\antigravity\brain\9c8a2afc-0a30-425b-9efc-243ca6e935b0\.system_generated\logs\transcript.jsonl' -Tail 40
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $obj = $line | ConvertFrom-Json
    if ($obj.type -eq 'USER_INPUT' -or $obj.source -eq 'USER_EXPLICIT') {
        Write-Host "=== Step $($obj.step_index) [$($obj.type)] ==="
        Write-Host $obj.content
    }
}
