[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateSet(1,4)][int]$Workers=1,
    [switch]$AnalyzeOnly
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$Source=[IO.Path]::GetFullPath($Source)
$LogRoot=[IO.Path]::GetFullPath($LogRoot)
if([IO.Path]::GetPathRoot($Source) -ne 'D:\' -or [IO.Path]::GetPathRoot($LogRoot) -ne 'F:\'){
    throw 'Use D for frozen sources and F for trace evidence.'
}
[IO.Directory]::CreateDirectory($LogRoot)|Out-Null
$env:TEMP=Join-Path (Split-Path $Source) 'trace-temp'
$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home'
$env:DOTNET_TieredCompilation='0'
$trace=(Get-Command dotnet-trace -CommandType Application).Source
$dll=Join-Path $Source 'Benchmark/Experiments/ProductTransactions/bin/Release/net10.0/ProductTransactions.dll'
$sourceHash=(Get-FileHash -LiteralPath $Manifest).Hash
$data=Join-Path 'D:\CodexTemp\o' ([Guid]::NewGuid().ToString('N').Substring(0,12))
$traceFile=Join-Path $LogRoot 'join.nettrace'
$profileReport=Join-Path $LogRoot 'profile-report.json'
$argsForTrace=@('collect','--profile','dotnet-sampled-thread-time','--output',$traceFile,'--',
    'C:\Program Files\dotnet\dotnet.exe',$dll,'--carrier-comparison',$data,$profileReport,$sourceHash,
    '16384','Memory',$Workers.ToString(),'FLinq','25','Cpu')
$memory=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
if(!$memory.healthy -or !$memory.lastStatus.codingActive -or $memory.currentUsedPercent -ge 85){throw 'Memory guard prevents sampling.'}
if(!$AnalyzeOnly){
    $run=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable $trace -Arguments $argsForTrace -WorkingDirectory $Source -ArtifactDirectory (Join-Path $LogRoot 'collect') -TimeoutSeconds 300
    if($run.Outcome -ne 'Passed'){throw 'Sampling failed.'}
}
$report=Get-Content -LiteralPath $profileReport -Raw|ConvertFrom-Json
if($report.AssemblySha256 -ne (Get-FileHash -LiteralPath $dll).Hash -or $report.Affinity -ne 255 -or
    $report.SourceManifestSha256 -ne $sourceHash -or $report.QueryWorkers -ne $Workers -or $report.Profile -ne 'Cpu'){
    throw 'Profile build or affinity differs.'
}
if(!$AnalyzeOnly){
    $run=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable $trace -Arguments @('convert',$traceFile,'--format','Speedscope') -WorkingDirectory $Source -ArtifactDirectory (Join-Path $LogRoot 'convert') -TimeoutSeconds 300
    if($run.Outcome -ne 'Passed'){throw 'Trace conversion failed.'}
}
$speedscope=@(Get-ChildItem -LiteralPath $LogRoot -File -Filter '*.speedscope.json')
if($speedscope.Count -ne 1){throw 'Expected one converted stack profile.'}
$profile=Get-Content -LiteralPath $speedscope[0].FullName -Raw|ConvertFrom-Json -Depth 100
$totals=@{}
function Add-StackWeight($stack,[double]$weight){
    $frames=@($stack|ForEach-Object {$profile.shared.frames[$_].name})
    $query=$null
    foreach($frame in $frames){
        if($frame -match 'OptimizedLinq_(JoinIdentity|JoinMultiple|GroupJoinCounts|GroupJoinOwned|CarrierJoin)'){
            $query=$Matches[1]
            break
        }
    }
    if(!$query){return}
    $phase='その他・インライン化'
    for($depth=$frames.Count-1;$depth -ge 0;$depth--){
        $frame=$frames[$depth]
        if($frame -match 'ResourceBudget|ResourceReservation|CpuScheduler|Channel|Monitor.Wait|WaitHandle'){
            $phase='予約・同期・待機';break
        }
        if($frame -match 'Tsavorite\.core|VersionedStore|ReadVisible|EnumerateVisible|NativeRecord'){
            $phase='行読取り';break
        }
        if($frame -match 'QueryOrdering|EnumerableSorter|ArraySortHelper'){
            $phase='整列';break
        }
        if($frame -match 'QueryGrouping|QueryResultBuffer'){
            $phase='結果所有化・構築';break
        }
        if($frame -match 'Lookup.*(FindGroup|GetGroup|Match)'){
            $phase='照合';break
        }
        if($frame -match 'Lookup.*(Add|Build|RunWorker)'){
            $phase='Lookup構築';break
        }
    }
    $key=$query+'/'+$phase
    if(!$totals.ContainsKey($key)){$totals[$key]=@{Query=$query;Phase=$phase;Intervals=0;Weight=0.0}}
    $totals[$key].Intervals++
    $totals[$key].Weight+=$weight
}
foreach($thread in $profile.profiles){
    if($thread.type -eq 'sampled'){
        for($index=0;$index -lt $thread.samples.Count;$index++){
            Add-StackWeight $thread.samples[$index] $thread.weights[$index]
        }
    }elseif($thread.type -eq 'evented'){
        $stack=[Collections.Generic.List[int]]::new()
        $position=[double]$thread.startValue
        foreach($event in $thread.events){
            $at=[double]$event.at
            if($stack.Count -gt 0 -and $at -gt $position){Add-StackWeight $stack ($at-$position)}
            if($event.type -eq 'O'){
                $stack.Add([int]$event.frame)
            }else{
                $top=$stack.Count-1
                if($event.type -ne 'C' -or $top -lt 0 -or $stack[$top] -ne $event.frame){throw 'Unbalanced stack profile.'}
                $stack.RemoveAt($top)
            }
            $position=$at
        }
        if($stack.Count -ne 0){throw 'Incomplete stack profile.'}
    }
}
if($totals.Count -eq 0){throw 'No Join query stacks were attributed.'}
@{
    Profile='dotnet-sampled-thread-time'
    Classification='Nearest recognized frame on query stacks. Includes waiting. Worker-only stacks without query names are not attributed. Evented stack interval counts are not raw sample counts.'
    Units=@($profile.profiles|Select-Object -ExpandProperty unit -Unique)
    Weights=@($totals.Values|Sort-Object Query,Phase)
    TraceSha256=(Get-FileHash -LiteralPath $traceFile).Hash
    SourceManifestSha256=$sourceHash
    Workers=$Workers
}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $LogRoot 'phases.json') -Encoding utf8
