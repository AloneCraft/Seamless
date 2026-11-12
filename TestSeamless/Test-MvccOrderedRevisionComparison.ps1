[CmdletBinding()]
param([Parameter(Mandatory)][string]$LogRoot)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$builds=@(Get-Content -LiteralPath (Join-Path $LogRoot 'revision-builds.json') -Raw|ConvertFrom-Json)
if($builds.Count -ne 2 -or $builds[0].Name -ne 'Before' -or $builds[1].Name -ne 'After'){throw 'Invalid revision builds.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($build in $builds){
    if((Get-FileHash -LiteralPath $build.Manifest).Hash -ne $build.ManifestSha256){throw 'Frozen manifest differs.'}
    foreach($library in $build.Libraries){if((Get-FileHash -LiteralPath $library.Path).Hash -ne $library.Sha256){throw 'Runtime library differs.'}}
    foreach($entry in $build.Generated){if((Get-FileHash -LiteralPath $entry.File).Hash -ne $entry.Sha256){throw 'Generated source differs.'}}
    $inputs=Get-Content -LiteralPath $build.Manifest -Raw|ConvertFrom-Json -AsHashtable
    $marker='\Benchmark\Experiments\ProductTransactions\';$offset=$build.Generated[0].File.IndexOf($marker,[StringComparison]::OrdinalIgnoreCase)
    if($offset -lt 0){throw 'Unknown source layout.'};$snapshot=$build.Generated[0].File.Substring(0,$offset)
    foreach($entry in $inputs.GetEnumerator()){
        if((Get-FileHash -LiteralPath (Join-Path $snapshot $entry.Key)).Hash -ne $entry.Value){throw ('Frozen source differs: '+$entry.Key)}
        if($build.Name -eq 'After' -and (Get-FileHash -LiteralPath (Join-Path $workspace $entry.Key)).Hash -ne $entry.Value){throw ('Current candidate differs: '+$entry.Key)}
    }
}
$runs=@(Get-Content -LiteralPath (Join-Path $LogRoot 'revision-runs.json') -Raw|ConvertFrom-Json)
if($runs.Count -ne 32){throw 'Expected 32 interleaved profiles.'}
$identities=[Collections.Generic.HashSet[string]]::new();$evidence=@();$reports=@()
$queries=@('JoinIdentity','JoinMultiple','GroupIdentity','GroupEscapeMultiple')
foreach($run in $runs){
    $dir=$run.Artifact;$spec=Get-Content -LiteralPath (Join-Path $dir 'revision-spec.json') -Raw|ConvertFrom-Json
    $report=Get-Content -LiteralPath (Join-Path $dir 'report.json') -Raw|ConvertFrom-Json
    $receipt=Get-Content -LiteralPath (Join-Path $dir 'receipt.json') -Raw|ConvertFrom-Json
    $build=$builds|Where-Object Name -eq $spec.Revision
    if(-not $build -or $run.Revision -ne $spec.Revision -or $receipt.Outcome -ne 'Passed' -or $receipt.Task.status -ne 'Passed' -or $receipt.Task.exitCode -ne 0 -or $receipt.Task.affinity -ne '0xFF'){throw 'Invalid terminal profile.'}
    if($spec.RunnerSha256 -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Invoke-MvccOrderedRevisionComparison.ps1')).Hash){throw 'Revision runner changed.'}
    foreach($name in @('AssemblySha256','SourceManifestSha256')){if($report.$name -ne $spec.$name){throw 'Spec/report differs.'}}
    if($spec.AssemblySha256 -ne $build.AssemblySha256 -or $spec.SourceManifestSha256 -ne $build.ManifestSha256 -or (Get-FileHash -LiteralPath (Join-Path $dir 'source-manifest.json')).Hash -ne $build.ManifestSha256){throw 'Revision differs.'}
    if($report.Rows -ne $spec.Rows -or $report.StorageMode -ne $spec.Storage -or $report.QueryWorkers -ne $spec.Workers -or $report.Flavor -ne $spec.Flavor -or $report.Rows -notin @(4096,16384) -or $report.StorageMode -notin @('Memory','Storage') -or $report.QueryWorkers -notin @(1,4) -or $report.Flavor -notin @('FLinq','Linq')){throw 'Unexpected profile.'}
    $cell=([Array]::IndexOf(@(4096,16384),[int]$spec.Rows)*4)+([Array]::IndexOf(@('Memory','Storage'),$spec.Storage)*2)+[Array]::IndexOf(@(1,4),[int]$spec.Workers)
    $choices=@('Before/FLinq','After/FLinq','Before/Linq','After/Linq')
    if($spec.Rotation -ne $cell -or $spec.Order -ne ($reports.Count%4) -or $choices[($spec.Order+$cell)%4] -ne ($spec.Revision+'/'+$spec.Flavor)){throw 'Interleaving differs.'}
    if(-not $identities.Add("$($spec.Rows)/$($spec.Storage)/$($spec.Workers)/$($spec.Revision)/$($spec.Flavor)")){throw 'Duplicate profile.'}
    if($report.Format -ne 'product-ordered-comparison-v1' -or $report.Repeats -ne 10 -or $report.Warmups -ne 2 -or $report.Records.Count -ne 40 -or $report.MemoryObservations.Count -ne 4 -or $report.TieredCompilation -ne '0' -or $report.Affinity -ne 255 -or $report.Cpu.Active -ne 0 -or $report.Cpu.Outstanding -ne 0){throw 'Measurement shape differs.'}
    $samples=[Collections.Generic.HashSet[string]]::new()
    foreach($sample in $report.Records){
        $index=[Array]::IndexOf($queries,$sample.Query)
        if($index -lt 0 -or $sample.Repeat -lt 0 -or $sample.Repeat -ge 10 -or ($sample.Order+$sample.Repeat+4)%4 -ne $index -or -not $samples.Add("$($sample.Query)/$($sample.Repeat)")){throw 'Sample/order differs.'}
        foreach($name in @('Milliseconds','AllocatedBytes','PeakAdditionalReservedMemoryBytes','PeakAdditionalReservedStorageBytes')){if(-not [double]::IsFinite($sample.$name) -or $sample.$name -lt 0){throw 'Invalid metric.'}}
    }
    $reports+=@{Revision=$spec.Revision;Report=$report};$evidence+=@{Artifact=$dir;ReportSha256=(Get-FileHash -LiteralPath (Join-Path $dir 'report.json')).Hash;Pid=$receipt.Task.pid;CreationUtc=$receipt.Task.creationUtc}
}
function Median($values){$ordered=@($values|Sort-Object);$middle=[int][math]::Floor($ordered.Count/2);if($ordered.Count%2){return $ordered[$middle]};return ($ordered[$middle-1]+$ordered[$middle])/2.0}
$summary=@()
foreach($rows in @(4096,16384)){foreach($storage in @('Memory','Storage')){foreach($workers in @(1,4)){foreach($query in $queries){
    $item=[ordered]@{Rows=$rows;Storage=$storage;Workers=$workers;Query=$query}
    foreach($revision in @('Before','After')){foreach($flavor in @('FLinq','Linq')){
        $profile=@($reports|Where-Object {$_.Revision -eq $revision -and $_.Report.Rows -eq $rows -and $_.Report.StorageMode -eq $storage -and $_.Report.QueryWorkers -eq $workers -and $_.Report.Flavor -eq $flavor})[0].Report
        $samples=@($profile.Records|Where-Object Query -eq $query);$memory=$profile.MemoryObservations|Where-Object Query -eq $query;$prefix=$revision+$flavor
        $item[$prefix+'Milliseconds']=Median $samples.Milliseconds;$item[$prefix+'AllocatedBytes']=Median $samples.AllocatedBytes
        $item[$prefix+'RetainedBytes']=$memory.ManagedRetainedDeltaBytes;$item[$prefix+'ReservedBytes']=Median $samples.PeakAdditionalReservedMemoryBytes
        $item[$prefix+'SpillBytes']=Median $samples.PeakAdditionalReservedStorageBytes;$item[$prefix+'PeakWorkingSetBytes']=$profile.ProcessPeakWorkingSetBytes
        $item[$prefix+'MinMilliseconds']=($samples.Milliseconds|Measure-Object -Minimum).Minimum;$item[$prefix+'MaxMilliseconds']=($samples.Milliseconds|Measure-Object -Maximum).Maximum
    }}
    $summary+=[pscustomobject]$item
}}}}
$summary|Export-Csv -LiteralPath (Join-Path $LogRoot 'revision-summary.csv') -NoTypeInformation -Encoding utf8
@{Outcome='Passed';Profiles=32;TimedSamples=1280;MemoryObservations=128;AuditorSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;Evidence=$evidence;Summary=$summary}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $LogRoot 'revision-audit.json') -Encoding utf8
$summary|Where-Object Query -eq GroupEscapeMultiple|Select-Object Rows,Storage,Workers,BeforeFLinqMilliseconds,AfterFLinqMilliseconds,BeforeLinqMilliseconds,AfterLinqMilliseconds,BeforeFLinqAllocatedBytes,AfterFLinqAllocatedBytes,AfterLinqAllocatedBytes|Format-Table
