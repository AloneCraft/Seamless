param(
    [string]$Project = "G:\GitHub\Seamless\MultiProjectTemplate\Database\MultiProjectTemplate.Database.csproj",
    [string]$Configuration = "Debug"
)

$runId = [guid]::NewGuid().ToString()
$artifactDir = "F:\Seamless\Artifacts\Build_$runId"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$res = & "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1" `
    -Executable "C:\Program Files\dotnet\dotnet.exe" `
    -Arguments @("build", $Project, "-c", $Configuration) `
    -WorkingDirectory "G:\GitHub\Seamless" `
    -ArtifactDirectory $artifactDir

Write-Host "Outcome: $($res.Outcome)"
Write-Host "Error: $($res.Error)"

Get-ChildItem -Path $artifactDir -File | ForEach-Object {
    Write-Host "=== File: $($_.Name) ==="
    Get-Content $_.FullName
}

Remove-Item -Path $artifactDir -Recurse -Force
