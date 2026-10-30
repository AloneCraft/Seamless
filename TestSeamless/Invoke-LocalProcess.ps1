[CmdletBinding()]
param(
    [string]$Executable,
    [string[]]$Arguments = @(),
    [string]$WorkingDirectory,
    [string]$ArtifactDirectory,
    [ValidateRange(1, 2147483)][int]$TimeoutSeconds = 7200,
    [ValidateRange(1, 8)][int]$MaxLogicalProcessors = 8,
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
$result = [ordered]@{
    Outcome = 'Unavailable'; Error = $null
    Task = [ordered]@{ id = [guid]::NewGuid().ToString(); status = 'Unavailable'; exitCode = $null; artifactDirectory = $ArtifactDirectory; pid = $null; creationUtc = $null; affinity = $null }
}
try {
    if (-not $IsWindows -or [IntPtr]::Size -ne 8) { throw 'Unavailable: 64-bit Windows is required.' }
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
    if (-not [IO.Path]::IsPathFullyQualified($Executable) -or -not [IO.Path]::IsPathFullyQualified($WorkingDirectory) -or -not [IO.Path]::IsPathFullyQualified($ArtifactDirectory)) {
        throw 'Executable, WorkingDirectory and ArtifactDirectory must be absolute paths.'
    }
    if ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ArtifactDirectory)) -eq 'C:\' -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($WorkingDirectory)) -eq 'C:\') { throw 'Comparison work and artifacts must not use the C drive.' }
    $artifactSafe = $true
    [IO.Directory]::CreateDirectory($ArtifactDirectory) | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $outStream = [IO.FileStream]::new((Join-Path $ArtifactDirectory 'stdout.log'), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $errStream = [IO.FileStream]::new((Join-Path $ArtifactDirectory 'stderr.log'), [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Process did not start.' }
    $result.Task.pid = $process.Id
    $result.Task.creationUtc = $process.StartTime.ToUniversalTime().ToString('o')
    $outCopy = $process.StandardOutput.BaseStream.CopyToAsync($outStream)
    $errCopy = $process.StandardError.BaseStream.CopyToAsync($errStream)
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        $result.Outcome = 'Timeout'
        $result.Error = "Process exceeded $TimeoutSeconds seconds."
    } else {
        $result.Task.exitCode = $process.ExitCode
        if ($process.ExitCode -eq 0) { $result.Outcome = 'Passed' } else { $result.Outcome = 'Failed'; $result.Error = "Process exited with code $($process.ExitCode)." }
    }
    [void][Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($outCopy, $errCopy)).GetAwaiter().GetResult()
} catch {
    if ($InitializeOnly) { throw }
    $result.Error = $_.Exception.ToString()
    if ($process -and $result.Task.pid -and -not $process.HasExited) {
        try { $process.Kill($true); $process.WaitForExit() } catch { $result.Error += "`nCleanup failed: $($_.Exception)" }
    }
    if ($result.Outcome -ne 'Timeout') { $result.Outcome = 'Unavailable' }
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
            [IO.Directory]::CreateDirectory($ArtifactDirectory) | Out-Null
            [IO.File]::WriteAllText((Join-Path $ArtifactDirectory 'receipt.json'), ($result | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
        } catch { $result.Outcome = 'Unavailable'; $result.Task.status = 'Unavailable'; $result.Error = "Receipt write failed: $($_.Exception)" }
    }
    $current.Dispose()
}
if (-not $InitializeOnly) { [pscustomobject]$result }
