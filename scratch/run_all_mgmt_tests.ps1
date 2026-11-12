$ErrorActionPreference = "Stop"
$env:SEAMLESS_MANAGEMENT_TEST_ROOT = "F:\Seamless\TestRoots"
$env:NUGET_HTTP_CACHE_PATH = "F:\Seamless\Cache"
$artifactDir = "F:\Seamless\Artifacts\MgmtTest_" + [guid]::NewGuid().ToString("N")

if (!(Test-Path "F:\Seamless\TestRoots")) { New-Item -ItemType Directory -Path "F:\Seamless\TestRoots" -Force | Out-Null }
if (!(Test-Path "F:\Seamless\Cache")) { New-Item -ItemType Directory -Path "F:\Seamless\Cache" -Force | Out-Null }
if (!(Test-Path $artifactDir)) { New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null }

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$workingDir = "G:\GitHub\Seamless"
$invokeScript = "G:\GitHub\Seamless\TestSeamless\Invoke-LocalProcess.ps1"

& $invokeScript -Executable $dotnet `
    -Arguments @("test", "G:\GitHub\Seamless\Tests\Management.Tests\Management.Tests.csproj", "-c", "Release", "--filter", "DisplayName~WebView|DisplayName~JS", "--logger", "console;verbosity=normal") `
    -WorkingDirectory $workingDir `
    -ArtifactDirectory $artifactDir
