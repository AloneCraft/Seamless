$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"
& "C:\Program Files\dotnet\dotnet.exe" test "G:\GitHub\Seamless\Tests\Seamless.Database.Gateway.Tests\bin\Debug\net10.0\Seamless.Database.Gateway.Tests.dll" --filter "FullyQualifiedName~NativeRecordTransport" --no-build
