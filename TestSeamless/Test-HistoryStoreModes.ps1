[CmdletBinding()]
param([Parameter(Mandatory)][string]$TaskRoot,[Parameter(Mandatory)][string]$LogRoot)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
function Require([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function Median($values){$s=@($values|Sort-Object);Require ($s.Count -gt 0) 'No samples';$m=[int][Math]::Floor($s.Count/2);if($s.Count%2){return [double]$s[$m]};return ([double]$s[$m-1]+[double]$s[$m])/2}
function ValidNumber($value){return [double]::IsFinite([double]$value) -and [double]$value -ge 0}
$repo=Split-Path $PSScriptRoot
$results=@(Get-Content -LiteralPath (Join-Path $LogRoot 'Release-results.json') -Raw|ConvertFrom-Json)
Require ($results.Count -eq 2) 'Expected verification and comparison'
$sourceHash=$null;$assemblyHash=$null;$report=$null;$proof=@();$docs=@();$inputs=0;$bufferedControl=$null
foreach($result in $results){
    $directory=$result.Result.Task.artifactDirectory
    $receipt=Get-Content -LiteralPath (Join-Path $directory 'receipt.json') -Raw|ConvertFrom-Json
    $spec=Get-Content -LiteralPath (Join-Path $directory 'spec.json') -Raw|ConvertFrom-Json
    Require ($receipt.Outcome -eq 'Passed' -and $receipt.Task.exitCode -eq 0 -and $receipt.Task.id -eq $result.Result.Task.id -and $receipt.Task.affinity -eq '0xFF') 'No successful terminal receipt'
    Require ($spec.Configuration -eq 'Release' -and $spec.Phase -eq $result.Phase) 'Wrong verification/comparison spec'
    if($null -eq $bufferedControl){$bufferedControl=[bool]$spec.BufferedControl};Require ($bufferedControl -eq [bool]$spec.BufferedControl) 'I/O control spec differs'
    $path=Join-Path $directory 'source-manifest.json';$hash=(Get-FileHash -LiteralPath $path).Hash
    Require ($hash -eq $spec.SourceManifestSha256 -and $spec.RunnerSha256 -eq (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Invoke-HistoryStoreModes.ps1')).Hash) 'Manifest or runner differs'
    Require ($spec.Build.Outcome -eq 'Passed' -and $spec.Build.Task.exitCode -eq 0 -and @(Select-String -LiteralPath (Join-Path $spec.Build.Task.artifactDirectory 'stdout.log') -Pattern ': warning ').Count -eq 0) 'Build failed or has warnings'
    if($null -eq $sourceHash){
        $sourceHash=$hash;$assemblyHash=$spec.AssemblySha256
        $manifest=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -AsHashtable
        foreach($relative in $manifest.Keys){
            $frozen=Join-Path (Join-Path $TaskRoot 'source') $relative;Require ((Get-FileHash -LiteralPath $frozen).Hash -eq $manifest[$relative]) "Frozen source changed: $relative"
            $current=Join-Path $repo $relative
            if((Get-FileHash -LiteralPath $current).Hash -ne $manifest[$relative]){Require ([IO.Path]::GetExtension($relative) -eq '.md') "Current executable source differs: $relative";$docs+=$relative}
            $inputs++
        }
    }
    Require ($hash -eq $sourceHash -and $spec.AssemblySha256 -eq $assemblyHash) 'Verification and comparison used different builds'
    $data=Get-Content -LiteralPath (Join-Path $directory 'report.json') -Raw|ConvertFrom-Json
    Require ($data.SourceManifestSha256 -eq $sourceHash) 'Report input differs'
    if($result.Phase -eq 'verify'){
        Require ($data.Outcome -eq 'Passed' -and $data.Checks.Count -eq 39) 'Missing prototype verification checks'
        foreach($name in @('checksum rejects changed payload','snapshot refuses truncated page','checkpoint pages stay immutable across later append','committed checkpoint survives deferred file cleanup')){Require ($name -in $data.Checks) "Required check missing: $name"}
    }elseif($result.Phase -eq 'run'){$report=$data}
    else{throw 'Unexpected phase'}
    $proof+=@{Phase=$result.Phase;Directory=$directory;Pid=$receipt.Task.pid}
}
$expectedRecords=600;$storages=@('Memory','ResidentStorage','SmallCacheStorage');$ioMode='NativeDefault_ArenaBuffered'
if($bufferedControl){$expectedRecords=400;$storages=@('ResidentStorage','SmallCacheStorage');$ioMode='BufferedRandomAccess'}
Require ($null -ne $report -and $report.Format -eq 'history-store-modes-v1' -and $report.IoMode -eq $ioMode -and $report.Records.Count -eq $expectedRecords -and $report.Affinity -eq 255 -and $report.ProcessorCount -eq 8 -and $report.Repeats -eq 5 -and $report.Keys -eq 1024 -and $report.ReadSamples -eq 128 -and $report.ParentMemoryLimit -eq 64MB -and $report.ArenaLogicalLimit -eq 8MB) 'Wrong comparison matrix/profile'
$binary=Join-Path $TaskRoot 'source/Benchmark/Experiments/HistoryStoreModes/bin/Release/net10.0'
Require ((Get-FileHash -LiteralPath (Join-Path $binary 'HistoryStoreModes.dll')).Hash -eq $assemblyHash -and $report.AssemblySha256 -eq $assemblyHash) 'Executable changed'
Require ((Get-FileHash -LiteralPath (Join-Path $binary 'Tsavorite.core.dll')).Hash -eq $report.TsavoriteSha256) 'Tsavorite changed'
$unique=[Collections.Generic.HashSet[string]]::new();$runPid=($proof|Where-Object Phase -eq 'run').Pid
foreach($record in $report.Records){
    Require ($record.KeyBytes -in @(8,32) -and $record.RowBytes -in @(32,512) -and $record.Versions -in @(1,8,64) -and ($record.RowBytes -ne 512 -or $record.Versions -ne 64) -and $record.Storage -in $storages -and $record.IndexBytes -in @(64,32768) -and $record.Dedicated -is [bool] -and $record.Repeat -in 0..4 -and $record.Order -in @(0,1)) 'Invalid condition'
    $mode=0;if($record.Dedicated){$mode=1}
    Require (($record.Order+$record.Repeat)%2 -eq $mode) 'Mode order did not rotate'
    $logBytes=8MB;if($record.Storage -eq 'SmallCacheStorage'){$logBytes=64KB};Require ($record.LogBytes -eq $logBytes) 'Log/cache profile differs'
    Require ($unique.Add("$($record.KeyBytes)/$($record.RowBytes)/$($record.Versions)/$($record.Storage)/$($record.IndexBytes)/$($record.Dedicated)/$($record.Repeat)")) 'Duplicate sample'
    $r=$record.Result;Require ($r.AppendedVersions -eq 1024*$record.Versions) 'Wrong append count'
    foreach($field in @('AppendMilliseconds','AppendAllocatedBytes','SaveMilliseconds','ReopenMilliseconds','ReclaimMilliseconds')){Require (ValidNumber $r.$field) "Invalid measurement: $field"}
    foreach($temperature in @('WarmLatest','WarmOld','EvictedOld')){
        $sample=$r.$temperature
        if($temperature -eq 'EvictedOld' -and $record.Storage -eq 'Memory'){Require ($null -eq $sample) 'Memory data cannot be evicted';continue}
        $version=1;if($temperature -eq 'WarmLatest'){$version=$record.Versions};[long]$expected=0
        for($index=0;$index -lt 128;$index++){$id=($index*761+13)%1024;$expected+=(($id*31+$version*17+16)%256)+$version}
        Require ($sample.Count -eq 128 -and $sample.Version -eq $version -and $sample.Checksum -eq $expected) 'Read differs from independent expected checksum'
        foreach($field in @('Milliseconds','AllocatedBytes','P50','P95','P99')){Require (ValidNumber $sample.$field) 'Invalid read metric'}
        Require ($sample.P50 -le $sample.P95 -and $sample.P95 -le $sample.P99) 'Invalid latency order'
    }
    foreach($phase in @('Before','After')){
        $s=$r.$phase;$known=$s.NativeIndexPayloadBytes+$s.NativeLogAllocatedBytes
        if($record.Dedicated){$known+=$s.Arena.BufferBytes;Require ($s.Arena.BufferBytes -le $logBytes -and $s.Arena.Tail -le 8MB) 'Arena exceeded bound'}
        Require ($s.Native.IndexBytes -eq $record.IndexBytes -and $s.NativeIndexPayloadBytes -ge $record.IndexBytes+8MB -and $s.Budget.MemoryBytes -ge $known -and $s.Budget.PeakMemoryBytes -le 64MB -and $s.Budget.PeakTemporaryStorageBytes -le 256MB) 'Index/payload/reservation invariant failed'
    }
    if($record.Dedicated){Require ($r.After.Arena.Generation -eq $r.Before.Arena.Generation+1 -and $r.After.Arena.Tail -le $r.Before.Arena.Tail) 'Reclamation did not switch/shrink arena'}
    if($record.Storage -eq 'Memory'){Require ($r.FootprintBefore.TotalBytes -eq 0 -and $r.FootprintAfter.TotalBytes -eq 0) 'Memory backend wrote a dataset'}
    Require ($r.Resources.Status -eq 'Available' -and $r.Resources.Pid -eq $runPid -and $r.Resources.WorkingSetObservedPeak -gt 0 -and $r.Resources.PrivateBytesObservedPeak -gt 0) 'OS process observation unavailable or wrong process'
}
$summary=@($report.Records|Group-Object {$_.Storage,$_.KeyBytes,$_.RowBytes,$_.Versions,$_.IndexBytes,$_.Dedicated -join '/'}|ForEach-Object{
    $first=$_.Group[0];$r=$_.Group.Result
    # Storageの再openはownerを作り直す。回収中のピークも含め、各caseの両ownerの最大値を取ってから中央値を求める。
    $ownerPeaks=@($r|ForEach-Object{[Math]::Max($_.Before.Budget.PeakMemoryBytes,$_.After.Budget.PeakMemoryBytes)})
    [pscustomobject]@{Storage=$first.Storage;KeyBytes=$first.KeyBytes;RowBytes=$first.RowBytes;Versions=$first.Versions;IndexBytes=$first.IndexBytes;Dedicated=$first.Dedicated;Samples=$_.Count;AppendMilliseconds=Median $r.AppendMilliseconds;AppendAllocatedBytes=Median $r.AppendAllocatedBytes;LatestBatchP50=Median $r.WarmLatest.P50;OldBatchP50=Median $r.WarmOld.P50;OldBatchP95=Median $r.WarmOld.P95;SaveMilliseconds=Median $r.SaveMilliseconds;ReopenMilliseconds=Median $r.ReopenMilliseconds;ReclaimMilliseconds=Median $r.ReclaimMilliseconds;BeforeReservedMemory=Median $r.Before.Budget.MemoryBytes;BeforeOwnerPeakMemory=Median $r.Before.Budget.PeakMemoryBytes;AfterOwnerPeakMemory=Median $r.After.Budget.PeakMemoryBytes;OwnerPeakMemory=Median $ownerPeaks;BeforeNativeIndexPayload=Median $r.Before.NativeIndexPayloadBytes;BeforeNativeLogAllocated=Median $r.Before.NativeLogAllocatedBytes;BeforeFiles=Median $r.FootprintBefore.TotalBytes;AfterFiles=Median $r.FootprintAfter.TotalBytes;ProcessWriteTransferBytes=Median $r.Resources.WriteTransferBytes;ProcessReadTransferBytes=Median $r.Resources.ReadTransferBytes}
})
Require ($summary.Count -eq $expectedRecords/5 -and @($summary|Where-Object Samples -ne 5).Count -eq 0) 'Incomplete repetitions'
$output=Join-Path $LogRoot 'history-comparison-audit.json'
@{Outcome='Passed';Format='history-store-modes-audit-v1';AuditorSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;IoMode=$ioMode;SourceManifestSha256=$sourceHash;AssemblySha256=$assemblyHash;CheckedCurrentInputs=$inputs;DocumentationChanges=$docs;Samples=$report.Records.Count;Evidence=$proof;Summary=$summary;Limitations=@('Sequential backend prototype, not common WAL transactions','Arena adds a separate cache beside the native head log; reservation includes both','OwnerPeakMemory takes the per-case maximum before/after reopen, including reclamation','Read after one application eviction retains OS cache','Medians of 128-request batch percentiles across five repetitions','OS process transfers and observed process memory are not physical disk I/O or isolated backend RSS','Buffered file cache is outside the owner and process private memory counters')}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $output
[pscustomobject]@{Outcome='Passed';Samples=$report.Records.Count;Conditions=$summary.Count;CheckedCurrentInputs=$inputs;Output=$output}
