$lines = Get-Content 'Update-FocusedSolutions.ps1'
for ($i = 90; $i -le 96; $i++) {
    Write-Host "Line $i : $($lines[$i-1])"
}
