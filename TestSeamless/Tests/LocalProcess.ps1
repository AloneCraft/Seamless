param([Parameter(Mandatory)][string]$TestDirectory)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or -not [IO.Path]::IsPathFullyQualified($TestDirectory) -or $TestDirectory -notmatch '^[DF]:\\') { throw 'Use an absolute, dedicated D: or F: test directory on Windows.' }
if (Test-Path -LiteralPath $TestDirectory) { throw 'TestDirectory must not already exist.' }
$runner = Join-Path (Split-Path $PSScriptRoot -Parent) 'Invoke-LocalProcess.ps1'
$pwsh = (Get-Process -Id $PID).Path
[IO.Directory]::CreateDirectory($TestDirectory) | Out-Null
$originalCount = [Environment]::GetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', 'Process')
$originalTelemetry = [Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT', 'Process')
$init = $null
$other = $null
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
    $r = Invoke-Case 'streams' '[Console]::Out.Write(("x" * 200000)); [Console]::Error.Write(("y" * 200000))'
    Assert-True ($r.Outcome -eq 'Passed') "Stream run failed: $($r.Error)"
    Assert-True ((Get-Item (Join-Path $TestDirectory 'streams\stdout.log')).Length -eq 200000 -and (Get-Item (Join-Path $TestDirectory 'streams\stderr.log')).Length -eq 200000) 'Stream output was truncated.'
    $otherStart = [Diagnostics.ProcessStartInfo]::new($pwsh)
    $otherStart.UseShellExecute = $false
    $otherStart.CreateNoWindow = $true
    foreach ($arg in @('-NoProfile','-Command','Start-Sleep -Seconds 120')) { $otherStart.ArgumentList.Add($arg) }
    $other = [Diagnostics.Process]::Start($otherStart)
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
    Write-Output "Passed: inheritance, processor count, failure receipt, 200000-byte streams, timeout isolation. Evidence: $TestDirectory"
} finally {
    if ($other) { if (-not $other.HasExited) { $other.Kill($true); $other.WaitForExit() }; $other.Dispose() }
    if ($init) { [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = $init.OriginalAffinityMask }
    if ($null -eq $originalCount) { Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue } else { Set-Item Env:DOTNET_PROCESSOR_COUNT $originalCount }
    if ($null -eq $originalTelemetry) { Remove-Item Env:POWERSHELL_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue } else { Set-Item Env:POWERSHELL_TELEMETRY_OPTOUT $originalTelemetry }
}
