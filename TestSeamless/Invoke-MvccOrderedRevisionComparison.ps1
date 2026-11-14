[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineLogRoot,
    [Parameter(Mandatory)][string]$CandidateLogRoot,
    [Parameter(Mandatory)][string]$LogRoot
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$LogRoot=[IO.Path]::GetFullPath($LogRoot);if([IO.Path]::GetPathRoot($LogRoot) -ne 'F:\'){throw 'Use F evidence.'}
[IO.Directory]::CreateDirectory($LogRoot)|Out-Null
$lease=[IO.FileStream]::new((Join-Path $LogRoot 'revision-comparison.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    $revisions=@()
    foreach($profile in @(@{Name='Before';Root=$BaselineLogRoot},@{Name='After';Root=$CandidateLogRoot})){
        $runs=@(Get-Content -LiteralPath (Join-Path $profile.Root 'ordered-runs-Release.json') -Raw|ConvertFrom-Json)
        if($runs.Count -ne 16){throw 'Revision does not have all 16 successful profiles.'}
        foreach($run in $runs){$receipt=Get-Content -LiteralPath (Join-Path $run.Artifact 'receipt.json') -Raw|ConvertFrom-Json;if($receipt.Outcome -ne 'Passed' -or $receipt.Task.exitCode -ne 0){throw 'A revision run did not pass.'}}
        $generated=@(Get-Content -LiteralPath (Join-Path $profile.Root 'generated-Release.json') -Raw|ConvertFrom-Json)
        foreach($entry in $generated){if((Get-FileHash -LiteralPath $entry.File).Hash -ne $entry.Sha256){throw 'Frozen generated source differs.'}}
        $marker='\obj\Release\generated\';$offset=$generated[0].File.IndexOf($marker,[StringComparison]::OrdinalIgnoreCase)
        if($offset -lt 0){throw 'Unknown frozen build layout.'}
        $directory=Join-Path $generated[0].File.Substring(0,$offset) 'bin/Release/net10.0';$dll=Join-Path $directory 'ProductTransactions.dll'
        $spec=Get-Content -LiteralPath (Join-Path $runs[0].Artifact 'ordered-spec.json') -Raw|ConvertFrom-Json
        if((Get-FileHash -LiteralPath $dll).Hash -ne $spec.AssemblySha256){throw 'Frozen assembly changed.'}
        $manifest=Join-Path $runs[0].Artifact 'source-manifest.json'
        if((Get-FileHash -LiteralPath $manifest).Hash -ne $spec.SourceManifestSha256){throw 'Frozen manifest differs.'}
        $revisions+=@{Name=$profile.Name;Dll=$dll;Directory=$directory;AssemblySha256=$spec.AssemblySha256;Manifest=$manifest;ManifestSha256=$spec.SourceManifestSha256;Generated=$generated;Inputs=(Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json -AsHashtable);Libraries=@(Get-ChildItem -LiteralPath $directory -File -Filter '*.dll'|ForEach-Object {@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}
    }
    foreach($path in @('Benchmark\Experiments\ProductTransactions\Program.OrderedComparison.cs','Benchmark\Experiments\ProductTransactions\OrderedComparisonQueries.cs','Benchmark\Experiments\ProductTransactions\Database.cs')){
        if($revisions[0].Inputs[$path] -ne $revisions[1].Inputs[$path]){throw ('The workload changed between revisions: '+$path)}
    }
    $changes=@();foreach($path in $revisions[1].Inputs.Keys){if($revisions[0].Inputs[$path] -ne $revisions[1].Inputs[$path]){$changes+=@{Path=$path;Before=$revisions[0].Inputs[$path];After=$revisions[1].Inputs[$path]}}}
    $env:TEMP=Join-Path (Split-Path $revisions[1].Directory) 'revision-temp';$env:TMP=$env:TEMP
    [IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
    $env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:NUGET_HTTP_CACHE_PATH=Join-Path $LogRoot 'http-cache';$env:DOTNET_TieredCompilation='0'
    $choices=@(@{Revision=0;Flavor='FLinq'},@{Revision=1;Flavor='FLinq'},@{Revision=0;Flavor='Linq'},@{Revision=1;Flavor='Linq'})
    $records=@();$rotation=0
    foreach($rows in @(4096,16384)){foreach($storage in @('Memory','Storage')){foreach($workers in @(1,4)){
        for($order=0;$order -lt 4;$order++){
            $choice=$choices[($order+$rotation)%4];$revision=$revisions[$choice.Revision]
            $status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
            if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard prevents comparison.'}
            $id=[Guid]::NewGuid().ToString('N');$artifact=Join-Path $LogRoot ('paired-'+$rows+'-'+$storage+'-'+$workers+'-'+$revision.Name+'-'+$choice.Flavor+'-'+$id)
            $data=Join-Path 'D:\CodexTemp\o' $id.Substring(0,12);[IO.Directory]::CreateDirectory($artifact)|Out-Null
            @{Rows=$rows;Storage=$storage;Workers=$workers;Revision=$revision.Name;Flavor=$choice.Flavor;Order=$order;Rotation=$rotation;SourceManifestSha256=$revision.ManifestSha256;AssemblySha256=$revision.AssemblySha256;DataRoot=$data;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifact 'revision-spec.json') -Encoding utf8
            Copy-Item -LiteralPath $revision.Manifest -Destination (Join-Path $artifact 'source-manifest.json')
            $result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($revision.Dll,'--ordered-comparison',$data,(Join-Path $artifact 'report.json'),$revision.ManifestSha256,$rows.ToString(),$storage,$workers.ToString(),$choice.Flavor,'10') -WorkingDirectory $revision.Directory -ArtifactDirectory $artifact -TimeoutSeconds 900
            $records+=@{Revision=$revision.Name;Artifact=$artifact;Result=$result};$records|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $LogRoot 'revision-runs.json') -Encoding utf8
            if($result.Outcome -ne 'Passed'){throw ('Revision comparison failed: '+$artifact)}
            Write-Host ('Passed: '+$rows+'/'+$storage+'/'+$workers+' '+$revision.Name+'/'+$choice.Flavor)
        }
        $rotation++
    }}}
    foreach($revision in $revisions){foreach($library in $revision.Libraries){if((Get-FileHash -LiteralPath $library.Path).Hash -ne $library.Sha256){throw 'Runtime library changed during comparison.'}}}
    $revisions|ForEach-Object {@{Name=$_.Name;AssemblySha256=$_.AssemblySha256;Manifest=$_.Manifest;ManifestSha256=$_.ManifestSha256;Generated=$_.Generated;Libraries=$_.Libraries}}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $LogRoot 'revision-builds.json') -Encoding utf8
    $changes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $LogRoot 'revision-input-changes.json') -Encoding utf8
}finally{$lease.Dispose()}
