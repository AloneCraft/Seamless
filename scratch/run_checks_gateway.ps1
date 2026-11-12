Set-ExecutionPolicy -Scope Process Bypass -Force
$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
try {
    & .\TestSeamless\Invoke-LocalProcess.ps1 -InitializeOnly | Out-Null
    & .\tools\management\Invoke-Checks.ps1 -Suite Gateway -Configuration Debug -PackageFallbackRoot "C:\Users\kazuh\.nuget\packages"
} catch {
    Write-Host "ERROR: $_"
    Write-Host $_.ScriptStackTrace
}
