$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"
$runId = [guid]::NewGuid().ToString()
$artifactDir = "F:\Seamless\Artifacts\Test_$runId"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$res = & "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1" `
    -Executable "C:\Program Files\dotnet\dotnet.exe" `
    -Arguments @(
        "test",
        "G:\GitHub\Seamless\Tests\Seamless.Database.Gateway.Tests\Seamless.Database.Gateway.Tests.csproj",
        "-c", "Debug",
        "--filter", "FullyQualifiedName~MultiProjectTemplateE2ETests"
    ) `
    -WorkingDirectory "G:\GitHub\Seamless" `
    -ArtifactDirectory $artifactDir

Write-Host "Outcome: $($res.Outcome)"
Write-Host "Error: $($res.Error)"

Get-ChildItem -Path $artifactDir -File | ForEach-Object {
    Write-Host "=== File: $($_.Name) ==="
    Get-Content $_.FullName
}
