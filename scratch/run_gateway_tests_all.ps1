$testDir = "F:\Seamless\TestArtifacts\GatewayTestsAll_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
New-Item -ItemType Directory -Path "F:\Seamless\TestData" -Force | Out-Null

$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"

$dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$workDir = (Get-Item .).FullName
$testDll = "$workDir\Tests\Management.Tests\bin\Debug\net10.0\Management.Tests.dll"

& .\TestSeamless\Invoke-LocalProcess.ps1 -Executable $dotnetPath -Arguments @("test", $testDll, "--no-build") -WorkingDirectory $workDir -ArtifactDirectory $testDir
