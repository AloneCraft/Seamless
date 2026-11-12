param([Parameter(Mandatory)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or -not [IO.Path]::IsPathFullyQualified($TestDirectory) -or $TestDirectory -notmatch '^[DF]:\\') { throw 'Use an absolute, dedicated D: or F: test directory on Windows.' }
if (Test-Path -LiteralPath $TestDirectory) { throw 'TestDirectory must not already exist.' }
$runner = Join-Path (Split-Path $PSScriptRoot -Parent) 'Invoke-LocalProcess.ps1'
$summary = Join-Path (Split-Path $PSScriptRoot -Parent) 'Get-GatewayVerificationSummary.ps1'
$pwsh = (Get-Process -Id $PID).Path
[IO.Directory]::CreateDirectory($TestDirectory) | Out-Null
$originalCount = [Environment]::GetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', 'Process')
$originalTelemetry = [Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', 'Process')
$init = $null
$other = $null
$trackedJob = $null
$cancelJob = $null
$release = Join-Path $TestDirectory 'release-tracked'
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Invoke-Case([string]$name, [string]$code, [int]$timeout = 30) {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
    & $runner -Executable $pwsh -Arguments @('-NoProfile', '-EncodedCommand', $encoded) -WorkingDirectory $TestDirectory -ArtifactDirectory (Join-Path $TestDirectory $name) -TimeoutSeconds $timeout
}
try {
    $init = & $runner -InitializeOnly
    Assert-True ($init.EffectiveLogicalProcessors -ge 1 -and $init.EffectiveLogicalProcessors -le 8) 'Invalid processor count.'
    $inherit = @'
$grandchildCode = '[pscustomobject]@{ affinity = [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity.ToInt64(); count = [Environment]::ProcessorCount; env = $env:DOTNET_PROCESSOR_COUNT } | ConvertTo-Json -Compress'
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($grandchildCode))
$start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
$start.UseShellExecute = $false; $start.RedirectStandardOutput = $true
$start.CreateNoWindow = $true
foreach ($arg in @('-NoProfile','-EncodedCommand',$encoded)) { $start.ArgumentList.Add($arg) }
$p = [Diagnostics.Process]::Start($start)
$json = $p.StandardOutput.ReadToEnd(); $p.WaitForExit()
if ($p.ExitCode -ne 0) { exit 1 }
[pscustomobject]@{ affinity = [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity.ToInt64(); count = [Environment]::ProcessorCount; env = $env:DOTNET_PROCESSOR_COUNT; grandchild = ($json | ConvertFrom-Json) } | ConvertTo-Json -Compress
'@
    $r = Invoke-Case 'inherit' $inherit
    Assert-True ($r.Outcome -eq 'Passed') "Inheritance run failed: $($r.Error)"
    $data = Get-Content -Raw (Join-Path $TestDirectory 'inherit\stdout.log') | ConvertFrom-Json
    Assert-True ($data.affinity -eq $init.AffinityMask.ToInt64() -and $data.grandchild.affinity -eq $data.affinity) 'Child/grandchild affinity differs.'
    Assert-True ($data.count -eq $init.EffectiveLogicalProcessors -and $data.grandchild.count -eq $data.count -and $data.env -eq [string]$data.count -and $data.grandchild.env -eq $data.env) 'Effective processor count differs.'
    Assert-True ([Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity -eq $init.AffinityMask) 'Nested invocation did not restore initialized affinity.'
    $r = Invoke-Case 'failure' 'exit 23'
    Assert-True ($r.Outcome -eq 'Failed' -and $r.Task.exitCode -eq 23) 'Nonzero exit was not recorded.'
    $receipt = Get-Content -Raw (Join-Path $TestDirectory 'failure\receipt.json') | ConvertFrom-Json
    Assert-True ($receipt.Outcome -eq 'Failed' -and $receipt.Task.exitCode -eq 23) 'Receipt differs from result.'
    $trackedDirectory = Join-Path $TestDirectory 'tracked'
    $trackedJob = Start-ThreadJob -ArgumentList @($runner,$pwsh,$TestDirectory,$trackedDirectory) -ScriptBlock {
        param($runner,$pwsh,$work,$artifact)
        $code = 'while (!(Test-Path -LiteralPath (Join-Path (Get-Location) "release-tracked"))) { Start-Sleep -Milliseconds 50 }; "tracked completed"'
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
        & $runner -Executable $pwsh -Arguments @('-NoProfile','-EncodedCommand',$encoded) -WorkingDirectory $work -ArtifactDirectory $artifact -TimeoutSeconds 30
    }
    $startPath = Join-Path $trackedDirectory 'process-start.json'
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $startPath) -and $deadline.Elapsed.TotalSeconds -lt 10) { Start-Sleep -Milliseconds 50 }
    $start = Get-Content -LiteralPath $startPath -Raw | ConvertFrom-Json
    $observed = (& $summary -RunRoot $trackedDirectory -Suite ManagementChecks).Stages | Where-Object Stage -eq 'checks'
    Assert-True ($observed.Outcome -eq 'Running' -and $observed.RunnerState -eq 'Running' -and $observed.Pid -eq $start.Task.pid) 'The summary did not verify the live process identity.'
    $r = Invoke-Case 'tracked' '[IO.File]::WriteAllText("unexpected-duplicate", "started")'
    Assert-True ($r.Outcome -eq 'Unavailable' -and !$r.Task.pid) 'A duplicate execution was started.'
    Assert-True (!(Test-Path -LiteralPath (Join-Path $trackedDirectory 'receipt.json'))) 'A duplicate wrote a terminal receipt.'
    [IO.File]::WriteAllText($release, '')
    $completed = Receive-Job -Job $trackedJob -Wait -ErrorAction Stop
    Assert-True ($completed.Outcome -eq 'Passed' -and $completed.Task.id -eq $start.Task.id) 'The owner did not complete the recorded attempt.'
    $trackedReceipt = Join-Path $trackedDirectory 'receipt.json'
    $receiptHash = (Get-FileHash -LiteralPath $trackedReceipt).Hash
    $r = Invoke-Case 'tracked' 'exit 24'
    Assert-True ($r.Outcome -eq 'Unavailable' -and (Get-FileHash -LiteralPath $trackedReceipt).Hash -eq $receiptHash) 'A second attempt changed the completed receipt.'
    $observed = (& $summary -RunRoot $trackedDirectory -Suite ManagementChecks).Stages | Where-Object Stage -eq 'checks'
    Assert-True ($observed.Outcome -eq 'Passed') 'The terminal receipt did not supersede the process start record.'
    $unknownDirectory = Join-Path $TestDirectory 'unknown'
    [IO.Directory]::CreateDirectory($unknownDirectory) | Out-Null
    $start.Task.pid = $PID
    $start.Task.creationUtc = '2000-01-01T00:00:00Z'
    $start | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $unknownDirectory 'process-start.json')
    $observed = (& $summary -RunRoot $unknownDirectory -Suite ManagementChecks).Stages | Where-Object Stage -eq 'checks'
    Assert-True ($observed.Outcome -eq 'Unknown') 'A PID with a different start time was accepted as the original process.'
    $r = Invoke-Case 'streams' '[Console]::Out.Write(("x" * 200000)); [Console]::Error.Write(("y" * 200000))'
    Assert-True ($r.Outcome -eq 'Passed') "Stream run failed: $($r.Error)"
    Assert-True ((Get-Item (Join-Path $TestDirectory 'streams\stdout.log')).Length -eq 200000 -and (Get-Item (Join-Path $TestDirectory 'streams\stderr.log')).Length -eq 200000) 'Stream output was truncated.'
    $otherStart = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $otherStart.UseShellExecute = $false
    $otherStart.CreateNoWindow = $true
    foreach ($arg in @('-NoProfile','-Command','Start-Sleep -Seconds 120')) { $otherStart.ArgumentList.Add($arg) }
    $other = [Diagnostics.Process]::Start($otherStart)
    $cancelDirectory = Join-Path $TestDirectory 'cancelled'
    $cancelJob = Start-ThreadJob -ArgumentList @($runner,$pwsh,$TestDirectory,$cancelDirectory) -ScriptBlock {
        param($runner,$pwsh,$work,$artifact)
        $code = '$p=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @("-NoProfile","-Command","Start-Sleep -Seconds 120") -WindowStyle Hidden -PassThru; [IO.File]::WriteAllText((Join-Path (Get-Location) "cancel-grandchild.pid"), [string]$p.Id); Start-Sleep -Seconds 120'
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
        & $runner -Executable $pwsh -Arguments @('-NoProfile','-EncodedCommand',$encoded) -WorkingDirectory $work -ArtifactDirectory $artifact -TimeoutSeconds 30 -AllowCancellation
    }
    $childMarker = Join-Path $TestDirectory 'cancel-grandchild.pid'
    $deadline.Restart()
    while (!(Test-Path -LiteralPath $childMarker) -and $deadline.Elapsed.TotalSeconds -lt 10) { Start-Sleep -Milliseconds 50 }
    $grandchildId = [int](Get-Content -LiteralPath $childMarker -Raw)
    $cancelStart = Get-Content -LiteralPath (Join-Path $cancelDirectory 'process-start.json') -Raw | ConvertFrom-Json
    [IO.File]::WriteAllText((Join-Path $cancelDirectory "cancel-$([guid]::NewGuid()).request"), '')
    Start-Sleep -Milliseconds 300
    $observed = (& $summary -RunRoot $cancelDirectory -Suite ManagementChecks).Stages | Where-Object Stage -eq 'checks'
    Assert-True ($observed.Outcome -eq 'Running' -and !$observed.CancellationRequested) 'A cancellation request for another attempt was accepted.'
    [IO.File]::WriteAllText((Join-Path $cancelDirectory $cancelStart.Task.cancellationRequestFile), '')
    $cancelled = Receive-Job -Job $cancelJob -Wait -ErrorAction Stop
    Assert-True ($cancelled.Outcome -eq 'Cancelled' -and $cancelled.Task.cancellationRequestedUtc -and $cancelled.Task.id -eq $cancelStart.Task.id) "Cancellation did not complete: $($cancelled.Error)"
    $observed = (& $summary -RunRoot $cancelDirectory -Suite ManagementChecks).Stages | Where-Object Stage -eq 'checks'
    Assert-True ($observed.Outcome -eq 'Cancelled' -and $observed.CancellationRequested) 'The summary did not distinguish a request from completed cancellation.'
    foreach ($id in $cancelled.Task.pid,$grandchildId) {
        $process = Get-Process -Id $id -ErrorAction SilentlyContinue
        Assert-True (!$process -or $process.HasExited) 'A cancelled process remains active.'
    }
    Assert-True (-not $other.HasExited) 'Cancellation terminated an unrelated process.'
    $r = Invoke-Case 'timeout' '$p = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @("-NoProfile", "-Command", "Start-Sleep -Seconds 120") -WindowStyle Hidden -PassThru; [IO.File]::WriteAllText((Join-Path (Get-Location) "grandchild.pid"), [string]$p.Id); Start-Sleep -Seconds 120' 5
    Assert-True ($r.Outcome -eq 'Timeout') "Timeout not detected: $($r.Error)"
    Assert-True (-not $other.HasExited) 'Unrelated owned process was terminated.'
    $terminated = $false
    try { $terminated = (Get-Process -Id $r.Task.pid -ErrorAction Stop).HasExited } catch { $terminated = $true }
    Assert-True $terminated 'Timed-out child remains active.'
    $grandchildId = [int](Get-Content -Raw (Join-Path $TestDirectory 'grandchild.pid'))
    $terminated = $false
    try { $terminated = (Get-Process -Id $grandchildId -ErrorAction Stop).HasExited } catch { $terminated = $true }
    Assert-True $terminated 'Timed-out grandchild remains active.'
    Write-Output "Passed: inheritance, processor count, failure receipt, live identity, duplicate prevention, unknown result, 200000-byte streams, cancellation and timeout isolation. Evidence: $TestDirectory"
} finally {
    if ($cancelJob) {
        Wait-Job -Job $cancelJob -Timeout 45 | Out-Null
        Remove-Job -Job $cancelJob
    }
    if ($trackedJob) {
        [IO.File]::WriteAllText($release, '')
        Wait-Job -Job $trackedJob -Timeout 35 | Out-Null
        Remove-Job -Job $trackedJob
    }
    if ($other) { if (-not $other.HasExited) { $other.Kill($true); $other.WaitForExit() }; $other.Dispose() }
    if ($init) { [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = $init.OriginalAffinityMask }
    if ($null -eq $originalCount) { Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue } else { Set-Item Env:DOTNET_PROCESSOR_COUNT $originalCount }
    if ($null -eq $originalTelemetry) { Remove-Item Env:POWERSHELL_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue } else { Set-Item Env:POWERSHELL_TELEMETRY_OPTOUT $originalTelemetry }
}
