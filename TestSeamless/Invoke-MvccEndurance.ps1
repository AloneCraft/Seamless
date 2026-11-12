[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateRange(1,512)][int]$Cycles=32,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [ValidateSet('Update','Churn')][string]$Workload='Update',
    [switch]$ForceCompile
)
$ErrorActionPreference='Stop'
$project='Benchmark/Experiments/ProductTransactions/ProductTransactions.csproj'
$build=& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $LogRoot -Project $project -Configuration $Configuration -BuildOnly -ForceCompile:$ForceCompile
if($build.Outcome -ne 'Passed'){throw 'Endurance build did not pass.'}
$identity=[Guid]::NewGuid().ToString('N')
$artifacts=Join-Path $LogRoot ('endurance-'+$Configuration+'-'+$identity)
$data=Join-Path 'D:\CodexTemp\m' $identity.Substring(0,12)
$snapshot=Join-Path $TaskRoot 'source'
$dll=Join-Path $snapshot ('Benchmark/Experiments/ProductTransactions/bin/'+$Configuration+'/net10.0/ProductTransactions.dll')
$sourceManifest=Join-Path $build.Task.artifactDirectory 'source-manifest.json'
$manifestHash=(Get-FileHash -LiteralPath $sourceManifest -Algorithm SHA256).Hash
$generated=@(Get-ChildItem -LiteralPath (Join-Path $snapshot ('Benchmark/Experiments/ProductTransactions/obj/'+$Configuration+'/generated')) -Recurse -Filter '*.cs')
$plans=($generated|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw})-join "`n"
foreach($proof in @('.Iterate(ref','.IterateParallel(ref','[GroupBy/取引]','[Join/取引]')){
    if(-not $plans.Contains($proof)){throw "Missing generated execution proof: $proof"}
}
New-Item -ItemType Directory -Path $artifacts -Force|Out-Null
Copy-Item -LiteralPath $sourceManifest -Destination (Join-Path $artifacts 'source-manifest.json')
$generated|ForEach-Object{[pscustomobject]@{File=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifacts 'generated-manifest.json') -Encoding utf8
@{Cycles=$Cycles;Rows=256;Workload=$Workload;Configuration=$Configuration;DataRoot=$data;SourceManifestSha256=$manifestHash;BuildArtifact=$build.Task.artifactDirectory;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifacts 'endurance-spec.json') -Encoding utf8
$status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard does not permit an endurance run.'}
$result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,'--endurance',$data,(Join-Path $artifacts 'report.json'),$manifestHash,$Cycles.ToString(),$Workload) -WorkingDirectory $snapshot -ArtifactDirectory $artifacts -TimeoutSeconds 1800
$result
if($result.Outcome -ne 'Passed'){throw "Endurance $($result.Outcome): $($result.Error); artifacts: $artifacts"}
$report=Get-Content -LiteralPath (Join-Path $artifacts 'report.json') -Raw|ConvertFrom-Json
if($report.Records.Count -ne 2*$Cycles -or $report.SourceManifestSha256 -ne $manifestHash -or $report.Workload -ne $Workload){throw 'Endurance evidence does not match the requested run.'}
