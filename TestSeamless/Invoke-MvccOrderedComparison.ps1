[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [ValidateRange(3,25)][int]$Repeats=10,
    [switch]$Smoke
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$TaskRoot=[IO.Path]::GetFullPath($TaskRoot);$LogRoot=[IO.Path]::GetFullPath($LogRoot)
if([IO.Path]::GetPathRoot($TaskRoot) -ne 'F:\' -or [IO.Path]::GetPathRoot($LogRoot) -ne 'F:\'){throw 'Use F work/data and logs/cache.'}
[IO.Directory]::CreateDirectory($LogRoot)|Out-Null
$lease=[IO.FileStream]::new((Join-Path $LogRoot 'ordered-comparison.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    $build=& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $LogRoot -Project 'Benchmark/Experiments/Experiments.csproj' -ExperimentMode ProductTransactions -Configuration $Configuration -BuildOnly -ForceCompile
    if($build.Outcome -ne 'Passed'){throw 'Ordered comparison build failed.'}
    if(Select-String -LiteralPath (Join-Path $build.Task.artifactDirectory 'stdout.log') -Pattern '\bwarning [A-Z]+[0-9]+:'){throw 'Build has warnings.'}
    $snapshot=Join-Path $TaskRoot 'source'
    $dll=Join-Path $snapshot ('Benchmark/Experiments/bin/ProductTransactions/'+$Configuration+'/net10.0/ProductTransactions.dll')
    $manifest=Join-Path $build.Task.artifactDirectory 'source-manifest.json';$hash=(Get-FileHash -LiteralPath $manifest).Hash
    $generated=@(Get-ChildItem -LiteralPath (Join-Path $snapshot ('Benchmark/Experiments/obj/ProductTransactions/'+$Configuration+'/generated')) -Recurse -File -Filter '*.cs')
    $plans=($generated|Where-Object Name -like '*OrderedComparisonQueries*'|ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw})-join "`n"
    foreach($proof in @('OptimizedLinq_JoinIdentity_','OptimizedLinq_JoinMultiple_','OptimizedLinq_GroupIdentity_','OptimizedLinq_GroupEscapeMultiple_','[Join/整列/取引]','[GroupBy/整列/取引]','QueryOrdering.InputBudget','[整列/恒等キー]','後続整列の作業領域を先に予約')){if(-not $plans.Contains($proof)){throw ('Missing generated workload proof: '+$proof)}}
    $generated|ForEach-Object {@{File=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $LogRoot ('generated-'+$Configuration+'.json')) -Encoding utf8
    $env:DOTNET_TieredCompilation='0'
    $rows=@(4096,16384);$storages=@('Memory','Storage');$workers=@(1,4)
    if($Smoke){$rows=@(4096);$storages=@('Memory');$workers=@(1)}
    $runs=@();$order=0
    foreach($count in $rows){foreach($storage in $storages){foreach($worker in $workers){
        $flavors=@('FLinq','Linq');if($order%2){$flavors=@('Linq','FLinq')};$order++
        foreach($flavor in $flavors){
            $status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
            if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard prevents comparison.'}
            $identity=[Guid]::NewGuid().ToString('N');$artifact=Join-Path $LogRoot ('ordered-'+$Configuration+'-'+$count+'-'+$storage+'-'+$worker+'-'+$flavor+'-'+$identity)
            $data=Join-Path 'F:\Seamless\o' $identity.Substring(0,12)
            [IO.Directory]::CreateDirectory($artifact)|Out-Null
            Copy-Item -LiteralPath $manifest -Destination (Join-Path $artifact 'source-manifest.json')
            @{Rows=$count;StorageMode=$storage;QueryWorkers=$worker;Flavor=$flavor;Repeats=$Repeats;Configuration=$Configuration;Smoke=[bool]$Smoke;DataRoot=$data;SourceManifestSha256=$hash;BuildArtifact=$build.Task.artifactDirectory;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;AssemblySha256=(Get-FileHash -LiteralPath $dll).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifact 'ordered-spec.json') -Encoding utf8
            $result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,'--ordered-comparison',$data,(Join-Path $artifact 'report.json'),$hash,$count.ToString(),$storage,$worker.ToString(),$flavor,$Repeats.ToString()) -WorkingDirectory $snapshot -ArtifactDirectory $artifact -TimeoutSeconds 900
            $runs+=@{Artifact=$artifact;Result=$result};$runs|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $LogRoot ('ordered-runs-'+$Configuration+'.json')) -Encoding utf8
            if($result.Outcome -ne 'Passed'){throw ('Ordered comparison failed: '+$artifact)}
            Write-Host ('Passed: '+$count+' rows, '+$storage+', workers='+$worker+', '+$flavor)
        }
    }}}
    $runs
}finally{$lease.Dispose()}
