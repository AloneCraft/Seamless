Set-ExecutionPolicy -Scope Process Bypass -Force
$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"
$env:SEAMLESS_TEST_CACHE_ROOT = "F:\Seamless\TestCache"
$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY = "G:\GitHub\Seamless\Seamless.Database.Generator\bin\Debug\net10.0\Seamless.Database.Generator.dll"
$latestGenerator = Get-ChildItem "C:\Users\kazuh\.nuget\packages\memorypack.generator" -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY = Join-Path $latestGenerator.FullName "analyzers\dotnet\cs\MemoryPack.Generator.dll"
& .\TestSeamless\Invoke-LocalProcess.ps1 -InitializeOnly | Out-Null
& "C:\Program Files\dotnet\dotnet.exe" test Tests/Management.Tests/Management.Tests.csproj --filter "FullyQualifiedName~NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows" --logger "console;verbosity=normal"
