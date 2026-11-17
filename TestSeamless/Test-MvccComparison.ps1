[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$MatrixFile,
    [Parameter(Mandatory)][string]$OutputFile,
    [string]$RepositoryRoot=(Split-Path $PSScriptRoot),
    [switch]$FrozenInputs
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
Set-StrictMode -Version Latest
function Require([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function Median($values){
    $sorted=@($values|Sort-Object);Require ($sorted.Count -gt 0) 'Empty sample set'
    $middle=[int][Math]::Floor($sorted.Count/2)
    if($sorted.Count%2){return [double]$sorted[$middle]}
    return ([double]$sorted[$middle-1]+[double]$sorted[$middle])/2
}
function FiniteNonnegative($value){return [double]::IsFinite([double]$value) -and [double]$value -ge 0}
function FileFootprint([string]$path){
    $files=@();if(Test-Path -LiteralPath $path){$files=@(Get-ChildItem -LiteralPath $path -Recurse -File)}
    [long]$bytes=0;foreach($file in $files){$bytes+=$file.Length}
    return [pscustomobject]@{Files=$files.Count;LogicalBytes=$bytes}
}
Require ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputFile)) -eq 'F:\') 'Use F for the audit output'
$matrix=@(Get-Content -LiteralPath $MatrixFile -Raw|ConvertFrom-Json)
Require ($matrix.Count -eq 4) 'Expected the four 10000/100000 x Memory/Storage cells'
$keys=[Collections.Generic.HashSet[string]]::new()
$allQueries=[Collections.Generic.List[object]]::new();$allMixed=[Collections.Generic.List[object]]::new();$resources=[Collections.Generic.List[object]]::new()
$sourceHash=$null;$assemblyHash=$null;$tsavoriteHash=$null;$runnerHash=$null;$checkedInputs=0
$documentationChanges=[Collections.Generic.HashSet[string]]::new();$evidence=[Collections.Generic.List[object]]::new()
$executableChanges=[Collections.Generic.List[object]]::new()
$cache=Get-Content -LiteralPath (Join-Path (Split-Path $MatrixFile) 'comparison-build.json') -Raw|ConvertFrom-Json
$binaryDirectory=Join-Path $cache.TaskRoot 'source/Benchmark/Experiments/bin/ProductTransactions/Release/net10.0'
Require ($cache.Build.Outcome -eq 'Passed' -and $cache.Build.Task.exitCode -eq 0) 'No successful comparison build'
Require (@(Select-String -LiteralPath (Join-Path $cache.Build.Task.artifactDirectory 'stdout.log') -Pattern ': warning ').Count -eq 0) 'Comparison build has warnings'
foreach($cell in $matrix){
    Require ($cell.Rows -in @(10000,100000) -and $cell.Storage -in @('Memory','Storage')) 'Unexpected matrix condition'
    Require ($keys.Add("$($cell.Rows)/$($cell.Storage)")) 'Duplicate matrix condition'
    $directory=$cell.Result.Task.artifactDirectory
    $receipt=Get-Content -LiteralPath (Join-Path $directory 'receipt.json') -Raw|ConvertFrom-Json
    Require ($cell.Result.Outcome -eq 'Passed' -and $receipt.Outcome -eq 'Passed' -and $receipt.Task.exitCode -eq 0 -and $receipt.Task.status -eq 'Passed') "No successful terminal receipt: $directory"
    Require ($receipt.Task.id -eq $cell.Result.Task.id -and $receipt.Task.artifactDirectory -eq $directory -and $receipt.Task.affinity -eq '0xFF') 'Receipt identity or affinity differs'
    $report=Get-Content -LiteralPath (Join-Path $directory 'report.json') -Raw|ConvertFrom-Json
    $spec=Get-Content -LiteralPath (Join-Path $directory 'comparison-spec.json') -Raw|ConvertFrom-Json
    Require ($report.Format -eq 'product-mvcc-comparison-v1' -and $report.Stage -eq 'all' -and $report.Rows -eq $cell.Rows -and $report.StorageMode -eq $cell.Storage) 'Report condition differs'
    Require ($spec.Rows -eq $report.Rows -and $spec.StorageMode -eq $report.StorageMode -and $spec.Stage -eq $report.Stage -and $spec.OperationsPerWorker -eq $report.OperationsPerWorker -and $spec.Repeats -eq 5) 'Specification differs'
    Require ($report.Repeats -eq 5 -and $report.OperationsPerWorker -eq 64 -and $report.Affinity -eq 255 -and $report.ProcessorCount -eq 8 -and $report.CpuCapacity -eq 8) 'Unexpected repetition, operations or CPU profile'
    Require ($report.IndexBytes -eq 2MB -and $report.LogLimitBytes -eq 32MB -and $report.HistoryLimitBytes -eq 24MB -and $report.OwnerBudgetBytes -eq 128MB) 'Resource profile differs'
    Require ($report.QueryWorkerLimitIsCap -and $report.TsavoriteVersion -eq '2.2.5.0') 'Unexpected worker scope or package'
    $manifestFile=Join-Path $directory 'source-manifest.json'
    $manifestHash=(Get-FileHash -LiteralPath $manifestFile).Hash
    Require ($manifestHash -eq $report.SourceManifestSha256 -and $manifestHash -eq $spec.SourceManifestSha256) 'Source manifest hash differs'
    if($null -eq $sourceHash){$sourceHash=$manifestHash;$assemblyHash=$report.AssemblySha256;$tsavoriteHash=$report.TsavoriteSha256;$runnerHash=$spec.RunnerSha256
        $manifest=Get-Content -LiteralPath $manifestFile -Raw|ConvertFrom-Json -AsHashtable
        foreach($relative in $manifest.Keys){
            $frozen=Join-Path (Join-Path $cache.TaskRoot 'source') $relative
            Require ((Get-FileHash -LiteralPath $frozen).Hash -eq $manifest[$relative]) "Frozen input changed: $relative"
            $current=[IO.Path]::GetFullPath((Join-Path $RepositoryRoot $relative))
            Require ($current.StartsWith([IO.Path]::GetFullPath($RepositoryRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Source path escapes repository'
            Require (Test-Path -LiteralPath $current -PathType Leaf) "Current input missing: $relative"
            if((Get-FileHash -LiteralPath $current).Hash -ne $manifest[$relative]){
                if([IO.Path]::GetExtension($relative) -eq '.md'){[void]$documentationChanges.Add($relative)}
                else{
                    Require ([bool]$FrozenInputs) "Executable input changed: $relative; use -FrozenInputs only to audit the recorded snapshot, not the current tree"
                    $executableChanges.Add([pscustomobject]@{File=$relative;MeasuredSha256=$manifest[$relative];CurrentSha256=(Get-FileHash -LiteralPath $current).Hash})
                }
            }
            $checkedInputs++
        }
    }
    Require ($sourceHash -eq $manifestHash -and $assemblyHash -eq $report.AssemblySha256 -and $tsavoriteHash -eq $report.TsavoriteSha256 -and $runnerHash -eq $spec.RunnerSha256) 'Matrix was not measured with one frozen build'
    Require ($cache.AssemblySha256 -eq $assemblyHash -and $cache.RunnerSha256 -eq $runnerHash -and $spec.BuildArtifact -eq $cache.Build.Task.artifactDirectory) 'Build identity differs'
    Require ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Invoke-MvccComparison.ps1')).Hash -eq $runnerHash) 'Runner changed'
    foreach($generated in @(Get-Content -LiteralPath (Join-Path $directory 'generated-manifest.json') -Raw|ConvertFrom-Json)){
        Require ((Get-FileHash -LiteralPath $generated.File).Hash -eq $generated.Sha256) 'Generated source changed'
    }
    # Independent loop, without calling the benchmark's LINQ or Fold implementation.
    [long[]]$expected=0,$report.Rows,0,0;[long[]]$counts=New-Object long[] 64;[long[]]$totals=New-Object long[] 64
    for($id=0;$id -lt $report.Rows;$id++){
        [long]$balance=100+$id%17
        if($id%2 -eq 0){$expected[0]+=$balance}
        $bucket=$id%64;$counts[$bucket]++;$totals[$bucket]+=$balance;$expected[3]+=100+$bucket%17+$balance
    }
    for($bucket=0;$bucket -lt 64;$bucket++){$expected[2]+=($bucket+1)*$totals[$bucket]+17*$counts[$bucket]}
    $depths=@(1);if($report.Rows -eq 10000){$depths=@(1,8)}
    $queryKeys=[Collections.Generic.HashSet[string]]::new();$mixedKeys=[Collections.Generic.HashSet[string]]::new();$resourceKeys=[Collections.Generic.HashSet[string]]::new()
    foreach($record in $report.Records){
        switch($record.Kind){
            'query'{
                $queryIndex=[Array]::IndexOf(@('Sum','CountWhere','GroupBy','Join'),$record.Query)
                $flavors=@('NativeFLinq','NativeLinq','TransactionFLinq','TransactionLinq');if($record.TransactionVersionDepth -eq 1){$flavors+=@('TransactionFLinqEndToEnd','TransactionLinqEndToEnd')}
                Require ($queryIndex -ge 0 -and $record.Flavor -in $flavors -and $record.TransactionVersionDepth -in $depths -and $record.QueryWorkerLimit -in @(1,2,4,8) -and $record.Repeat -in 0..4) 'Unexpected query condition'
                Require ($queryKeys.Add("$($record.Query)/$($record.Flavor)/$($record.TransactionVersionDepth)/$($record.QueryWorkerLimit)/$($record.Repeat)")) 'Duplicate query sample'
                $flavorIndex=[Array]::IndexOf($flavors,$record.Flavor)
                Require ($record.Order -ge 0 -and $record.Order -lt $flavors.Count -and ($record.Order+$record.Repeat)%$flavors.Count -eq $flavorIndex) 'Query order did not rotate'
                Require ($record.Value -eq $expected[$queryIndex] -and $record.Expected -eq $expected[$queryIndex]) 'Query disagrees with independent expected value'
                Require ((FiniteNonnegative $record.Milliseconds) -and $record.Milliseconds -gt 0 -and (FiniteNonnegative $record.AllocatedBytes)) 'Invalid query measurement'
                $allQueries.Add([pscustomobject]@{Rows=$report.Rows;Storage=$report.StorageMode;Sample=$record})
            }
            'mixed'{
                Require ($record.Mode -in @('Exclusive','Optimistic') -and $record.Concurrency -in @(1,2,4,8) -and $record.ReadPercent -in @(100,95,50) -and $record.Repeat -in 0..4 -and $record.Hot -is [bool] -and $record.QueryWorkerLimit -eq 1) 'Unexpected mixed condition'
                Require ($mixedKeys.Add("$($record.Mode)/$($record.Concurrency)/$($record.ReadPercent)/$($record.Hot)/$($record.Repeat)")) 'Duplicate mixed sample'
                $modeIndex=[Array]::IndexOf(@('Exclusive','Optimistic'),$record.Mode)
                Require ($record.Order -in @(0,1) -and ($record.Order+$record.Repeat)%2 -eq $modeIndex) 'Mixed order did not rotate'
                Require ($record.Attempts -eq $record.Concurrency*$report.OperationsPerWorker -and $record.Committed+$record.Conflicts -eq $record.Attempts -and $record.Committed -gt 0 -and $record.Updates -ge 0 -and $record.Updates -le $record.Committed -and $record.Conflicts -ge 0) 'Invalid mixed outcomes'
                if($record.ReadPercent -eq 100){Require ($record.Updates -eq 0) 'Read-only workload wrote rows'}
                Require ($record.PeakConcurrentBodies -ge 1 -and $record.PeakConcurrentBodies -le $record.Concurrency) 'Invalid body overlap'
                if($record.Mode -eq 'Exclusive'){Require ($record.Conflicts -eq 0 -and $record.PeakConcurrentBodies -eq 1) 'Exclusive invariant failed'}
                foreach($field in @('Milliseconds','Throughput','P50','P95','P99','SuccessP95','AllocatedBytes')){Require (FiniteNonnegative $record.$field) "Invalid mixed metric: $field"}
                Require ($record.Milliseconds -gt 0 -and $record.P50 -le $record.P95 -and $record.P95 -le $record.P99) 'Invalid latency ordering'
                Require ([Math]::Abs($record.Throughput-$record.Committed*1000/$record.Milliseconds) -lt .000001*[Math]::Max(1,$record.Throughput)) 'Throughput does not count successful commits'
                $allMixed.Add([pscustomobject]@{Rows=$report.Rows;Storage=$report.StorageMode;Sample=$record})
            }
            'query-resources'{
                Require ($record.TransactionVersionDepth -in $depths -and $record.QueryWorkerLimit -in @(1,2,4,8) -and $resourceKeys.Add("$($record.TransactionVersionDepth)/$($record.QueryWorkerLimit)")) 'Duplicate or invalid resource condition'
                Require ($record.Usage.MemoryLimitBytes -eq $report.OwnerBudgetBytes -and $record.Usage.PeakMemoryBytes -le $report.OwnerBudgetBytes -and $record.Usage.PeakTemporaryStorageBytes -le $record.Usage.TemporaryStorageLimitBytes) 'Database reservation exceeded budget'
                Require ($record.NativeUsage.PeakMemoryBytes -le $report.OwnerBudgetBytes -and $record.Native.MemoryLogBytes -le $report.LogLimitBytes -and $record.Native.Count -eq $report.Rows -and $record.Native.IndexBytes -eq $report.IndexBytes) 'Native resource profile differs'
                Require ($record.Domain.ActiveOperations -eq 0 -and -not $record.Domain.Reclaiming -and $record.Domain.HistoryMemoryBytes -le $report.HistoryLimitBytes -and $record.Domain.HistoryStorageBytes -le $report.HistoryLimitBytes) 'History or operation invariant failed'
                $expectedReaders=0;if($record.TransactionVersionDepth -eq 8){$expectedReaders=1;Require ($record.Domain.HistoryMemoryBytes+$record.Domain.HistoryStorageBytes -gt 0) 'Old-snapshot profile retained no history'}
                Require ($record.Domain.ActiveTransactions -eq $expectedReaders -and $record.Cpu.Active -eq 0 -and $record.Cpu.Outstanding -eq 0 -and $record.Cpu.Waiting -eq 0 -and $record.Cpu.PeakActive -le 8) 'Unreleased transaction/CPU lease'
                Require ([IO.Path]::GetPathRoot($spec.DataRoot) -eq 'F:\') 'Expected D data root'
                $queryRoot=Join-Path $spec.DataRoot ("query-$($record.TransactionVersionDepth)-$($record.QueryWorkerLimit)")
                $afterRun=[pscustomobject]@{Native=FileFootprint (Join-Path $spec.DataRoot 'native');DatabaseStore=FileFootprint (Join-Path $queryRoot 'database/store-mvcc-v1');DatabaseWal=FileFootprint (Join-Path $queryRoot 'database/wal-mvcc-v1');PrivateTemporary=FileFootprint (Join-Path $queryRoot 'temporary')}
                $resources.Add([pscustomobject]@{Rows=$report.Rows;Storage=$report.StorageMode;Sample=$record;FilesAfterRun=$afterRun})
            }
            default{throw "Unexpected record kind: $($record.Kind)"}
        }
    }
    $expectedQueries=480;if($report.Rows -eq 10000){$expectedQueries=800}
    Require ($queryKeys.Count -eq $expectedQueries -and $mixedKeys.Count -eq 240 -and $resourceKeys.Count -eq $depths.Count*4) 'Incomplete Cartesian coverage'
    $evidence.Add([pscustomobject]@{Rows=$report.Rows;Storage=$report.StorageMode;Directory=$directory;Queries=$queryKeys.Count;Mixed=$mixedKeys.Count;Resources=$resourceKeys.Count})
}
Require ((Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'ProductTransactions.dll')).Hash -eq $assemblyHash) 'Actual comparison assembly differs'
Require ((Get-FileHash -LiteralPath (Join-Path $binaryDirectory 'Tsavorite.core.dll')).Hash -eq $tsavoriteHash) 'Actual Tsavorite assembly differs'
$querySummary=@($allQueries|Group-Object {$_.Rows,$_.Storage,$_.Sample.TransactionVersionDepth,$_.Sample.QueryWorkerLimit,$_.Sample.Query,$_.Sample.Flavor -join '/'}|ForEach-Object{
    $first=$_.Group[0];$sample=$first.Sample
    [pscustomobject]@{Rows=$first.Rows;Storage=$first.Storage;VersionDepth=$sample.TransactionVersionDepth;TransactionQueryWorkerLimit=$sample.QueryWorkerLimit;Query=$sample.Query;Flavor=$sample.Flavor;MedianMilliseconds=Median $_.Group.Sample.Milliseconds;MedianManagedAllocatedBytes=Median $_.Group.Sample.AllocatedBytes;Samples=$_.Count}
})
$mixedSummary=@($allMixed|Group-Object {$_.Rows,$_.Storage,$_.Sample.Mode,$_.Sample.Concurrency,$_.Sample.ReadPercent,$_.Sample.Hot -join '/'}|ForEach-Object{
    $first=$_.Group[0];$sample=$first.Sample;$attempts=($_.Group.Sample.Attempts|Measure-Object -Sum).Sum;$conflicts=($_.Group.Sample.Conflicts|Measure-Object -Sum).Sum
    [pscustomobject]@{Rows=$first.Rows;Storage=$first.Storage;Mode=$sample.Mode;ConcurrentTransactions=$sample.Concurrency;ReadPercent=$sample.ReadPercent;Hot=$sample.Hot;MedianSuccessfulTransactionsPerSecond=Median $_.Group.Sample.Throughput;MedianP95Milliseconds=Median $_.Group.Sample.P95;MedianP99Milliseconds=Median $_.Group.Sample.P99;ConflictRate=$conflicts/$attempts;PeakConcurrentBodies=($_.Group.Sample.PeakConcurrentBodies|Measure-Object -Maximum).Maximum;MedianManagedAllocatedBytes=Median $_.Group.Sample.AllocatedBytes;Samples=$_.Count}
})
$summary=[pscustomobject]@{Outcome='Passed';Format='product-mvcc-comparison-audit-v1';Utc=[DateTime]::UtcNow.ToString('O');SourceManifestSha256=$sourceHash;AssemblySha256=$assemblyHash;TsavoriteSha256=$tsavoriteHash;RunnerSha256=$runnerHash;CheckedFrozenInputs=$checkedInputs;CurrentExecutableInputsMatch=($executableChanges.Count -eq 0);ExecutableInputChanges=$executableChanges;DocumentationChanges=@($documentationChanges);Evidence=$evidence;Queries=$querySummary;Mixed=$mixedSummary;Resources=$resources;Limitations=@('Warm resident data; no cold-disk comparison','Worker setting is a cap for transaction queries; native uses its existing execution policy','Five query repetitions describe these samples, not a stable tail distribution','Managed allocation, owner reservations and cumulative process peak RSS are separate metrics','FilesAfterRun is post-disposal logical file length, not timed peak or physical disk allocation','Mixed throughput includes harness costs and has no automatic business retries')}
$summary|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $OutputFile -Encoding utf8
[pscustomobject]@{Outcome=$summary.Outcome;MatrixCells=$evidence.Count;QuerySamples=$allQueries.Count;MixedSamples=$allMixed.Count;ResourceSamples=$resources.Count;CheckedFrozenInputs=$checkedInputs;CurrentExecutableInputsMatch=$summary.CurrentExecutableInputsMatch;OutputFile=$OutputFile}
