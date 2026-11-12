[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateRange(64,100000)][int]$Rows=10000,
    [ValidateSet('Memory','Storage')][string]$StorageMode='Memory',
    [ValidateRange(8,4096)][int]$OperationsPerWorker=64,
    [ValidateSet('queries','mixed','all')][string]$Stage='all',
    [switch]$UseExistingBuild
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$TaskRoot=[IO.Path]::GetFullPath($TaskRoot);$LogRoot=[IO.Path]::GetFullPath($LogRoot)
if([IO.Path]::GetPathRoot($TaskRoot) -ne 'D:\' -or [IO.Path]::GetPathRoot($LogRoot) -ne 'F:\'){throw 'Use D work and F logs'}
$snapshot=Join-Path $TaskRoot 'source'
$dll=Join-Path $snapshot 'Benchmark/Experiments/ProductTransactions/bin/Release/net10.0/ProductTransactions.dll'
$buildFile=Join-Path $LogRoot 'comparison-build.json'
if($UseExistingBuild){
    $cached=Get-Content -LiteralPath $buildFile -Raw|ConvertFrom-Json
    if($cached.TaskRoot -ne $TaskRoot -or $cached.RunnerSha256 -ne (Get-FileHash -LiteralPath $PSCommandPath).Hash -or $cached.AssemblySha256 -ne (Get-FileHash -LiteralPath $dll).Hash){throw 'The frozen comparison build changed'}
    $build=$cached.Build
    $env:TEMP=Join-Path $TaskRoot 'temp';$env:TMP=$env:TEMP;$env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home';$env:NUGET_PACKAGES=Join-Path $LogRoot 'packages';$env:NUGET_HTTP_CACHE_PATH=Join-Path $LogRoot 'http-cache'
}else{
    $build=& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $LogRoot -Project 'Benchmark/Experiments/ProductTransactions/ProductTransactions.csproj' -Configuration Release -BuildOnly
    if($build.Outcome -ne 'Passed'){throw 'Comparison build did not pass'}
    @{TaskRoot=$TaskRoot;Build=$build;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;AssemblySha256=(Get-FileHash -LiteralPath $dll).Hash}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $buildFile
}
if(@(Select-String -LiteralPath (Join-Path $build.Task.artifactDirectory 'stdout.log') -Pattern ': warning ').Count){throw 'Comparison build has warnings'}
$identity=[Guid]::NewGuid().ToString('N')
$artifacts=Join-Path $LogRoot ('comparison-'+$StorageMode+'-'+$Rows+'-'+$Stage+'-'+$identity)
$data=Join-Path 'D:\CodexTemp\m' $identity.Substring(0,12)
$manifest=Join-Path $build.Task.artifactDirectory 'source-manifest.json'
$hash=(Get-FileHash -LiteralPath $manifest).Hash
$generated=@(Get-ChildItem -LiteralPath (Join-Path $snapshot 'Benchmark/Experiments/ProductTransactions/obj/Release/generated') -Recurse -Filter '*.cs')
$plans=($generated|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw})-join "`n"
foreach($proof in @('.Iterate(ref','[GroupBy/取引]','[Join/取引]')){if(-not $plans.Contains($proof)){throw ('Missing generated proof '+$proof)}}
$comparisonPlans=($generated|Where-Object {$_.Name -like '*ComparisonQueries*'}|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw})-join "`n"
foreach($method in @('NativeSum','NativeCount','NativeGroups','NativeJoin','TransactionSum','TransactionCount','TransactionGroups','TransactionJoin')){
    if(-not $comparisonPlans.Contains($method)){throw ('The comparison query itself was not expanded: '+$method)}
}
$nativeSum=[regex]::Match($comparisonPlans,'(?s)public static long OptimizedLinq_NativeSum_[^{]+\{(?<body>.*?)\n    \}')
if(-not $nativeSum.Success -or -not $nativeSum.Groups['body'].Value.Contains('.Iterate')){throw 'Native baseline did not select a native Iterate path'}
New-Item -ItemType Directory -Path $artifacts -Force|Out-Null
Copy-Item -LiteralPath $manifest -Destination (Join-Path $artifacts 'source-manifest.json')
$generated|ForEach-Object{[pscustomobject]@{File=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}}|ConvertTo-Json|Set-Content (Join-Path $artifacts 'generated-manifest.json')
@{Rows=$Rows;StorageMode=$StorageMode;Stage=$Stage;OperationsPerWorker=$OperationsPerWorker;Repeats=5;DataRoot=$data;SourceManifestSha256=$hash;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;BuildArtifact=$build.Task.artifactDirectory}|ConvertTo-Json|Set-Content (Join-Path $artifacts 'comparison-spec.json')
$status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status|ConvertFrom-Json
if(-not $status.healthy -or -not $status.lastStatus.codingActive -or $status.currentUsedPercent -ge 85){throw 'Memory guard prevents comparison'}
$result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,'--comparison',$data,(Join-Path $artifacts 'report.json'),$hash,$Rows.ToString(),$StorageMode,$OperationsPerWorker.ToString(),$Stage) -WorkingDirectory $snapshot -ArtifactDirectory $artifacts -TimeoutSeconds 7200
$result
if($result.Outcome -ne 'Passed'){throw "Comparison $($result.Outcome): $($result.Error); artifacts: $artifacts"}
