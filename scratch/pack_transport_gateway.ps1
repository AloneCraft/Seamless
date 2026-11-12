$ErrorActionPreference = 'Stop'
$projects = @(
    "Seamless.Database.Client\Seamless.Database.Client.csproj",
    "Seamless.Transport.Kestrel\Seamless.Transport.Kestrel.csproj",
    "Seamless.Database.Gateway\Seamless.Database.Gateway.csproj"
)

$runId = [guid]::NewGuid().ToString()
$artifactBase = "F:\Seamless\Artifacts\Pack_$runId"

foreach ($proj in $projects) {
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    Write-Host "Packing $projName..."
    $artifactDir = Join-Path $artifactBase $projName
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

    $res = & "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1" -Executable "C:\Program Files\dotnet\dotnet.exe" -Arguments @("pack", "G:\GitHub\Seamless\$proj", "-c", "Release", "-p:Version=0.0.0", "-p:PackageOutputPath=G:\GitHub\Seamless\Work\VirtualDbFeed") -WorkingDirectory "G:\GitHub\Seamless" -ArtifactDirectory $artifactDir

    Write-Host "Result for $projName : $($res.Outcome) (exit $($res.Task.exitCode))"
    if ($res.Outcome -ne 'Passed') {
        $logFile = Join-Path $artifactDir "stdout.log"
        if (Test-Path $logFile) { Get-Content $logFile -Tail 20 }
        $errFile = Join-Path $artifactDir "stderr.log"
        if (Test-Path $errFile) { Get-Content $errFile -Tail 20 }
        throw "Failed to pack $projName"
    }
}

Write-Host "Transport and Gateway packages packed successfully."
