$p = Start-Process -FilePath "C:\Program Files\dotnet\dotnet.exe" -ArgumentList "`"G:\GitHub\Seamless\MultiProjectTemplate\Server\bin\Debug\net10.0\MultiProjectTemplate.Server.dll`" --port 0" -NoNewWindow -PassThru -RedirectStandardOutput "F:\Seamless\Artifacts\server_test_out.log" -RedirectStandardError "F:\Seamless\Artifacts\server_test_err.log"
Start-Sleep -Seconds 3
if (Test-Path "F:\Seamless\Artifacts\server_test_out.log") {
    Write-Host "=== Output ==="
    Get-Content "F:\Seamless\Artifacts\server_test_out.log"
}
if (Test-Path "F:\Seamless\Artifacts\server_test_err.log") {
    Write-Host "=== Error ==="
    Get-Content "F:\Seamless\Artifacts\server_test_err.log"
}
if (-not $p.HasExited) {
    $p.Kill()
}
