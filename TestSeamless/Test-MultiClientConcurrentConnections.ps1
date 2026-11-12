# Multi-client concurrent connection verification script
param(
    [int]$ClientCount = 4,
    [int]$Port = 51240,
    [string]$OutputDir = "F:\Seamless\multiclient-verification"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$serverDll = (Resolve-Path "MultiProjectTemplate\Server\bin\Debug\net10.0\MultiProjectTemplate.Server.dll").Path
$clientDll = (Resolve-Path "MultiProjectTemplate\Client\bin\Debug\net10.0\MultiProjectTemplate.Client.dll").Path

$serverLog = Join-Path $OutputDir "server.log"
$serverErr = Join-Path $OutputDir "server-err.log"
if (Test-Path $serverLog) { Remove-Item $serverLog -Force }
if (Test-Path $serverErr) { Remove-Item $serverErr -Force }

Write-Host "Starting Server on port $Port..."
$serverPsi = New-Object System.Diagnostics.ProcessStartInfo
$serverPsi.FileName = $dotnet
$serverPsi.Arguments = "`"$serverDll`" --port $Port"
$serverPsi.RedirectStandardOutput = $true
$serverPsi.RedirectStandardError = $true
$serverPsi.RedirectStandardInput = $true
$serverPsi.UseShellExecute = $false
$serverPsi.CreateNoWindow = $true

$serverProc = [System.Diagnostics.Process]::Start($serverPsi)

$serverOutWriter = [System.IO.StreamWriter]::new($serverLog)
$serverErrWriter = [System.IO.StreamWriter]::new($serverErr)

$ready = $false
$sw = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # Wait for READY
    while (-not $ready -and $sw.ElapsedMilliseconds -lt 15000) {
        if ($serverProc.HasExited) {
            throw "Server exited unexpectedly with code $($serverProc.ExitCode)"
        }
        $line = $serverProc.StandardOutput.ReadLine()
        if ($null -ne $line) {
            $serverOutWriter.WriteLine($line)
            $serverOutWriter.Flush()
            if ($line -match "READY") {
                $ready = $true
            }
        } else {
            Start-Sleep -Milliseconds 100
        }
    }

    if (-not $ready) {
        throw "Server did not output READY within 15 seconds."
    }

    Write-Host "Server is READY. Spawning $ClientCount concurrent clients..."

    $clientTasks = @()
    for ($i = 0; $i -lt $ClientCount; $i++) {
        $cIndex = $i
        $clientLog = Join-Path $OutputDir "client_$cIndex.log"
        $clientErr = Join-Path $OutputDir "client_$cIndex.err"
        if (Test-Path $clientLog) { Remove-Item $clientLog -Force }
        if (Test-Path $clientErr) { Remove-Item $clientErr -Force }

        $clientPsi = New-Object System.Diagnostics.ProcessStartInfo
        $clientPsi.FileName = $dotnet
        $clientPsi.Arguments = "`"$clientDll`" --port $Port"
        $clientPsi.RedirectStandardOutput = $true
        $clientPsi.RedirectStandardError = $true
        $clientPsi.UseShellExecute = $false
        $clientPsi.CreateNoWindow = $true

        $clientTasks += [pscustomobject]@{
            Index = $cIndex
            Psi = $clientPsi
            Log = $clientLog
            Err = $clientErr
        }
    }

    $runningClients = @()
    foreach ($c in $clientTasks) {
        $proc = [System.Diagnostics.Process]::Start($c.Psi)
        $runningClients += [pscustomobject]@{
            Index = $c.Index
            Process = $proc
            Log = $c.Log
            Err = $c.Err
        }
    }

    Write-Host "All $ClientCount clients started. Waiting for completion..."

    $clientResults = @()
    foreach ($rc in $runningClients) {
        $stdOut = $rc.Process.StandardOutput.ReadToEnd()
        $stdErr = $rc.Process.StandardError.ReadToEnd()
        $rc.Process.WaitForExit(30000)

        [System.IO.File]::WriteAllText($rc.Log, $stdOut)
        [System.IO.File]::WriteAllText($rc.Err, $stdErr)

        $success = ($rc.Process.ExitCode -eq 0) -and ($stdOut -match "CLIENT_SUCCESS")
        $clientResults += [pscustomobject]@{
            Index = $rc.Index
            ExitCode = $rc.Process.ExitCode
            Success = $success
        }
        Write-Host "Client $($rc.Index): ExitCode=$($rc.Process.ExitCode), Success=$success"
    }

    $failedClients = $clientResults | Where-Object { -not $_.Success }
    if ($failedClients.Count -gt 0) {
        throw "$($failedClients.Count) out of $ClientCount clients failed."
    }

    Write-Host "SUCCESS: All $ClientCount clients completed successfully concurrently!"
}
finally {
    if ($null -ne $serverProc -and -not $serverProc.HasExited) {
        Write-Host "Stopping server..."
        try {
            $serverProc.StandardInput.WriteLine()
            $serverProc.StandardInput.Flush()
            if (-not $serverProc.WaitForExit(5000)) {
                $serverProc.Kill()
            }
        } catch {
            $serverProc.Kill()
        }
    }
    $serverOutWriter.Dispose()
    $serverErrWriter.Dispose()
}
