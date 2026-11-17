$ErrorActionPreference = "Stop"
$artifactDir = "F:\Seamless\Artifacts\GenTest_" + [guid]::NewGuid().ToString("N")
if (!(Test-Path $artifactDir)) { New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null }

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$workingDir = "G:\GitHub\Seamless"
$invokeScript = "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1"

& $invokeScript -Executable $dotnet `
    -Arguments @("test", "G:\GitHub\Seamless\Tests\Database.Query.Tests\Database.Query.Tests.csproj", "--filter", "FullyQualifiedName~DatabaseContainerGeneratorTests", "--logger", "console;verbosity=normal") `
    -WorkingDirectory $workingDir `
    -ArtifactDirectory $artifactDir
