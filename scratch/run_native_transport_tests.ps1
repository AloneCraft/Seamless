$env:SEAMLESS_TEST_DATA_ROOT = "F:\Seamless\TestData"
& "C:\Program Files\dotnet\dotnet.exe" test "G:\GitHub\Seamless\Tests\Management.Tests\bin\Debug\net10.0\Management.Tests.dll" --filter "FullyQualifiedName~NativeRecordTransport" --no-build
