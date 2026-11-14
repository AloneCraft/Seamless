[CmdletBinding()]
param(
    [string]$Executable,
    [string[]]$Arguments = @(),
    [string]$WorkingDirectory,
    [string]$ArtifactDirectory,
    [ValidateRange(1, 2147483)][int]$TimeoutSeconds = 7200,
    [ValidateRange(1, 8)][int]$MaxLogicalProcessors = 8,
    [switch]$AllowCancellation,
    [switch]$InitializeOnly
)

$ErrorActionPreference = 'Stop'
$current = [Diagnostics.Process]::GetCurrentProcess()
$oldAffinity = $null
$oldCount = [Environment]::GetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', 'Process')
$oldTelemetry = [Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', 'Process')
$initialized = $false
$keepInitialization = $false
$process = $null
$outStream = $null
$errStream = $null
$outCopy = $null
$errCopy = $null
$artifactSafe = $false
$artifactClaim = $null
$stopping = $false
$result = [ordered]@{
    Outcome = 'Unavailable'; Error = $null
    Task = [ordered]@{ id = [guid]::NewGuid().ToString(); status = 'Unavailable'; exitCode = $null; artifactDirectory = $ArtifactDirectory; pid = $null; creationUtc = $null; affinity = $null }
}
try {
    $isWin = if ($null -ne (Get-Variable -Name IsWindows -ErrorAction SilentlyContinue)) { $IsWindows } else { [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT }
    if (-not $isWin -or [IntPtr]::Size -ne 8) { throw 'Unavailable: 64-bit Windows is required.' }
    $machineCount = @(Get-ChildItem -LiteralPath 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor' -ErrorAction Stop).Count
    if ($machineCount -eq 0) { throw 'Unavailable: logical processor count could not be determined.' }
    if ($machineCount -gt 64) { throw 'Unavailable: systems with more than 64 logical processors are not supported.' }
    $oldAffinity = $current.ProcessorAffinity
    $bits = [BitConverter]::ToUInt64([BitConverter]::GetBytes($oldAffinity.ToInt64()), 0)
    [uint64]$selected = 0
    $count = 0
    for ($i = 0; $i -lt 64 -and $count -lt $MaxLogicalProcessors; $i++) {
        [uint64]$bit = [uint64]1 -shl $i
        if (($bits -band $bit) -ne 0) { $selected = $selected -bor $bit; $count++ }
    }
    if ($count -eq 0) { throw 'Unavailable: the current process has no accessible affinity bits.' }
    $mask = [IntPtr]::new([BitConverter]::ToInt64([BitConverter]::GetBytes($selected), 0))
    $current.ProcessorAffinity = $mask
    $initialized = $true
    [Environment]::SetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', [string]$count, 'Process')
    [Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', '1', 'Process')
    if ($current.ProcessorAffinity -ne $mask) { throw 'Unavailable: affinity could not be verified.' }
    $result.Task.affinity = '0x{0:X}' -f $selected
    if ($InitializeOnly) {
        $keepInitialization = $true
        [pscustomobject]@{ OriginalAffinityMask = $oldAffinity; AffinityMask = $mask; EffectiveLogicalProcessors = $count }
        return
    }
    $isAbs = { param($p) [string]::IsNullOrEmpty($p) -eq $false -and [IO.Path]::IsPathRooted($p) -and ([IO.Path]::GetPathRoot($p) -match '^[a-zA-Z]:[\\/]') }
    if (-not (& $isAbs $Executable) -or -not (& $isAbs $WorkingDirectory) -or -not (& $isAbs $ArtifactDirectory)) {
        throw 'Executable, WorkingDirectory and ArtifactDirectory must be absolute paths.'
    }
    if ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ArtifactDirectory)) -eq 'C:\' -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($WorkingDirectory)) -eq 'C:\') { throw 'Comparison work and artifacts must not use the C drive.' }
    [IO.Directory]::CreateDirectory($ArtifactDirectory) | Out-Null
    # A directory belongs to one attempt, even if its process or terminal receipt is missing.
    foreach ($name in 'process-start.json','receipt.json','stdout.log','stderr.log') {
        if ([IO.File]::Exists((Join-Path $ArtifactDirectory $name))) {
            throw 'The artifact directory already contains a process attempt.'
        }
    }
    $artifactClaim = [IO.FileStream]::new((Join-Path $ArtifactDirectory 'process-attempt.lock'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $artifactSafe = $true
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    if ([Environment]::GetEnvironmentVariable('DOTNET_CLI_HOME', 'Process') -and -not [Environment]::GetEnvironmentVariable('DOTNET_ADD_GLOBAL_TOOLS_TO_PATH', 'Process')) {
        $start.Environment['DOTNET_ADD_GLOBAL_TOOLS_TO_PATH'] = 'false'
    }
    if ($null -ne $start.PSObject.Properties['ArgumentList'] -and $null -ne $start.ArgumentList) {
        foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    } else {
        $escaped = foreach ($a in $Arguments) {
            if ($a -match '[\s"]') { '"' + ($a -replace '\\(?="|$)', '\\$0' -replace '"', '\"') + '"' } else { $a }
        }
        $start.Arguments = $escaped -join ' '
    }
    $outStream = [IO.FileStream]::new((Join-Path $ArtifactDirectory 'stdout.log'), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $errStream = [IO.FileStream]::new((Join-Path $ArtifactDirectory 'stderr.log'), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Process did not start.' }
    $result.Task.pid = $process.Id
    $result.Task.creationUtc = $process.StartTime.ToUniversalTime().ToString('o')
    $running = [ordered]@{
        Task = [ordered]@{ id = $result.Task.id; status = 'Started'; pid = $result.Task.pid; creationUtc = $result.Task.creationUtc; affinity = $result.Task.affinity }
        Runner = [ordered]@{ pid = $current.Id; creationUtc = $current.StartTime.ToUniversalTime().ToString('o') }
    }
    if ($AllowCancellation) {
        $running.Task.cancellationRequestFile = "cancel-$($result.Task.id).request"
        $cancelPath = Join-Path $ArtifactDirectory $running.Task.cancellationRequestFile
    }
    $startRecord = Join-Path $ArtifactDirectory 'process-start.json'
    [IO.File]::WriteAllText("$startRecord.tmp", ($running | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move("$startRecord.tmp", $startRecord)
    $outCopy = $process.StandardOutput.BaseStream.CopyToAsync($outStream)
    $errCopy = $process.StandardError.BaseStream.CopyToAsync($errStream)
    $timeout = $TimeoutSeconds * 1000
    $stopOutcome = 'Timeout'
    if ($AllowCancellation) {
        $elapsed = [Diagnostics.Stopwatch]::StartNew()
        do {
            $remaining = [Math]::Max(0, $timeout - $elapsed.ElapsedMilliseconds)
            $finished = $process.WaitForExit([int][Math]::Min($remaining, 100))
            if ($finished -or $elapsed.ElapsedMilliseconds -ge $timeout) { break }
            if ([IO.File]::Exists($cancelPath)) {
                $finished = $process.WaitForExit(0)
                if (-not $finished) {
                    $stopOutcome = 'Cancelled'
                    $result.Task.cancellationRequestedUtc = [DateTime]::UtcNow.ToString('o')
                }
                break
            }
        } while ($true)
    } else {
        $finished = $process.WaitForExit($timeout)
    }
    if (-not $finished) {
        $stopping = $true
        if ($process.GetType().GetMethod('Kill', [type[]]@([bool]))) {
            $process.Kill($true)
        } else {
            $process.Kill()
        }
        if (-not $process.WaitForExit(10000)) { throw 'The process did not exit after termination was requested.' }
        $result.Outcome = $stopOutcome
        if ($stopOutcome -eq 'Timeout') { $result.Error = "Process exceeded $TimeoutSeconds seconds." }
    } else {
        $result.Task.exitCode = $process.ExitCode
        if ($process.ExitCode -eq 0) { $result.Outcome = 'Passed' } else { $result.Outcome = 'Failed'; $result.Error = "Process exited with code $($process.ExitCode)." }
    }
    $streams = [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($outCopy, $errCopy))
    if ($stopping) {
        if ($streams.GetType().GetMethod('WaitAsync', [type[]]@([TimeSpan]))) {
            $streams = $streams.WaitAsync([TimeSpan]::FromSeconds(10))
            [void]$streams.GetAwaiter().GetResult()
        } else {
            [void]$streams.Wait(10000)
        }
    } else {
        [void]$streams.GetAwaiter().GetResult()
    }
} catch {
    if ($InitializeOnly) { throw }
    $result.Error = $_.Exception.ToString() + "`n" + $_.ScriptStackTrace
    $result.Outcome = if ($stopping) { 'Unknown' } else { 'Unavailable' }
    if ($process -and $result.Task.pid -and -not $process.HasExited) {
        try {
            if ($process.GetType().GetMethod('Kill', [type[]]@([bool]))) {
            $process.Kill($true)
        } else {
            $process.Kill()
        }
            if (-not $process.WaitForExit(10000)) { throw 'The process did not exit during cleanup.' }
        } catch {
            $result.Outcome = 'Unknown'
            $result.Error += "`nCleanup failed: $($_.Exception)"
        }
    }
} finally {
    if ($outStream) { $outStream.Dispose() }
    if ($errStream) { $errStream.Dispose() }
    if ($process) { $process.Dispose() }
    if (-not $keepInitialization) {
        if ($initialized) { $current.ProcessorAffinity = $oldAffinity }
        if ($null -eq $oldCount) { Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue } else { Set-Item Env:DOTNET_PROCESSOR_COUNT $oldCount }
        if ($null -eq $oldTelemetry) { Remove-Item Env:POWERSHELL_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue } else { Set-Item Env:POWERSHELL_TELEMETRY_OPTOUT $oldTelemetry }
    }
    if (-not $InitializeOnly -and $artifactSafe) {
        $result.Task.status = $result.Outcome
        try {
            $receipt = Join-Path $ArtifactDirectory 'receipt.json'
            [IO.File]::WriteAllText("$receipt.tmp", ($result | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
            [IO.File]::Move("$receipt.tmp", $receipt)
        } catch { $result.Outcome = 'Unavailable'; $result.Task.status = 'Unavailable'; $result.Error = "Receipt write failed: $($_.Exception)" }
    }
    if ($artifactClaim) { $artifactClaim.Dispose() }
    $current.Dispose()
}
if (-not $InitializeOnly) { [pscustomobject]$result }
