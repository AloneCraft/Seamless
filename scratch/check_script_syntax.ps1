$errs = $null
$tokens = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile('G:\GitHub\Seamless\Update-FocusedSolutions.ps1', [ref]$tokens, [ref]$errs)
if ($errs) {
    foreach ($e in $errs) {
        Write-Host "Line $($e.Extent.StartLineNumber): $($e.Message)"
    }
} else {
    Write-Host "No syntax errors found!"
}
