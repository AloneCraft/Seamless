[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [string]$BuildLogRoot=$LogRoot,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [switch]$VerifyOnly,
    [switch]$BufferedControl
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
if([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($TaskRoot)) -ne 'F:\' -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($LogRoot)) -ne 'F:\' -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($BuildLogRoot)) -ne 'F:\'){throw 'Use F work and logs/caches'}
$build=& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $BuildLogRoot -Project 'Benchmark/Experiments/Experiments.csproj' -ExperimentMode HistoryStoreModes -Configuration $Configuration -BuildOnly
if($build.Outcome -ne 'Passed'){throw 'History comparison build failed'}
if(@(Select-String -LiteralPath (Join-Path $build.Task.artifactDirectory 'stdout.log') -Pattern ': warning ').Count){throw 'History comparison build has warnings'}
$snapshot=Join-Path $TaskRoot 'source';$dll=Join-Path $snapshot ('Benchmark/Experiments/bin/HistoryStoreModes/'+$Configuration+'/net10.0/HistoryStoreModes.dll')
$manifest=Join-Path $build.Task.artifactDirectory 'source-manifest.json';$hash=(Get-FileHash -LiteralPath $manifest).Hash
New-Item -ItemType Directory -Path $LogRoot -Force|Out-Null
$phases=@('verify');if(-not $VerifyOnly){if($Configuration -ne 'Release'){throw 'Performance comparison requires Release'};$phases+=@('run')}
$results=@()
foreach($phase in $phases){
    $id=[guid]::NewGuid().ToString('N');$artifacts=Join-Path $LogRoot ($Configuration+'-'+$phase+'-'+$id);$data=Join-Path 'F:\Seamless\HistoryStoreModes' $id.Substring(0,12)
    New-Item -ItemType Directory -Path $artifacts|Out-Null;Copy-Item -LiteralPath $manifest -Destination (Join-Path $artifacts 'source-manifest.json')
    @{Phase=$phase;Configuration=$Configuration;BufferedControl=[bool]$BufferedControl;DataRoot=$data;SourceManifestSha256=$hash;AssemblySha256=(Get-FileHash -LiteralPath $dll).Hash;Build=$build;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $artifacts 'spec.json')
    $status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
    if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard prevents comparison'}
    $arguments=@($dll,('--'+$phase),$data,(Join-Path $artifacts 'report.json'),$hash);if($phase -eq 'run' -and $BufferedControl){$arguments+='--buffered'}
    $result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments $arguments -WorkingDirectory $snapshot -ArtifactDirectory $artifacts -TimeoutSeconds 7200
    $results+=@{Phase=$phase;Configuration=$Configuration;Result=$result};$results|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $LogRoot ($Configuration+'-results.json'))
    $result;if($result.Outcome -ne 'Passed'){throw ('History '+$phase+' failed: '+$artifacts)}
}
