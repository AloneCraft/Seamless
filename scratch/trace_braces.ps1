$lines = Get-Content 'Update-FocusedSolutions.ps1'
$depth = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $opens = ([regex]::Matches($line, '\{')).Count
    $closes = ([regex]::Matches($line, '\}')).Count
    $depth += ($opens - $closes)
    if ($opens -ne 0 -or $closes -ne 0) {
        Write-Host "Line $($i+1) [Depth $depth] (+$opens, -$closes): $line"
    }
}
