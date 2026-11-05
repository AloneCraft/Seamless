[CmdletBinding()]
param([ValidateSet('ServerWindows','ServerLinux','WindowsUI')][string[]]$Targets=@('ServerWindows','ServerLinux','WindowsUI'))
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work='D:\CodexTemp\SeamlessManagement-20261102'
$id=[guid]::NewGuid().ToString('N')
$output=Join-Path $work ('distribution-'+$id)
$logs=Join-Path 'F:\CodexTemp\SeamlessManagement-20261102' ('publish-'+$id)
$guard=& 'C:\Python314\python.exe' -B 'G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py' --status | ConvertFrom-Json
if(!$guard.healthy -or !$guard.lastStatus.codingActive -or $guard.currentUsedPercent -ge 85){throw 'Unavailable: メモリ監視または空きメモリを確認してください。'}
New-Item -ItemType Directory -Path $output,$logs -Force | Out-Null
$env:TEMP=$work;$env:TMP=$work;$env:DOTNET_CLI_HOME='F:\CodexTemp\SeamlessManagement-20261102'
$env:NUGET_PACKAGES='F:\CodexTemp\SeamlessManagement-20261102\packages';$env:NUGET_HTTP_CACHE_PATH='F:\CodexTemp\SeamlessManagement-20261102\http'
Copy-Item -LiteralPath (Join-Path $repository 'global.json') -Destination (Join-Path $work 'global.json') -Force
. (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
foreach($target in $Targets){
    $project=Join-Path $repository 'Seamless.Management.Server/WindowsServer.csproj';$rid='win-x64'
    if($target -eq 'ServerLinux'){$rid='linux-x64'}
    if($target -eq 'WindowsUI'){$project=Join-Path $repository 'Seamless.Management.UI/SeamlessManager.csproj'}
    $destination=Join-Path $output $target
    $arguments=@('publish',$project,'-c','Release','-r',$rid,'--self-contained','false','--artifacts-path',(Join-Path $work 'artifacts'),'-o',$destination,'-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages','-p:UseSharedCompilation=false','-m:1')
    $result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs $target) -TimeoutSeconds 900
    if($result.Outcome -ne 'Passed'){Get-Content -LiteralPath (Join-Path $logs ($target+'/stdout.log')) -Tail 20;throw ('配布ビルドが失敗しました: '+$target)}
    $manifest=@(Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName | ForEach-Object {[ordered]@{Path=[IO.Path]::GetRelativePath($destination,$_.FullName);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    [ordered]@{Target=$target;Runtime=$rid;SelfContained=$false;Files=$manifest} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'distribution.json') -Encoding utf8
    Compress-Archive -LiteralPath $destination -DestinationPath (Join-Path $output ($target+'.zip'))
}
Write-Output $output
Write-Output $logs
