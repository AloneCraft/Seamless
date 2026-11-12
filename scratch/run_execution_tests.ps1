$runId = [guid]::NewGuid().ToString()
$artifactDir = "F:\Seamless\Artifacts\ExecutionTests_$runId"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$res = & "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1" `
    -Executable "C:\Program Files\dotnet\dotnet.exe" `
    -Arguments @(
        "test",
        "G:\GitHub\Seamless\Execution.Tests\Execution.Tests.csproj",
        "-c", "Debug",
        "-v", "normal"
    ) `
    -WorkingDirectory "G:\GitHub\Seamless" `
    -ArtifactDirectory $artifactDir

Write-Host "Outcome: $($res.Outcome)"
Write-Host "Error: $($res.Error)"

Get-ChildItem -Path $artifactDir -File | ForEach-Object {
    Write-Host "=== File: $($_.Name) ==="
    Get-Content $_.FullName
}
