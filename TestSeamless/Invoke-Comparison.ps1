param(
    [ValidateSet('Prepare','Pack','Rebuild','Compare','All')][string]$Operation='All',
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$ManagedRoot,
    [string]$ArchiveRoot,
    [string]$TemporaryRoot,
    [string]$Version,
    [string[]]$DatabaseName,
    [string]$Bgo='G:\GitHub\CodexBackgroundOperations\src\BackgroundOps\bin\Release\net10.0-windows\BackgroundOps.exe',
    [string]$BgoState='D:\CodexBackgroundOps',
    [switch]$Offline
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$storage=& (Join-Path $PSScriptRoot 'Initialize-Storage.ps1') -ArchiveRoot $ArchiveRoot -TemporaryRoot $TemporaryRoot
$ArchiveRoot=$storage.ArchiveRoot;$TemporaryRoot=$storage.TemporaryRoot;$packageRoot=$storage.PackageRoot
if(-not $ManagedRoot){if(Test-Path -LiteralPath 'D:\'){$ManagedRoot='D:\Seamless\Comparison\Managed'}else{$ManagedRoot=Join-Path $ArchiveRoot 'Managed'}}
if([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ManagedRoot)) -eq 'C:\'){throw 'ManagedRoot must not use the C drive.'}
$ManagedRoot=[IO.Path]::GetFullPath($ManagedRoot);New-Item -ItemType Directory -Force -Path $ManagedRoot|Out-Null
$owner=Join-Path $ManagedRoot '.seamless-comparison-owner'
if(-not (Test-Path -LiteralPath $owner)){if(@(Get-ChildItem -Force -LiteralPath $ManagedRoot).Count -ne 0){throw 'ManagedRoot must be empty or already owned by this script.'};[IO.File]::WriteAllText($owner,'Seamless.Comparison.v1')}
if([IO.File]::ReadAllText($owner) -ne 'Seamless.Comparison.v1'){throw 'The managed root owner does not match.'}
$pipelineLease=New-Object IO.FileStream((Join-Path $ManagedRoot '.pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$localCache=Join-Path $ArchiveRoot 'dependency-feed';New-Item -ItemType Directory -Force -Path $localCache|Out-Null
$activePath=Join-Path $ManagedRoot 'active.json'
$utf8=New-Object Text.UTF8Encoding $false
function Write-Json($path,$value){$temporary=$path+'.tmp.'+[Guid]::NewGuid().ToString('N');[IO.File]::WriteAllText($temporary,(ConvertTo-Json -InputObject $value -Depth 100),$utf8);if(Test-Path -LiteralPath $path){$backup=$path+'.backup';[IO.File]::Replace($temporary,$path,$backup,$true)}else{[IO.File]::Move($temporary,$path)}}
function Resolve-Managed([string]$relative){if([IO.Path]::IsPathRooted($relative)){throw 'Managed paths must be relative.'};$prefix=$ManagedRoot.TrimEnd('\')+'\';$resolved=[IO.Path]::GetFullPath((Join-Path $ManagedRoot $relative));if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes the managed root.'};$ancestor=$resolved;while($ancestor -and ($ancestor -eq $ManagedRoot -or $ancestor.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase))){if(Test-Path -LiteralPath $ancestor){if((Get-Item -Force -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Managed paths must not traverse a reparse point.'}};if($ancestor -eq $ManagedRoot){break};$ancestor=Split-Path $ancestor -Parent};return $resolved}
function Invoke-Step([string]$name,[string]$executable,[string[]]$arguments,[object[]]$resources,[int]$timeout=7200){
    $operation=Join-Path $ArchiveRoot ('operations\'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'-'+$name);New-Item -ItemType Directory -Force -Path $operation|Out-Null
    $spec=@{policyVersion='1';agent='seamless-comparison';application='comparison-pipeline';operation=$name;classification='A';method='cli';requiresUserInput=$false;mayActivateWindow=$false;executable=$executable;arguments=$arguments;workingDirectory=$repo;resources=$resources;waitTimeoutSeconds=7200;runTimeoutSeconds=$timeout}
    $specPath=Join-Path $operation 'run.spec.json';Write-Json $specPath $spec
    $reply=& $Bgo run --spec $specPath --state $BgoState|ConvertFrom-Json;Write-Json (Join-Path $operation 'receipt.json') $reply
    if($reply.outcome -ne 'Passed'){throw "Operation '$name' did not pass: $($reply.outcome); $($reply.error); evidence: $($reply.task.artifactDirectory)"}
    return $reply.task
}
function Build-Locks {return @(@{id="workspace:$repo";mode='S'},@{id="build-dir:$repo";mode='X'},@{id='cpu:heavy';mode='X'},@{id="profile:$ManagedRoot";mode='X'},@{id="profile:$ArchiveRoot";mode='X'},@{id=("build-dir:"+(Join-Path (Split-Path $TemporaryRoot -Parent) 'product-build'));mode='X'})}
function Execution-Locks([string]$directory){return @(@{id="profile:$directory";mode='X'},@{id='cpu:heavy';mode='X'},@{id='sqlserver:localhost';mode='S'})}
function Get-PackageProjects{return @(& (Join-Path $PSScriptRoot 'Get-ProductProjects.ps1') -RepositoryRoot $repo)}

function New-Packages{
    if(-not $Version){$script:Version='0.0.0-local.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')}
    $feed=Join-Path $ArchiveRoot ('feeds\'+$Version);if(Test-Path -LiteralPath $feed){throw 'A package version/feed cannot be reused.'};New-Item -ItemType Directory -Path $feed|Out-Null
    $sources=$localCache;if(-not $Offline){$sources+=';https://api.nuget.org/v3/index.json'}
    $plan=Join-Path $feed 'package-plan.json';Write-Json $plan @{Projects=@(Get-PackageProjects);Dotnet=$dotnet;Configuration=$Configuration;Feed=$feed;Version=$Version;Sources=$sources;ArtifactsPath=(Join-Path (Split-Path $TemporaryRoot -Parent) ('product-build/'+$Version))}
    # 同じcheckoutの全製品を一つのS lease内でpackし、版をまたいだソース混在を防ぐ。
    [void](Invoke-Step 'pack-products-snapshot' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Pack-Products.ps1'),'-Plan',$plan) (Build-Locks))

    Write-Json (Join-Path $ArchiveRoot 'latest-packages.json') @{Version=$Version;Feed=$feed;Configuration=$Configuration}
}
function Get-PackageInfo{if(-not (Test-Path -LiteralPath (Join-Path $ArchiveRoot 'latest-packages.json'))){throw 'Run Pack first.'};return Get-Content -Raw -LiteralPath (Join-Path $ArchiveRoot 'latest-packages.json')|ConvertFrom-Json}
function Build-Consumer($packages,[string]$directory){
    $sources=$packages.Feed+';'+$localCache;if(-not $Offline){$sources+=';https://api.nuget.org/v3/index.json'}
    $restoreConfig=Join-Path $directory 'NuGet.Config'; & (Join-Path $PSScriptRoot 'Write-RestoreConfiguration.ps1') -Sources @($sources.Split(';')) -Output $restoreConfig
    $arguments=@('build',(Join-Path $PSScriptRoot 'TestSeamless.csproj'),'-c',$Configuration,'--nologo','-m:1','-nr:false','-p:UseSharedCompilation=false',"-p:SeamlessPackageVersion=$($packages.Version)","-p:GenerationDirectory=$directory","-p:ComparisonBuildRoot=$directory\build","-p:ComparisonPackageRoot=$packageRoot",'-p:NuGetAudit=false',"-p:RestoreConfigFile=$restoreConfig")
    [void](Invoke-Step 'build-package-consumer' $dotnet $arguments (Build-Locks))
    return Join-Path $directory "build\bin\TestSeamless\$Configuration\net10.0\TestSeamless.dll"
}
function Verify-Provenance($packages,[string]$directory){
    $assets=Join-Path $directory 'build\obj\TestSeamless\project.assets.json';$document=Get-Content -Raw -LiteralPath $assets|ConvertFrom-Json
    $cache=[IO.Path]::GetFullPath((Join-Path $packageRoot $packages.Version));$proof=@()
    foreach($library in $document.libraries.PSObject.Properties){if($library.Name -notlike 'Seamless.*/*'){continue};$parts=$library.Name.Split('/');if($parts[1] -ne $packages.Version -or $library.Value.type -ne 'package'){throw "The consumer resolved a product outside this package run: $($library.Name)"};$package=Join-Path $cache $library.Value.path;$metadata=Get-Content -Raw -LiteralPath (Join-Path $package '.nupkg.metadata')|ConvertFrom-Json;if([IO.Path]::GetFullPath($metadata.source).TrimEnd('\') -ne [IO.Path]::GetFullPath($packages.Feed).TrimEnd('\')){throw "The product did not come from the current local feed: $($library.Name)"};$proof+=@{Package=$library.Name;Path=$package;Feed=$metadata.source;ContentHash=$metadata.contentHash}}
    if($proof.Count -eq 0){throw 'No product packages were verified.'};Write-Json (Join-Path $directory 'package-provenance.json') $proof
    $generated=Join-Path $directory 'build\obj\TestSeamless.Queries\generated';$interceptors=@(Get-ChildItem -LiteralPath $generated -Recurse -File -Filter '*.cs'|Where-Object {[IO.File]::ReadAllText($_.FullName) -match 'InterceptsLocation'})
    if($interceptors.Count -eq 0){throw 'No FLinq interceptor was generated for the native query module.'}
    Write-Json (Join-Path $directory 'flinq-proof.json') @{PackageVersion=$packages.Version;Interceptors=@($interceptors|ForEach-Object {@{Path=$_.FullName;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})}
}
function Register-Generation([string]$id){
    $ledgerPath=Join-Path $ManagedRoot 'managed-generations.json';$ledger=@();if(Test-Path -LiteralPath $ledgerPath){$ledger=@(Get-Content -Raw -LiteralPath $ledgerPath|ConvertFrom-Json)}
    if(@($ledger|Where-Object Id -eq $id).Count -ne 0){throw 'Generation ID is already registered.'};$ledger+=@{Id=$id;RelativePath='generations\'+$id};Write-Json $ledgerPath @($ledger)
}
function Remove-RetiredGenerations([string]$currentDirectory){
    $current=Get-Content -Raw -LiteralPath (Join-Path $currentDirectory 'generation.json')|ConvertFrom-Json;$names=@($current.Databases|ForEach-Object Name);$ledgerPath=Join-Path $ManagedRoot 'managed-generations.json';$kept=@()
    foreach($entry in @(Get-Content -Raw -LiteralPath $ledgerPath|ConvertFrom-Json)){
        $path=Resolve-Managed $entry.RelativePath;if($path -eq $currentDirectory){$kept+=$entry;continue};if(-not (Test-Path -LiteralPath $path)){$kept+=$entry;continue}
        if([IO.Path]::GetFileName($path) -ne $entry.Id -or [IO.File]::ReadAllText((Join-Path $path '.seamless-generation-owner')) -ne $entry.Id){throw 'Generation path/owner/ledger mismatch; no cleanup was performed.'}
        if((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing cleanup through a reparse point.'}
        if(@(Get-ChildItem -LiteralPath $path -Force -Recurse|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -gt 0){throw 'Refusing cleanup of a generation containing reparse points.'}
        $manifest=Get-Content -Raw -LiteralPath (Join-Path $path 'generation.json')|ConvertFrom-Json;$removed=@($manifest.Databases|Where-Object {$names -cnotcontains $_.Name})
        if($removed.Count -eq 0){$kept+=$entry;continue}
        # 全体に型を含むDLLもあるため、削除DBを含む旧世代全体を管理台帳の範囲だけで撤去する。
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    Write-Json $ledgerPath @($kept)
}
function Start-OwnedServer([string]$directory,[string]$exe){
    foreach($name in @('server-stop','server-ready.json')){$path=Join-Path $directory $name;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    $spec=@{policyVersion='1';agent='seamless-comparison-server';application='comparison-server';operation=('serve-'+[IO.Path]::GetFileName($directory));classification='A';method='cli';requiresUserInput=$false;mayActivateWindow=$false;executable=$dotnet;arguments=@($exe,'serve',$directory);workingDirectory=$repo;resources=@(@{id="profile:$directory\data";mode='X'},@{id='port:14333';mode='X'},@{id='port:14335';mode='X'},@{id='port:8088';mode='X'});waitTimeoutSeconds=60;runTimeoutSeconds=604800}
    $specPath=Join-Path $directory 'server.spec.json';Write-Json $specPath $spec
    $controller=Start-Process -FilePath $Bgo -ArgumentList @('run','--spec',('"'+$specPath+'"'),'--state',('"'+$BgoState+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'server-controller.json') -RedirectStandardError (Join-Path $directory 'server-controller-error.log')

    $script:CandidateStopUnconfirmed=$false
    try{
        $deadline=[DateTime]::UtcNow.AddSeconds(60);while(-not (Test-Path -LiteralPath (Join-Path $directory 'server-ready.json'))){if($controller.HasExited){throw 'The owned server exited before becoming ready.'};if([DateTime]::UtcNow -gt $deadline){throw 'The owned server did not become ready.'};Start-Sleep -Milliseconds 250}
    }catch{
        [IO.File]::WriteAllText((Join-Path $directory 'server-stop'),'stop')
        if(-not $controller.HasExited){
            $status=& $Bgo status --state $BgoState|ConvertFrom-Json
            $owned=@($status.tasks|Where-Object {$_.ownerPid -eq $controller.Id -and $_.operation -eq $spec.operation -and $_.executable -eq $dotnet -and $_.status -in @('Waiting','Granted','Running')})
            if($owned.Count -eq 1 -and (Get-Process -Id $controller.Id).StartTime.ToUniversalTime() -eq $controller.StartTime.ToUniversalTime()){
                $cancel=& $Bgo cancel --task $owned[0].id --state $BgoState|ConvertFrom-Json
                if(-not $cancel.ok){$script:CandidateStopUnconfirmed=$true;throw 'The owned candidate could not be cancelled; rollback restart was not attempted.'}
            }
            try{Wait-Process -Id $controller.Id -Timeout 60 -ErrorAction Stop}catch{$script:CandidateStopUnconfirmed=$true;throw 'The owned candidate has not stopped; rollback restart was not attempted.'}
        }
        throw
    }
    $identity=Get-Content -Raw -LiteralPath (Join-Path $directory 'server-ready.json')|ConvertFrom-Json
    return @{Directory=$directory;Executable=$exe;ControllerPid=$controller.Id;ControllerCreatedUtc=$controller.StartTime.ToUniversalTime().ToString('O');ServerPid=$identity.Pid;ServerCreatedUtc=$identity.CreatedUtc;Generation=$identity.Generation}
}
function Stop-OwnedServer($record){
    foreach($pair in @(@($record.ControllerPid,$record.ControllerCreatedUtc),@($record.ServerPid,$record.ServerCreatedUtc))){$process=Get-Process -Id $pair[0] -ErrorAction SilentlyContinue;if(-not $process){continue};if($process.StartTime.ToUniversalTime() -ne [DateTime]::Parse($pair[1]).ToUniversalTime()){throw 'PID creation time mismatch; refusing to stop another process.'}}
    [IO.File]::WriteAllText((Join-Path $record.Directory 'server-stop'),'stop');$deadline=[DateTime]::UtcNow.AddSeconds(60);while(Get-Process -Id $record.ServerPid -ErrorAction SilentlyContinue){if([DateTime]::UtcNow -gt $deadline){throw 'The owned server did not stop; refusing to kill it.'};Start-Sleep -Milliseconds 250}
}
if($Operation -eq 'Prepare'){
    $windowsPowerShell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe';[void](Invoke-Step 'prepare-fixture' $windowsPowerShell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Preparation\Prepare-Fixture.ps1'),'-Server','localhost') @(@{id="workspace:$repo";mode='S'},@{id='sqlserver:localhost';mode='X'}))
    & (Join-Path $PSScriptRoot 'Preparation\Invoke-Prepare-TpcE.ps1')
}
if($Operation -in @('Pack','All')){New-Packages}
if($Operation -in @('Rebuild','All')){
    $packages=Get-PackageInfo;$id=[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8);$directory=Resolve-Managed ('generations\'+$id)
    $bootstrap=Resolve-Managed ('bootstrap\'+$packages.Version);$exe=Build-Consumer $packages $bootstrap
    New-Item -ItemType Directory -Path $directory|Out-Null;[IO.File]::WriteAllText((Join-Path $directory '.seamless-generation-owner'),$id);Register-Generation $id
    $captureArguments=@($exe,'prepare',$directory,$packages.Version)
    if($DatabaseName.Count -gt 0){$captureArguments=@($exe,'prepare-selected',$directory,$packages.Version)+$DatabaseName}
    [void](Invoke-Step 'capture-source-and-generate' $dotnet $captureArguments (Execution-Locks $directory))
    $exe=Build-Consumer $packages $directory;Verify-Provenance $packages $directory
    [void](Invoke-Step 'import-and-verify-all-data' $dotnet @($exe,'import',$directory) (Execution-Locks $directory))
    [void](Invoke-Step 'deploy-query-module' $dotnet @($exe,'module',$directory) (Execution-Locks $directory))
    $validationLocks=(Execution-Locks $directory)+@(@{id='port:14343';mode='X'},@{id='port:14345';mode='X'},@{id='port:8098';mode='X'})
    [void](Invoke-Step 'validate-package-tds-native' $dotnet @($exe,'validate',$directory) $validationLocks)
    $manifest=Get-Content -Raw -LiteralPath (Join-Path $directory 'generation.json')|ConvertFrom-Json;if($manifest.State -ne 'Validated'){throw 'The generation did not pass every validation gate.'}
    [void](Invoke-Step 'stage-ssms' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Ssms\Update-SsmsWorkspace.ps1'),'-Operation','Stage','-GenerationManifest',(Join-Path $directory 'generation.json'),'-OutputDirectory',(Join-Path $directory 'ssms-stage')) @(@{id="workspace:$repo";mode='S'},@{id="build-dir:$directory\ssms-stage";mode='X'}))
    $previous=$null;if(Test-Path -LiteralPath $activePath){$previous=Get-Content -Raw -LiteralPath $activePath|ConvertFrom-Json;Stop-OwnedServer $previous}
    $next=$null
    try{$next=Start-OwnedServer $directory $exe;[void](Invoke-Step 'health-after-cutover' $dotnet @($exe,'health',$directory) @(@{id="profile:$directory\health";mode='S'}));[void](Invoke-Step 'publish-ssms' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Publish-TestWorkspace.ps1'),'-GenerationDirectory',$directory,'-PackageVersion',$packages.Version,'-Feed',$packages.Feed,'-PackageCache',$localCache,'-ComparisonPackageRoot',$packageRoot) @(@{id="workspace:$repo";mode='X'},@{id="build-dir:$PSScriptRoot\Ssms";mode='X'}));Write-Json $activePath $next}
    catch{if($next){Stop-OwnedServer $next};if($script:CandidateStopUnconfirmed){throw};if($previous){$restored=Start-OwnedServer $previous.Directory $previous.Executable;Write-Json $activePath $restored};throw}
    Remove-RetiredGenerations $directory
    Write-Host "Activated validated generation: $id"
}
if($Operation -in @('Compare','All')){if(-not (Test-Path -LiteralPath $activePath)){throw 'No active generation exists.'};$current=Get-Content -Raw -LiteralPath $activePath|ConvertFrom-Json;[void](Invoke-Step 'compare-four-routes' $dotnet @($current.Executable,'compare',$current.Directory) ((Execution-Locks $current.Directory)+@(@{id='measurement:seamless-sqlserver';mode='X'})))}

}finally{$pipelineLease.Dispose()}
