[CmdletBinding()]
param([Parameter(Mandatory)][string]$LogRoot)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runs=@(Get-Content -LiteralPath (Join-Path $LogRoot 'ordered-runs-Release.json') -Raw|ConvertFrom-Json)
if($runs.Count -ne 16){throw 'Expected 16 isolated Release profiles.'}
$reports=@();$identities=[Collections.Generic.HashSet[string]]::new();$inputHash=$null;$inputs=$null;$evidence=@()
foreach($run in $runs){
    $dir=$run.Artifact;$spec=Get-Content -LiteralPath (Join-Path $dir 'ordered-spec.json') -Raw|ConvertFrom-Json
    $receipt=Get-Content -LiteralPath (Join-Path $dir 'receipt.json') -Raw|ConvertFrom-Json
    $report=Get-Content -LiteralPath (Join-Path $dir 'report.json') -Raw|ConvertFrom-Json
    if($receipt.Outcome -ne 'Passed' -or $receipt.Task.status -ne 'Passed' -or $receipt.Task.exitCode -ne 0 -or $receipt.Task.affinity -ne '0xFF'){throw ('Nonterminal/failed profile: '+$dir)}
    if($spec.Smoke -or $spec.Configuration -ne 'Release' -or $spec.Repeats -ne 10 -or $report.Format -ne 'product-ordered-comparison-v1' -or $report.Records.Count -ne 40 -or $report.MemoryObservations.Count -ne 4 -or $report.TieredCompilation -ne '0' -or $report.Affinity -ne 255){throw 'Invalid comparison shape.'}
    foreach($name in @('Rows','StorageMode','QueryWorkers','Flavor','Repeats','AssemblySha256','SourceManifestSha256')){if($spec.$name -ne $report.$name){throw ('Spec/report mismatch: '+$name)}}
    if($spec.Rows -notin @(4096,16384) -or $spec.StorageMode -notin @('Memory','Storage') -or $spec.QueryWorkers -notin @(1,4) -or $spec.Flavor -notin @('FLinq','Linq')){throw 'Unexpected profile.'}
    if(-not $identities.Add("$($spec.Rows)/$($spec.StorageMode)/$($spec.QueryWorkers)/$($spec.Flavor)")){throw 'Duplicate profile.'}
    $manifest=Join-Path $dir 'source-manifest.json';$hash=(Get-FileHash -LiteralPath $manifest).Hash
    if($hash -ne $spec.SourceManifestSha256 -or ($inputHash -and $hash -ne $inputHash)){throw 'Comparison inputs differ.'};$inputHash=$hash
    if($null -eq $inputs){$inputs=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json -AsHashtable}
    $buildSpec=Get-Content -LiteralPath (Join-Path $spec.BuildArtifact 'validation-spec.json') -Raw|ConvertFrom-Json
    if(-not $buildSpec.ForceCompile -or $buildSpec.RunnerSha256 -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1')).Hash -or $spec.RunnerSha256 -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Invoke-MvccOrderedComparison.ps1')).Hash){throw 'Missing current forced-build provenance.'}
    if(Select-String -LiteralPath (Join-Path $spec.BuildArtifact 'stdout.log') -Pattern '\bwarning [A-Z]+[0-9]+:'){throw 'Build warning.'}
    $samples=[Collections.Generic.HashSet[string]]::new();$queries=@('JoinIdentity','JoinMultiple','GroupIdentity','GroupEscapeMultiple')
    foreach($sample in $report.Records){
        $index=[Array]::IndexOf($queries,$sample.Query)
        if($index -lt 0 -or $sample.Repeat -lt 0 -or $sample.Repeat -ge 10 -or ($sample.Order+$sample.Repeat+4)%4 -ne $index -or -not $samples.Add("$($sample.Query)/$($sample.Repeat)")){throw 'Invalid sample/order.'}
        foreach($name in @('Milliseconds','AllocatedBytes','PeakAdditionalReservedMemoryBytes','PeakAdditionalReservedStorageBytes')){if(-not [double]::IsFinite($sample.$name) -or $sample.$name -lt 0){throw ('Invalid metric: '+$name)}}
    }
    if($report.Cpu.Active -ne 0 -or $report.Cpu.Outstanding -ne 0 -or $report.OwnerUsage.MemoryBytes -gt $report.OwnerUsage.MemoryLimitBytes -or $report.OwnerUsage.TemporaryStorageBytes -gt $report.OwnerUsage.TemporaryStorageLimitBytes){throw 'Resource/CPU mismatch.'}
    $reports+=$report;$evidence+=@{Artifact=$dir;ReportSha256=(Get-FileHash -LiteralPath (Join-Path $dir 'report.json')).Hash;Pid=$receipt.Task.pid;CreationUtc=$receipt.Task.creationUtc}
}
foreach($entry in $inputs.GetEnumerator()){if((Get-FileHash -LiteralPath (Join-Path $workspace $entry.Key)).Hash -ne $entry.Value){throw ('Changed current input: '+$entry.Key)}}
$owners=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($relative in $inputs.Keys){if([IO.Path]::GetExtension($relative) -eq '.csproj'){[void]$owners.Add((Split-Path (Join-Path $workspace $relative)))}}
[void]$owners.Add((Join-Path $workspace 'Shared'))
foreach($directory in $owners){foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File){
    $relative=[IO.Path]::GetRelativePath($directory,$file.FullName)
    if($relative -match '(^|[\\/])(bin|obj|Artifacts|\.git)([\\/]|$)'){continue}
    if($relative -match '^Work[\\/]' -and (Split-Path $directory -Leaf) -ne 'Seamless.Collections.Persisted'){continue}
    if(-not $inputs.ContainsKey([IO.Path]::GetRelativePath($workspace,$file.FullName))){throw ('Unrecorded current source: '+$file.FullName)}
}}
$generated=@(Get-Content -LiteralPath (Join-Path $LogRoot 'generated-Release.json') -Raw|ConvertFrom-Json)
foreach($entry in $generated){if((Get-FileHash -LiteralPath $entry.File).Hash -ne $entry.Sha256){throw 'Generated execution proof changed.'}}
$marker='\obj\Release\generated\';$offset=$generated[0].File.IndexOf($marker,[StringComparison]::OrdinalIgnoreCase)
if($offset -lt 0){throw 'Cannot resolve the frozen benchmark assembly.'}
$dll=Join-Path $generated[0].File.Substring(0,$offset) 'bin/Release/net10.0/ProductTransactions.dll'
if(@($reports|Where-Object AssemblySha256 -ne (Get-FileHash -LiteralPath $dll).Hash).Count){throw 'Frozen benchmark assembly differs from executed reports.'}
function Median($values){$ordered=@($values|Sort-Object);$middle=[int][math]::Floor($ordered.Count/2);if($ordered.Count%2){return $ordered[$middle]};return ($ordered[$middle-1]+$ordered[$middle])/2.0}
$summary=@()
foreach($count in @(4096,16384)){foreach($storage in @('Memory','Storage')){foreach($workers in @(1,4)){foreach($query in @('JoinIdentity','JoinMultiple','GroupIdentity','GroupEscapeMultiple')){
    $pair=@($reports|Where-Object {$_.Rows -eq $count -and $_.StorageMode -eq $storage -and $_.QueryWorkers -eq $workers})
    $f=$pair|Where-Object Flavor -eq FLinq;$l=$pair|Where-Object Flavor -eq Linq
    $fs=@($f.Records|Where-Object Query -eq $query);$ls=@($l.Records|Where-Object Query -eq $query)
    $fm=$f.MemoryObservations|Where-Object Query -eq $query;$lm=$l.MemoryObservations|Where-Object Query -eq $query
    $ft=Median $fs.Milliseconds;$lt=Median $ls.Milliseconds
    $summary+=@{Rows=$count;Storage=$storage;Workers=$workers;Query=$query;FLinqMilliseconds=$ft;LinqMilliseconds=$lt;TimeRatioFLinqOverLinq=$ft/$lt;FLinqAllocatedBytes=(Median $fs.AllocatedBytes);LinqAllocatedBytes=(Median $ls.AllocatedBytes);FLinqReservedBytes=(Median $fs.PeakAdditionalReservedMemoryBytes);LinqReservedBytes=(Median $ls.PeakAdditionalReservedMemoryBytes);FLinqRetainedDeltaBytes=$fm.ManagedRetainedDeltaBytes;LinqRetainedDeltaBytes=$lm.ManagedRetainedDeltaBytes;FLinqMinMilliseconds=($fs.Milliseconds|Measure-Object -Minimum).Minimum;FLinqMaxMilliseconds=($fs.Milliseconds|Measure-Object -Maximum).Maximum;LinqMinMilliseconds=($ls.Milliseconds|Measure-Object -Minimum).Minimum;LinqMaxMilliseconds=($ls.Milliseconds|Measure-Object -Maximum).Maximum;FLinqSpillBytes=(Median $fs.PeakAdditionalReservedStorageBytes);LinqSpillBytes=(Median $ls.PeakAdditionalReservedStorageBytes)}
}}}}
$result=@{Outcome='Passed';Utc=[DateTimeOffset]::UtcNow;Profiles=16;TimedSamples=640;MemoryObservations=64;SourceManifestSha256=$inputHash;InputCount=$inputs.Count;AuditorSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;Evidence=$evidence;Summary=$summary}
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $LogRoot 'ordered-comparison-audit.json') -Encoding utf8
$summary|ForEach-Object {[pscustomobject]$_}|Export-Csv -LiteralPath (Join-Path $LogRoot 'ordered-comparison-summary.csv') -NoTypeInformation -Encoding utf8
$summary|ForEach-Object {[pscustomobject]$_}|Select-Object Rows,Storage,Workers,Query,FLinqMilliseconds,LinqMilliseconds,FLinqAllocatedBytes,LinqAllocatedBytes|Format-Table
