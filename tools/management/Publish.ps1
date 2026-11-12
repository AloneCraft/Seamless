[CmdletBinding()]
param([ValidateSet('ServerWindows','ServerLinux')][string[]]$Targets=@('ServerWindows','ServerLinux'),[string]$WorkRoot='F:\Seamless\Management',[string]$LogRoot='F:\Seamless\Management',[string]$PackageFallbackRoot)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work=[IO.Path]::GetFullPath($WorkRoot)
if(![IO.Path]::IsPathRooted($WorkRoot) -or !$work.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase) -or ![IO.Path]::IsPathRooted($LogRoot) -or ![IO.Path]::GetFullPath($LogRoot).StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'WorkRootとLogRootはF:\Seamless配下の絶対パスを指定してください。'}
$fallback=@()
if($PackageFallbackRoot){
    if(![IO.Path]::IsPathRooted($PackageFallbackRoot) -or !(Test-Path -LiteralPath $PackageFallbackRoot -PathType Container)){throw 'PackageFallbackRootは既存キャッシュの絶対パスを指定してください。'}
    $fallback+=('-p:RestoreFallbackFolders='+[IO.Path]::GetFullPath($PackageFallbackRoot))
}
$id=[guid]::NewGuid().ToString('N')
$output=Join-Path $work ('distribution-'+$id)
$logs=Join-Path $LogRoot ('publish-'+$id)
$guard=& 'C:\Python314\python.exe' -B 'G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py' --status | ConvertFrom-Json
if(!$guard.healthy -or !$guard.lastStatus.codingActive -or $guard.currentUsedPercent -ge 85){throw 'Unavailable: メモリ監視または空きメモリを確認してください。'}
New-Item -ItemType Directory -Path $output,$logs -Force | Out-Null
$env:TEMP=$work;$env:TMP=$work;$env:DOTNET_CLI_HOME=$work
$env:NUGET_PACKAGES=Join-Path $work 'packages';$env:NUGET_HTTP_CACHE_PATH=Join-Path $work 'http'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false';$env:MSBUILDDISABLENODEREUSE='1'
Copy-Item -LiteralPath (Join-Path $repository 'global.json') -Destination (Join-Path $work 'global.json') -Force
& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
foreach($target in $Targets){
    $project=Join-Path $repository 'Seamless.Management.Server/WindowsServer.csproj';$rid='win-x64'
    if($target -eq 'ServerLinux'){$rid='linux-x64'}
    $destination=Join-Path $output $target
    $arguments=@('publish',$project,'-c','Release','-r',$rid,'--self-contained','false','--artifacts-path',(Join-Path $work 'artifacts'),'-o',$destination,'-p:UseSharedCompilation=false','-m:1')+$fallback
    $result=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs $target) -TimeoutSeconds 900
    if($result.Outcome -ne 'Passed'){Get-Content -LiteralPath (Join-Path $logs ($target+'/stdout.log')) -Tail 20;throw ('配布ビルドが失敗しました: '+$target)}
    & (Join-Path $PSScriptRoot 'Copy-NativeAssetLayout.ps1') -PublishedDirectory $destination
    if($target -eq 'ServerLinux'){
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Prepare-LinuxNativeCompatibility.sh') -Destination $destination
    }
    & (Join-Path $PSScriptRoot 'New-SourcePackageFeed.ps1') -PublishedDirectory $destination -ArtifactsPath (Join-Path $work 'artifacts') -LogRoot (Join-Path $logs ($target+'-source-packages')) -Configuration Release -PackageFallbackRoot $PackageFallbackRoot | Out-Host
    Copy-Item -LiteralPath (Join-Path $repository 'LICENSE') -Destination $destination
    $hostArguments=@('msbuild',$project,'-t:ResolveFrameworkReferences','-getProperty:AppHostSourcePath','-p:Configuration=Release',('-p:RuntimeIdentifier='+$rid),'-p:SelfContained=false',('-p:ArtifactsPath='+(Join-Path $work 'artifacts')),'-p:UseArtifactsOutput=true','-p:UseSharedCompilation=false','-m:1')+$fallback
    $hostLog=Join-Path $logs ($target+'-apphost-source')
    $hostResult=& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $hostArguments -WorkingDirectory $repository -ArtifactDirectory $hostLog -TimeoutSeconds 90
    if($hostResult.Outcome -ne 'Passed'){throw ('Apphost元ファイルの解決に失敗しました: '+$target)}
    $appHostSource=(Get-Content -LiteralPath (Join-Path $hostLog 'stdout.log') -Raw).Trim()
    $packageRoots=@($env:NUGET_PACKAGES);if($PackageFallbackRoot){$packageRoots+=$PackageFallbackRoot}
    & (Join-Path $PSScriptRoot 'Write-ThirdPartyNotices.ps1') -PublishedDirectory $destination -PackageRoot $packageRoots -AppHostSourcePath $appHostSource
    $manifest=@(Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName | ForEach-Object {[ordered]@{Path=[IO.Path]::GetRelativePath($destination,$_.FullName);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
    [ordered]@{Target=$target;Runtime=$rid;SelfContained=$false;Files=$manifest} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'distribution.json') -Encoding utf8
    Compress-Archive -LiteralPath $destination -DestinationPath (Join-Path $output ($target+'.zip'))
}
Write-Output $output
Write-Output $logs
