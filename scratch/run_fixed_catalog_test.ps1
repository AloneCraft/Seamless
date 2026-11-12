$testDir = "F:\Seamless\TestArtifacts\FixedCaseCatalog_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

$dotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$workDir = (Get-Item .).FullName
$testDll = "$workDir\Benchmark.Dynamic.Tests\bin\Debug\net10.0-windows\Benchmark.Dynamic.Tests.dll"

& .\TestSeamless\Invoke-LocalProcess.ps1 -Executable $dotnetPath -Arguments @("test", $testDll, "--filter", "FullyQualifiedName~FixedCaseCatalog", "--no-build") -WorkingDirectory $workDir -ArtifactDirectory $testDir
