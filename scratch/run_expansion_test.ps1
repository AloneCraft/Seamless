Set-ExecutionPolicy -Scope Process Bypass -Force
$env:PATH = "C:\Program Files\dotnet;" + $env:PATH
$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"
$env:SEAMLESS_TEST_CACHE_ROOT = "F:\Seamless\TestCache"
$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY = "G:\GitHub\Seamless\Seamless.Database.Generator\bin\Debug\net10.0\Seamless.Database.Generator.dll"
$env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY = "C:\Users\kazuh\.nuget\packages\memorypack.generator\1.21.4\analyzers\dotnet\cs\MemoryPack.Generator.dll"
& .\TestSeamless\Invoke-LocalProcess.ps1 -InitializeOnly | Out-Null
& "C:\Program Files\dotnet\dotnet.exe" test Tests/Seamless.Database.Gateway.Tests/Seamless.Database.Gateway.Tests.csproj --filter "FullyQualifiedName~NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows" --logger "console;verbosity=normal"
