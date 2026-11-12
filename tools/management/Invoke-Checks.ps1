[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[string]$Label='unit',[switch]$BuildUI,[switch]$WithUI,[ValidateSet('Management','Execution','Transactions','TransactionCore','Gateway')][string]$Suite='Management',[string]$Filter,[ValidateRange(1,2147483)][int]$TimeoutSeconds=900,[string]$WorkRoot,[string]$PackageFallbackRoot,[switch]$AllowCancellation)
$ErrorActionPreference='Stop'
$guard=& 'C:\Python314\python.exe' -B 'G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py' --status | ConvertFrom-Json
if(!$guard.healthy -or !$guard.lastStatus.codingActive -or $guard.currentUsedPercent -ge 85){throw 'Unavailable: メモリ監視が非アクティブ、またはメモリ使用率が85%以上です。'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$isolated=$PSBoundParameters.ContainsKey('WorkRoot')
$work=Join-Path $repository 'Work/Management'
if($isolated){
    if(![IO.Path]::IsPathRooted($WorkRoot)){throw 'WorkRootは絶対パスを指定してください。'}
    $work=[IO.Path]::GetFullPath($WorkRoot)
    if(!$work.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw '分離するWorkRootはF:\Seamless配下を指定してください。'}
}
$logs=Join-Path $work ($Label+'-'+$Configuration+'-'+[guid]::NewGuid().ToString('N'))
$packages=Join-Path $work 'packages';$http=Join-Path $work 'http';$temporary=Join-Path $work 'temp'
New-Item -ItemType Directory -Path $work,$logs,$packages,$http,$temporary -Force | Out-Null
$env:TEMP=$temporary;$env:TMP=$temporary;$env:DOTNET_CLI_HOME=$work
$env:NUGET_PACKAGES=$packages
$env:NUGET_HTTP_CACHE_PATH=$http
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false';$env:MSBUILDDISABLENODEREUSE='1'
$env:SEAMLESS_MANAGEMENT_TEST_ROOT=Join-Path 'F:\Seamless' ('m'+[guid]::NewGuid().ToString('N').Substring(0,12))
$env:SEAMLESS_SOURCE_REPOSITORY=$repository
$env:SEAMLESS_TEST_DATA_ROOT=Join-Path 'F:\Seamless' ('t'+[guid]::NewGuid().ToString('N').Substring(0,12))
$env:SEAMLESS_TEST_CACHE_ROOT=Join-Path 'F:\Seamless' ('c'+[guid]::NewGuid().ToString('N').Substring(0,12))
if((Test-Path -LiteralPath $env:SEAMLESS_MANAGEMENT_TEST_ROOT) -or (Test-Path -LiteralPath $env:SEAMLESS_TEST_DATA_ROOT) -or (Test-Path -LiteralPath $env:SEAMLESS_TEST_CACHE_ROOT)){throw '検証領域が既に存在します。新しい実行IDで再実行してください。'}
New-Item -ItemType Directory -Path $env:SEAMLESS_MANAGEMENT_TEST_ROOT,$env:SEAMLESS_TEST_DATA_ROOT,$env:SEAMLESS_TEST_CACHE_ROOT -Force | Out-Null
$env:SEAMLESS_MANAGEMENT_TEST_LOG=$logs
Remove-Item Env:SEAMLESS_MANAGEMENT_UI_TEST_EXE -ErrorAction SilentlyContinue
if($isolated){Copy-Item -LiteralPath (Join-Path $repository 'global.json') -Destination (Join-Path $work 'global.json') -Force}
& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
$target=Join-Path $repository 'Tests/Management.Tests/Management.Tests.csproj'
if($Suite -eq 'Execution'){$target=Join-Path $repository 'Tests/Seamless.Core.Tests/Seamless.Core.Tests.csproj'}
if($Suite -eq 'Transactions'){$target=Join-Path $repository 'Tests/Seamless.Database.Transactions.Tests/Seamless.Database.Transactions.Tests.csproj'}
if($Suite -eq 'TransactionCore'){$target=Join-Path $repository 'Tests/Seamless.Transactions.Tests/Seamless.Transactions.Tests.csproj'}
if($Suite -eq 'Gateway'){$target=Join-Path $repository 'Tests/Seamless.Database.Gateway.Tests/Seamless.Database.Gateway.Tests.csproj'}
$buildOptions=@('-p:UseSharedCompilation=false','-m:1')
if($isolated){$buildOptions+=@('--artifacts-path',(Join-Path $work 'artifacts'))}
if($PackageFallbackRoot){
    if(![IO.Path]::IsPathRooted($PackageFallbackRoot) -or !(Test-Path -LiteralPath $PackageFallbackRoot -PathType Container)){throw 'PackageFallbackRootは既存キャッシュの絶対パスを指定してください。'}
    $buildOptions+=('-p:RestoreFallbackFolders='+[IO.Path]::GetFullPath($PackageFallbackRoot))
}
$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY=Join-Path $repository ('Seamless.Database.Generator/bin/'+$Configuration+'/net10.0/Seamless.Database.Generator.dll')
if($isolated){$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY=Join-Path $work ('artifacts/bin/Seamless.Database.Generator/'+$Configuration.ToLowerInvariant()+'/Seamless.Database.Generator.dll')}
$env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY=Join-Path $packages 'memorypack.generator/1.21.4/analyzers/dotnet/cs/MemoryPack.Generator.dll'
if(!(Test-Path -LiteralPath $env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY) -and $PackageFallbackRoot){
    $fallbackAnalyzer=Join-Path $PackageFallbackRoot 'memorypack.generator/1.21.4/analyzers/dotnet/cs/MemoryPack.Generator.dll'
    if(Test-Path -LiteralPath $fallbackAnalyzer){$env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY=$fallbackAnalyzer}
}
$flinqGenerator=$env:SEAMLESS_TEST_FLINQ_GENERATOR_ASSEMBLY
if(!$flinqGenerator){$flinqGenerator=$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY.Replace('Seamless.Database.Generator','Seamless.FLinq.Generator').Replace('net10.0','netstandard2.0')}
$arguments=@('test',$target,'-c',$Configuration,'--logger','trx;LogFileName=checks.trx','--results-directory',$logs)
if($Filter){$arguments+=@('--filter',$Filter)}
if($BuildUI){$target=Join-Path $repository 'Seamless.Management.Server/WindowsServer.csproj';$arguments=@('build',$target,'-c',$Configuration)}
$arguments+=$buildOptions
[ordered]@{StartedUtc=[DateTime]::UtcNow.ToString('o');Repository=$repository;WorkRoot=$work;IsolatedArtifacts=$isolated;Configuration=$Configuration;Suite=$Suite;Filter=$Filter;TimeoutSeconds=$TimeoutSeconds;AllowCancellation=[bool]$AllowCancellation;BuildUI=[bool]$BuildUI;WithUI=[bool]$WithUI;ManagementTestRoot=$env:SEAMLESS_MANAGEMENT_TEST_ROOT;TestDataRoot=$env:SEAMLESS_TEST_DATA_ROOT;TestCacheRoot=$env:SEAMLESS_TEST_CACHE_ROOT;PackageRoot=$packages;PackageFallbackRoot=$PackageFallbackRoot;GeneratorAssembly=$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY;MemoryPackGeneratorAssembly=$env:SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY;FLinqGeneratorAssembly=$flinqGenerator;UiExecutable=$env:SEAMLESS_MANAGEMENT_UI_TEST_EXE;ScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $logs 'execution-input.json') -Encoding utf8
if($WithUI -and !$BuildUI){
    $uiArguments=@('build',(Join-Path $repository 'Seamless.Management.Server/WindowsServer.csproj'),'-c',$Configuration)+$buildOptions
    $uiResult=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $uiArguments -WorkingDirectory $repository -ArtifactDirectory (Join-Path $logs 'ui-build') -TimeoutSeconds 900 -AllowCancellation:$AllowCancellation
    if($uiResult.Outcome -eq 'Cancelled'){Write-Output $logs;exit 2}
    if($uiResult.Outcome -ne 'Passed'){Get-Content -LiteralPath (Join-Path $logs 'ui-build/stdout.log') -Tail 20;throw 'Managerを配信するWebホストのビルドが失敗しました。'}
}
$result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $repository -ArtifactDirectory $logs -TimeoutSeconds $TimeoutSeconds -AllowCancellation:$AllowCancellation
if($result.Outcome -ne 'Passed' -and !(Test-Path -LiteralPath (Join-Path $logs 'stdout.log'))){
    throw "Invoke-LocalProcess failed ($($result.Outcome)): $($result.Error)"
}
Get-Content -LiteralPath (Join-Path $logs 'stdout.log') -Tail 30
Get-Content -LiteralPath (Join-Path $logs 'stderr.log') -Tail 10
Write-Output $logs
if($result.Outcome -eq 'Cancelled'){exit 2}
if($result.Outcome -ne 'Passed'){exit 1}
if(!$BuildUI){
    [xml]$trx=Get-Content -LiteralPath (Join-Path $logs 'checks.trx') -Raw
    if([int]$trx.TestRun.ResultSummary.Counters.total -eq 0){throw 'テストが0件のため合格として扱いません。Filterと対象成果物を確認してください。'}
}
