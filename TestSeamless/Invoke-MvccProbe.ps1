[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateRange(64,100000)][int]$Rows=10000,
    [ValidateSet('Memory','Storage')][string]$StorageMode='Memory',
    [ValidateRange(8,4096)][int]$OperationsPerWorker=128
)
$ErrorActionPreference='Stop'
$project='Benchmark/Experiments/ProductTransactions/ProductTransactions.csproj'
& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $LogRoot -Project $project -Configuration Release -BuildOnly
$manifest=Get-FileHash -LiteralPath (Join-Path $LogRoot 'source-manifest.json')
$identity=[Guid]::NewGuid().ToString('N')
$artifacts=Join-Path $LogRoot ('product-probe-'+$identity)
$data=Join-Path 'D:\CodexTemp\Seamless\MvccData' $identity.Substring(0,12)
$snapshot=Join-Path $TaskRoot 'source'
$dll=Join-Path $snapshot 'Benchmark/Experiments/ProductTransactions/bin/Release/net10.0/ProductTransactions.dll'
$generated=Get-ChildItem -LiteralPath (Join-Path $snapshot 'Benchmark/Experiments/ProductTransactions/obj/Release/generated') -Recurse -Filter '*.cs'
$plans=($generated|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw})-join "`n"
foreach($proof in @('.Iterate(ref','.IterateParallel(ref','[GroupBy/取引]','[Join/取引]')){
    if(-not $plans.Contains($proof)){throw "Missing generated execution proof: $proof"}
}
New-Item -ItemType Directory -Path $artifacts -Force|Out-Null
$generated|ForEach-Object{[pscustomobject]@{File=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifacts 'generated-manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $LogRoot 'source-manifest.json') -Destination (Join-Path $artifacts 'source-manifest.json')
@{Rows=$Rows;StorageMode=$StorageMode;OperationsPerWorker=$OperationsPerWorker;DataRoot=$data;SourceManifestSha256=$manifest.Hash;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifacts 'probe-spec.json') -Encoding utf8
$status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard does not permit a heavy probe.'}
$result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,$data,(Join-Path $artifacts 'report.json'),$Rows.ToString(),$manifest.Hash,$StorageMode,$OperationsPerWorker.ToString()) -WorkingDirectory $snapshot -ArtifactDirectory $artifacts -TimeoutSeconds 1800
$result
if($result.Outcome -ne 'Passed'){throw "Product probe $($result.Outcome): $($result.Error); artifacts: $artifacts"}
