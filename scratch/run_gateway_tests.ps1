$runId = [guid]::NewGuid().ToString()
$artifactDir = "F:\Seamless\Artifacts\Test_$runId"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$testDataRoot = "F:\Seamless\TestData"
New-Item -ItemType Directory -Path $testDataRoot -Force | Out-Null
$env:SEAMLESS_TEST_DATA_ROOT = $testDataRoot

$res = & "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1" `
    -Executable "C:\Program Files\dotnet\dotnet.exe" `
    -Arguments @(
        "test",
        "G:\GitHub\Seamless\Tests\Management.Tests\Management.Tests.csproj",
        "-c", "Debug",
        "--filter", "FullyQualifiedName~NativeRecordTransportTests|FullyQualifiedName~MultiProjectTemplateE2ETests"
    ) `
    -WorkingDirectory "G:\GitHub\Seamless" `
    -ArtifactDirectory $artifactDir

Write-Host "Outcome: $($res.Outcome)"
Write-Host "Error: $($res.Error)"

Get-ChildItem -Path $artifactDir -File | ForEach-Object {
    Write-Host "=== File: $($_.Name) ==="
    Get-Content $_.FullName
}
