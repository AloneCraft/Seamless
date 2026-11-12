[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BeforeSource,
    [Parameter(Mandatory)][string]$BeforeManifest,
    [Parameter(Mandatory)][string]$AfterSource,
    [Parameter(Mandatory)][string]$AfterManifest,
    [Parameter(Mandatory)][string]$LogRoot,
    [switch]$Smoke,
    [int[]]$Cells,
    [ValidateSet('CarrierJoinOnly','JoinMultipleOnly')][string]$IsolatedQuery
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$LogRoot=[IO.Path]::GetFullPath($LogRoot)
if([IO.Path]::GetPathRoot($LogRoot) -ne 'F:\'){throw 'Use F for comparison evidence.'}
[IO.Directory]::CreateDirectory($LogRoot)|Out-Null
$lease=[IO.File]::Open((Join-Path $LogRoot 'comparison.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    $project='Benchmark/Experiments/ProductTransactions'
    $fixture=@('Program.cs','Program.OrderedComparison.cs','OrderedComparisonQueries.cs')
    $dllPath=$project+'/bin/Release/net10.0/ProductTransactions.dll'
    $versions=@(
        @{Name='Before';Source=$BeforeSource;Manifest=$BeforeManifest},
        @{Name='After';Source=$AfterSource;Manifest=$AfterManifest}
    )
    foreach($version in $versions){
        $version.Source=[IO.Path]::GetFullPath($version.Source)
        if([IO.Path]::GetPathRoot($version.Source) -ne 'D:\'){throw 'Use D for frozen sources.'}
        $version.Dll=Join-Path $version.Source $dllPath
        $version.AssemblyHash=(Get-FileHash -LiteralPath $version.Dll).Hash
        $version.SourceHash=(Get-FileHash -LiteralPath $version.Manifest).Hash
        $version.Inputs=Get-Content -LiteralPath $version.Manifest -Raw|ConvertFrom-Json -AsHashtable
        foreach($input in $version.Inputs.GetEnumerator()){
            if((Get-FileHash -LiteralPath (Join-Path $version.Source $input.Key)).Hash -ne $input.Value){
                throw ('Frozen input differs: '+$input.Key)
            }
        }
        $version.Libraries=@(Get-ChildItem -LiteralPath (Split-Path $version.Dll) -File -Filter '*.dll'|ForEach-Object {
            @{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
        $generatedRoot=Join-Path $version.Source ($project+'/obj/Release/generated')
        $version.Generated=@(Get-ChildItem -LiteralPath $generatedRoot -Recurse -File -Filter '*.cs'|ForEach-Object {
            @{Path=$_.FullName;Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
    }
    foreach($file in $fixture){
        $relative=($project+'/'+$file).Replace('/','\')
        if(!$versions[0].Inputs.Contains($relative) -or $versions[0].Inputs[$relative] -ne $versions[1].Inputs[$relative]){
            throw ('The workloads differ between builds: '+$relative)
        }
    }
    $versions|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $LogRoot 'builds.json') -Encoding utf8
    $inputKeys=@($versions[0].Inputs.Keys)+@($versions[1].Inputs.Keys)|Sort-Object -Unique
    @($inputKeys|Where-Object {$versions[0].Inputs[$_] -ne $versions[1].Inputs[$_]}|ForEach-Object {
        @{Path=$_;Before=$versions[0].Inputs[$_];After=$versions[1].Inputs[$_]}
    })|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $LogRoot 'input-changes.json') -Encoding utf8
    $env:TEMP=Join-Path (Split-Path $AfterSource) 'carrier-temp'
    $env:TMP=$env:TEMP
    [IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
    $env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home'
    $env:DOTNET_TieredCompilation='0'
    $profiles=@()
    foreach($rows in @(4096,16384)){
        foreach($storage in @('Memory','Storage')){
            foreach($workers in @(1,4)){
                $profiles+=@{Rows=$rows;Storage=$storage;Workers=$workers;Kind='Database'}
            }
        }
        foreach($provider in @('Array','List','Tsavorite','Persisted')){
            $profiles+=@{Rows=$rows;Provider=$provider;Kind='Collection'}
        }
    }
    foreach($workers in @(1,4)){
        $profiles+=@{Rows=16384;Storage='Memory';Workers=$workers;Kind='Spill'}
    }
    if($Smoke){$profiles=@($profiles[0],($profiles|Where-Object Kind -eq 'Collection'|Select-Object -First 1))}
    if($Cells -and @($Cells|Where-Object {$_ -lt 0 -or $_ -ge $profiles.Count}).Count){throw 'Unknown comparison cell.'}
    if($IsolatedQuery -and ($Smoke -or !$Cells -or @($Cells|Where-Object {$profiles[$_].Kind -ne 'Database'}).Count)){
        throw 'Select explicit database cells for an isolated query.'
    }
    $choices=@(@{Version=0;Flavor='FLinq';Label='Before'},@{Version=1;Flavor='FLinq';Label='After'},@{Version=1;Flavor='Linq';Label='Linq'})
    $runs=@()
    $summaries=@()
    $completed=0
    $runnerHash=(Get-FileHash -LiteralPath $PSCommandPath).Hash
    for($cell=0;$cell -lt $profiles.Count;$cell++){
        if($Cells -and $cell -notin $Cells){continue}
        $profile=$profiles[$cell]
        for($index=0;$index -lt $choices.Count;$index++){
            $choice=$choices[($cell+$index)%$choices.Count]
            $version=$versions[$choice.Version]
            $status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
            if(!$status.healthy -or !$status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard prevents comparison.'}
            $id=[Guid]::NewGuid().ToString('N')
            $data=Join-Path 'D:\CodexTemp\o' $id.Substring(0,12)
            $artifact=Join-Path $LogRoot ('cell-'+$cell+'-'+$choice.Label+'-'+$id)
            [IO.Directory]::CreateDirectory($artifact)|Out-Null
            $report=Join-Path $artifact 'report.json'
            if($profile.Kind -eq 'Collection'){
                $arguments=@($version.Dll,'--collection-carrier',$data,$report,$version.SourceHash,$profile.Rows.ToString(),$profile.Provider,$choice.Flavor)
            }else{
                $mode='Carrier'
                if($choice.Label -eq 'Before'){$mode='Baseline'}
                if($profile.Kind -eq 'Spill'){$mode='Spill'}
                if($IsolatedQuery){$mode=$IsolatedQuery}
                $arguments=@($version.Dll,'--carrier-comparison',$data,$report,$version.SourceHash,$profile.Rows.ToString(),$profile.Storage,$profile.Workers.ToString(),$choice.Flavor,'10',$mode)
            }
            @{Cell=$cell;Order=$index;Profile=$profile;Revision=$choice.Label;Arguments=$arguments;RunnerSha256=$runnerHash}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $artifact 'spec.json') -Encoding utf8
            $result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments $arguments -WorkingDirectory $version.Source -ArtifactDirectory $artifact -TimeoutSeconds 900
            $runs+=@{Cell=$cell;Revision=$choice.Label;Profile=$profile;Artifact=$artifact;Outcome=$result.Outcome}
            $runs|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $LogRoot 'runs.json') -Encoding utf8
            if($result.Outcome -ne 'Passed'){throw ('Comparison failed: '+$artifact)}
            $json=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
            if($json.Affinity -ne 255 -or $json.Rows -ne $profile.Rows -or $json.Flavor -ne $choice.Flavor -or $json.AssemblySha256 -ne $version.AssemblyHash -or $json.SourceManifestSha256 -ne $version.SourceHash){
                throw ('Comparison receipt differs: '+$artifact)
            }
            if($profile.Kind -eq 'Collection'){
                if($json.Provider -ne $profile.Provider -or @($json.Records).Count -ne 10){throw 'Collection profile differs.'}
            }else{
                $missing=@($json.Unavailable.PSObject.Properties).Count
                if($json.StorageMode -ne $profile.Storage -or $json.QueryWorkers -ne $profile.Workers -or
                    @($json.Records).Count -ne 10*($json.QueryCount-$missing)){
                    throw 'Database profile differs.'
                }
                if($IsolatedQuery -and ($json.Profile -ne $IsolatedQuery -or $json.QueryCount -ne 1 -or $missing -ne 0)){
                    throw 'Isolated query receipt differs.'
                }
            }
            foreach($query in ($json.Records|Group-Object Query)){
                if($query.Count -ne 10){throw ('Expected 10 samples: '+$artifact)}
                $times=@($query.Group.Milliseconds|Sort-Object)
                $alloc=@($query.Group.AllocatedBytes|Sort-Object)
                $summaries+=[pscustomobject]@{
                    Kind=$profile.Kind;Rows=$profile.Rows;Storage=$profile.Storage;Provider=$profile.Provider;Workers=$profile.Workers
                    Query=$query.Name;Revision=$choice.Label;MedianMs=($times[4]+$times[5])/2.0;AllocatedBytes=($alloc[4]+$alloc[5])/2.0
                    MinMs=$times[0];MaxMs=$times[9];PeakSpillBytes=($query.Group.PeakAdditionalReservedStorageBytes|Measure-Object -Maximum).Maximum
                }
            }
            Write-Host ('Passed cell '+$cell+' '+$profile.Kind+' '+$choice.Label)
        }
        $completed++
        $summaries|Export-Csv -LiteralPath (Join-Path $LogRoot 'summary.csv') -NoTypeInformation -Encoding utf8
    }
    foreach($version in $versions){
        foreach($file in @($version.Libraries)+@($version.Generated)){
            if((Get-FileHash -LiteralPath $file.Path).Hash -ne $file.Sha256){throw 'Build files changed during measurement.'}
        }
    }
    $summaries|Export-Csv -LiteralPath (Join-Path $LogRoot 'summary.csv') -NoTypeInformation -Encoding utf8
    @{Outcome='Passed';Cells=$completed;Processes=$runs.Count;TimedSamples=$summaries.Count*10;Smoke=[bool]$Smoke;RunnerSha256=$runnerHash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $LogRoot 'audit.json') -Encoding utf8
}finally{$lease.Dispose()}
