[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[string]$Label='unit',[switch]$BuildUI,[switch]$WithUI,[ValidateSet('Management','Execution','Transactions')][string]$Suite='Management')
$ErrorActionPreference='Stop'
$guard=& 'C:\Python314\python.exe' -B 'G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py' --status | ConvertFrom-Json
if(!$guard.healthy -or !$guard.lastStatus.codingActive -or $guard.currentUsedPercent -ge 85){throw 'Unavailable: メモリ監視が非アクティブ、またはメモリ使用率が85%以上です。'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work='D:\CodexTemp\SeamlessManagement-20261102'
$logs=Join-Path 'F:\CodexTemp\SeamlessManagement-20261102' ($Label+'-'+$Configuration+'-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work,$logs,'F:\CodexTemp\SeamlessManagement-20261102\packages','F:\CodexTemp\SeamlessManagement-20261102\http' -Force | Out-Null
$env:TEMP=$work;$env:TMP=$work;$env:DOTNET_CLI_HOME='F:\CodexTemp\SeamlessManagement-20261102'
$env:NUGET_PACKAGES='F:\CodexTemp\SeamlessManagement-20261102\packages'
$env:NUGET_HTTP_CACHE_PATH='F:\CodexTemp\SeamlessManagement-20261102\http'
$env:SEAMLESS_MANAGEMENT_TEST_ROOT=Join-Path $work 'test-data'
$env:SEAMLESS_TEST_DATA_ROOT=Join-Path $work ('regression-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $env:SEAMLESS_TEST_DATA_ROOT -Force | Out-Null
$env:SEAMLESS_MANAGEMENT_TEST_LOG=$logs
if($WithUI){$env:SEAMLESS_MANAGEMENT_UI_TEST_EXE=Join-Path $work ('artifacts/bin/SeamlessManager/'+$Configuration.ToLowerInvariant()+'/Seamless.Management.UI.exe')}else{Remove-Item Env:SEAMLESS_MANAGEMENT_UI_TEST_EXE -ErrorAction SilentlyContinue}
Copy-Item -LiteralPath (Join-Path $repository 'global.json') -Destination (Join-Path $work 'global.json') -Force
. (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
$target=Join-Path $repository 'Tests/Management.Tests/Management.Tests.csproj'
if($Suite -eq 'Execution'){$target=Join-Path $repository 'Execution.Tests/Execution.Tests.csproj'}
if($Suite -eq 'Transactions'){$target=Join-Path $repository 'Tests/Seamless.Database.Transactions.Tests/Seamless.Database.Transactions.Tests.csproj'}
$arguments=@('test',$target,'-c',$Configuration,'--artifacts-path',(Join-Path $work 'artifacts'),'-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages','--logger','trx','--results-directory',$logs)
if($BuildUI){$target=Join-Path $repository 'Seamless.Management.UI/SeamlessManager.csproj';$arguments=@('build',$target,'-c',$Configuration,'--artifacts-path',(Join-Path $work 'artifacts'),'-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages')}
$arguments+=@('-p:UseSharedCompilation=false','-m:1')
if($WithUI -and !$BuildUI){
    $uiArguments=@('build',(Join-Path $repository 'Seamless.Management.UI/SeamlessManager.csproj'),'-c',$Configuration,'--artifacts-path',(Join-Path $work 'artifacts'),'-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages','-p:UseSharedCompilation=false','-m:1')
    $uiResult=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $uiArguments -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs 'ui-build') -TimeoutSeconds 900
    if($uiResult.Outcome -ne 'Passed'){Get-Content -LiteralPath (Join-Path $logs 'ui-build/stdout.log') -Tail 20;throw 'GUIのビルドが失敗しました。'}
}
$result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $work -ArtifactDirectory $logs -TimeoutSeconds 900
Get-Content -LiteralPath (Join-Path $logs 'stdout.log') -Tail 30
Get-Content -LiteralPath (Join-Path $logs 'stderr.log') -Tail 10
Write-Output $logs
if($result.Outcome -ne 'Passed'){exit 1}
