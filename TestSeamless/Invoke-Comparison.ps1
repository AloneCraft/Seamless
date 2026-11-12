#Requires -Version 7.0
param(
    [ValidateSet('Prepare','Pack','Rebuild','Validate','Activate','Restart','Compare','All')][string]$Operation='All',
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$ManagedRoot,
    [string]$ArchiveRoot,
    [string]$TemporaryRoot,
    [string]$Version,
    [string]$GenerationId,
    [string[]]$DatabaseName,
    [switch]$Offline,
    [switch]$DeferActivation
)
$ErrorActionPreference='Stop'
if($DeferActivation -and $Operation -ne 'Rebuild'){throw '-DeferActivation is supported only with -Operation Rebuild.'}
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
$cpuPolicy=$null;$previousProcessorCount=$env:DOTNET_PROCESSOR_COUNT;$previousTelemetry=$env:POWERSHELL_TELEMETRY_OPTOUT
try{
$cpuPolicy=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly -MaxLogicalProcessors 8
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$localCache=Join-Path $ArchiveRoot 'dependency-feed';New-Item -ItemType Directory -Force -Path $localCache|Out-Null
$activePath=Join-Path $ManagedRoot 'active.json'
$utf8=New-Object Text.UTF8Encoding $false
function Write-Json($path,$value){$temporary=$path+'.tmp.'+[Guid]::NewGuid().ToString('N');[IO.File]::WriteAllText($temporary,(ConvertTo-Json -InputObject $value -Depth 100),$utf8);if(Test-Path -LiteralPath $path){$backup=$path+'.backup';[IO.File]::Replace($temporary,$path,$backup,$true)}else{[IO.File]::Move($temporary,$path)}}
function Resolve-Managed([string]$relative){if([IO.Path]::IsPathRooted($relative)){throw 'Managed paths must be relative.'};$prefix=$ManagedRoot.TrimEnd('\')+'\';$resolved=[IO.Path]::GetFullPath((Join-Path $ManagedRoot $relative));if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escapes the managed root.'};$ancestor=$resolved;while($ancestor -and ($ancestor -eq $ManagedRoot -or $ancestor.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase))){if(Test-Path -LiteralPath $ancestor){if((Get-Item -Force -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Managed paths must not traverse a reparse point.'}};if($ancestor -eq $ManagedRoot){break};$ancestor=Split-Path $ancestor -Parent};return $resolved}
function Invoke-Step([string]$name,[string]$executable,[string[]]$arguments,[object[]]$resources,[int]$timeout=7200){
    $operation=Join-Path $ArchiveRoot ('operations\'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'-'+$name);New-Item -ItemType Directory -Force -Path $operation|Out-Null
    Write-Json (Join-Path $operation 'operation.json') @{Operation=$name;ExecutionMode='Direct';MaxLogicalProcessors=8;Executable=$executable;Arguments=$arguments;WorkingDirectory=$repo;TimeoutSeconds=$timeout;ResourceExclusion='None'}
    $reply=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable $executable -Arguments $arguments -WorkingDirectory $repo -ArtifactDirectory $operation -TimeoutSeconds $timeout -MaxLogicalProcessors 8
    if($reply.Outcome -ne 'Passed'){throw "Operation '$name' did not pass: $($reply.Outcome); $($reply.Error); evidence: $operation"}
    return $reply.Task
}
function Build-Locks {return @(@{id="workspace:$repo";mode='S'},@{id="build-dir:$repo";mode='X'},@{id='cpu:heavy';mode='X'},@{id="profile:$ManagedRoot";mode='X'},@{id="profile:$ArchiveRoot";mode='X'},@{id=("build-dir:"+(Join-Path (Split-Path $TemporaryRoot -Parent) 'product-build'));mode='X'})}
function Execution-Locks([string]$directory){return @(@{id="profile:$directory";mode='X'},@{id='cpu:heavy';mode='X'},@{id='sqlserver:localhost';mode='S'})}
function Get-PackageProjects{return @(& (Join-Path $PSScriptRoot 'Get-ProductProjects.ps1') -RepositoryRoot $repo)}

function New-Packages{
    if(-not $Version){$script:Version='0.0.0-local.'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')}
    $feed=Join-Path $ArchiveRoot ('feeds\'+$Version);if(Test-Path -LiteralPath $feed){throw 'A package version/feed cannot be reused.'};New-Item -ItemType Directory -Path $feed|Out-Null
    $sources=$localCache;if(-not $Offline){$sources+=';https://api.nuget.org/v3/index.json'}
    $plan=Join-Path $feed 'package-plan.json';Write-Json $plan @{Projects=@(Get-PackageProjects);Dotnet=$dotnet;Configuration=$Configuration;Feed=$feed;Version=$Version;Sources=$sources;ArtifactsPath=(Join-Path (Split-Path $TemporaryRoot -Parent) ('product-build/'+$Version))}
    # 同じ比較パイプラインを直列実行し、所有する子プロセスを共通の8論理CPUへ制限する。
    [void](Invoke-Step 'pack-products-snapshot' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Pack-Products.ps1'),'-Plan',$plan) (Build-Locks))

    Write-Json (Join-Path $ArchiveRoot 'latest-packages.json') @{Version=$Version;Feed=$feed;Configuration=$Configuration}
}
function Get-PackageInfo{if(-not (Test-Path -LiteralPath (Join-Path $ArchiveRoot 'latest-packages.json'))){throw 'Run Pack first.'};return Get-Content -Raw -LiteralPath (Join-Path $ArchiveRoot 'latest-packages.json')|ConvertFrom-Json}
function Build-Consumer($packages,[string]$directory){
    $sources=$packages.Feed+';'+$localCache;if(-not $Offline){$sources+=';https://api.nuget.org/v3/index.json'}
    $restoreConfig=Join-Path $directory 'NuGet.Config'; & (Join-Path $PSScriptRoot 'Write-RestoreConfiguration.ps1') -Sources @($sources.Split(';')) -Output $restoreConfig
    $arguments=@('build',(Join-Path $PSScriptRoot 'TestSeamless.csproj'),'-c',$Configuration,'--nologo','-m:1','-nr:false','-p:UseSharedCompilation=false',"-p:SeamlessPackageVersion=$($packages.Version)","-p:GenerationDirectory=$directory","-p:ComparisonBuildRoot=$directory\build","-p:ComparisonPackageRoot=$packageRoot",'-p:NuGetAudit=false',"-p:RestoreConfigFile=$restoreConfig",("-p:RestoreSources="+$sources.Replace(';','%3B')))
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
    $current=Get-Content -Raw -LiteralPath (Join-Path $currentDirectory 'generation.json')|ConvertFrom-Json
    $names=@($current.SourceDatabaseNames);$proofPath=Join-Path $currentDirectory 'validation.json'
    if($current.State -ne 'Validated' -or $names.Count -eq 0 -or @($names|Where-Object {[string]::IsNullOrWhiteSpace($_)}).Count -gt 0 -or -not (Test-Path -LiteralPath $proofPath)){throw 'Cleanup requires a validated generation and the complete verified source inventory.'}
    $proof=Get-Content -Raw -LiteralPath $proofPath|ConvertFrom-Json
    if($proof.Generation -cne $current.Id -or $proof.Status -ne 'Passed' -or $proof.SourceInventoryVerified -ne $true){throw 'Cleanup requires successful final source inventory verification.'}
    $ledgerPath=Join-Path $ManagedRoot 'managed-generations.json';$kept=@();$retired=@()
    foreach($entry in @(Get-Content -Raw -LiteralPath $ledgerPath|ConvertFrom-Json)){
        $path=Resolve-Managed $entry.RelativePath;if($path -eq $currentDirectory){$kept+=$entry;continue};if(-not (Test-Path -LiteralPath $path)){$kept+=$entry;continue}
        if([IO.Path]::GetFileName($path) -ne $entry.Id -or [IO.File]::ReadAllText((Join-Path $path '.seamless-generation-owner')) -ne $entry.Id){throw 'Generation path/owner/ledger mismatch; no cleanup was performed.'}
        if((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing cleanup through a reparse point.'}
        if(@(Get-ChildItem -LiteralPath $path -Force -Recurse|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -gt 0){throw 'Refusing cleanup of a generation containing reparse points.'}
        $manifest=Get-Content -Raw -LiteralPath (Join-Path $path 'generation.json')|ConvertFrom-Json;$removed=@($manifest.Databases|Where-Object {$names -cnotcontains $_.Name})
        if($removed.Count -eq 0){$kept+=$entry;continue}
        # 全体に型を含むDLLもあるため、削除DBを含む旧世代全体を管理台帳の範囲だけで撤去する。
        $retired+=$path
    }
    foreach($path in $retired){Remove-Item -LiteralPath $path -Recurse -Force}
    Write-Json $ledgerPath @($kept)
}
function Start-OwnedServer([string]$directory,[string]$exe){
    foreach($name in @('server-stop','server-ready.json')){$path=Join-Path $directory $name;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    $logDirectory=Join-Path $ArchiveRoot ('servers\'+[IO.Path]::GetFileName($directory)+'\'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $logDirectory -Force|Out-Null
    $server=Start-Process -FilePath $dotnet -ArgumentList @(('"'+$exe+'"'),'serve',('"'+$directory+'"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDirectory 'stdout.log') -RedirectStandardError (Join-Path $logDirectory 'stderr.log')
    $script:CandidateStopUnconfirmed=$false
    try{
        $server.ProcessorAffinity=$cpuPolicy.AffinityMask
        $deadline=[DateTime]::UtcNow.AddSeconds(60);while(-not (Test-Path -LiteralPath (Join-Path $directory 'server-ready.json'))){if($server.HasExited){throw "The owned server exited before becoming ready; logs: $logDirectory"};if([DateTime]::UtcNow -gt $deadline){throw 'The owned server did not become ready.'};Start-Sleep -Milliseconds 250}
        $identity=Get-Content -Raw -LiteralPath (Join-Path $directory 'server-ready.json')|ConvertFrom-Json
        if($identity.Pid -ne $server.Id -or ([DateTime]$identity.CreatedUtc).ToUniversalTime() -ne $server.StartTime.ToUniversalTime() -or $identity.Generation -cne [IO.Path]::GetFileName($directory)){throw 'The ready server identity differs from the owned process.'}
        return @{Directory=$directory;Executable=$exe;ControllerPid=0;ControllerCreatedUtc=$null;ServerPid=$server.Id;ServerCreatedUtc=$server.StartTime.ToUniversalTime().ToString('O');Generation=$identity.Generation;ExecutionMode='Direct';MaxLogicalProcessors=$cpuPolicy.EffectiveLogicalProcessors;ProcessorAffinity=('0x{0:X}' -f $cpuPolicy.AffinityMask.ToInt64());LogDirectory=$logDirectory}
    }catch{
        [IO.File]::WriteAllText((Join-Path $directory 'server-stop'),'stop')
        if(-not $server.HasExited -and -not $server.WaitForExit(60000)){$script:CandidateStopUnconfirmed=$true;throw 'The owned candidate has not stopped; rollback restart was not attempted.'}
        throw
    }finally{$server.Dispose()}
}
function Stop-OwnedServer($record){
    if($record.ServerPid -le 0){throw 'A positive owned server PID is required.'}
    foreach($pair in @(@($record.ControllerPid,$record.ControllerCreatedUtc),@($record.ServerPid,$record.ServerCreatedUtc))){if($pair[0] -le 0){continue};$process=Get-Process -Id $pair[0] -ErrorAction SilentlyContinue;if(-not $process){continue};if($process.StartTime.ToUniversalTime() -ne ([DateTime]$pair[1]).ToUniversalTime()){throw 'PID creation time mismatch; refusing to stop another process.'}}
    [IO.File]::WriteAllText((Join-Path $record.Directory 'server-stop'),'stop');$deadline=[DateTime]::UtcNow.AddSeconds(60);while(Get-Process -Id $record.ServerPid -ErrorAction SilentlyContinue){if([DateTime]::UtcNow -gt $deadline){throw 'The owned server did not stop; refusing to kill it.'};Start-Sleep -Milliseconds 250}
}

function Get-ValidatedGeneration([string]$id){
    if([string]::IsNullOrWhiteSpace($id) -or $id -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$'){throw 'Invalid generation ID.'}
    $entries=@(Get-Content -Raw -LiteralPath (Join-Path $ManagedRoot 'managed-generations.json')|ConvertFrom-Json|Where-Object {$_.Id -ceq $id})
    if($entries.Count -ne 1 -or $entries[0].RelativePath -cne ('generations\'+$id)){throw 'Generation ledger mismatch.'}
    $dir=Resolve-Managed $entries[0].RelativePath
    if([IO.File]::ReadAllText((Join-Path $dir '.seamless-generation-owner')) -cne $id){throw 'Generation owner mismatch.'}
    $m=Get-Content -Raw -LiteralPath (Join-Path $dir 'generation.json')|ConvertFrom-Json
    $v=Get-Content -Raw -LiteralPath (Join-Path $dir 'validation.json')|ConvertFrom-Json
    if($m.Id -cne $id -or $m.State -cne 'Validated' -or -not $m.ProductVersion -or @($m.SourceDatabaseNames).Count -eq 0 -or @($m.Databases).Count -eq 0 -or $v.Generation -cne $id -or $v.Status -cne 'Passed' -or $v.SourceInventoryVerified -ne $true){throw 'Validated generation and inventory proof required.'}
    if($m.ProductVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$'){throw 'Invalid product version.'}
    $generationPrefix=[IO.Path]::GetFullPath($dir).TrimEnd('\')+'\'
    $feed=Join-Path $ArchiveRoot ('feeds\'+$m.ProductVersion)
    $proof=@(Get-Content -Raw -LiteralPath (Join-Path $dir 'package-provenance.json')|ConvertFrom-Json)
    $assets=Get-Content -Raw -LiteralPath (Join-Path $dir 'build\obj\TestSeamless\project.assets.json')|ConvertFrom-Json
    $libs=@($assets.libraries.PSObject.Properties|Where-Object {$_.Name -like 'Seamless.*/*'})
    if($proof.Count -eq 0 -or $proof.Count -ne $libs.Count){throw 'Package provenance incomplete.'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($e in $proof){
        $parts=$e.Package.Split('/');$lib=$assets.libraries.PSObject.Properties[$e.Package]
        if($parts.Count -ne 2 -or $parts[1] -cne $m.ProductVersion -or -not $seen.Add($e.Package) -or -not $lib -or $lib.Value.type -ne 'package'){throw 'Package identity mismatch.'}
        $expected=[IO.Path]::GetFullPath((Join-Path (Join-Path $packageRoot $m.ProductVersion) $lib.Value.path))
        $meta=Get-Content -Raw -LiteralPath (Join-Path $expected '.nupkg.metadata')|ConvertFrom-Json
        if([IO.Path]::GetFullPath($e.Path) -ne $expected -or [IO.Path]::GetFullPath($e.Feed) -ne [IO.Path]::GetFullPath($feed) -or [IO.Path]::GetFullPath($meta.source) -ne [IO.Path]::GetFullPath($feed) -or $meta.contentHash -cne $e.ContentHash){throw 'Package provenance mismatch.'}
    }
    $f=Get-Content -Raw -LiteralPath (Join-Path $dir 'flinq-proof.json')|ConvertFrom-Json
    if($f.PackageVersion -cne $m.ProductVersion -or @($f.Interceptors).Count -eq 0){throw 'FLinq proof mismatch.'}
    $prefix=[IO.Path]::GetFullPath((Join-Path $dir 'build\obj\TestSeamless.Queries\generated')).TrimEnd('\')+'\'
    foreach($e in @($f.Interceptors)){if(-not [IO.Path]::GetFullPath($e.Path).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $e.Path -Algorithm SHA256).Hash -cne $e.Sha256){throw 'FLinq source hash mismatch.'}}
    if(-not $m.ModuleManifest -or [IO.Path]::IsPathRooted($m.ModuleManifest)){throw 'Native manifest missing.'}
    $modulePath=Resolve-Managed ($entries[0].RelativePath+'\'+$m.ModuleManifest)
    if(-not $modulePath.StartsWith($generationPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Native manifest escapes generation.'}
    $mod=Get-Content -Raw -LiteralPath $modulePath|ConvertFrom-Json
    if($mod.ModuleId -cne $id -or $mod.ProductVersion -cne $m.ProductVersion -or -not $mod.AssemblyPath -or [IO.Path]::IsPathRooted($mod.AssemblyPath)){throw 'Native module identity mismatch.'}
    $assembly=Resolve-Managed ($entries[0].RelativePath+'\'+[IO.Path]::GetDirectoryName($m.ModuleManifest)+'\'+$mod.AssemblyPath)
    if(-not $assembly.StartsWith($generationPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Native assembly escapes generation.'}
    if((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -cne $mod.AssemblySha256){throw 'Native module hash mismatch.'}
    foreach($q in @($mod.Queries)){$db=@($m.Databases|Where-Object {$_.Name -ceq $q.Database});if($db.Count -ne 1 -or $db[0].Fingerprint -cne $q.SchemaFingerprint){throw 'Native module fingerprint mismatch.'}}
    $schema=Resolve-Managed ($entries[0].RelativePath+'\'+$m.SchemaAssembly)
    if(-not $schema.StartsWith($generationPrefix,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $schema -PathType Leaf)){throw 'Schema assembly missing or outside generation.'}
    $exe=Join-Path $dir "build\bin\TestSeamless\$Configuration\net10.0\TestSeamless.dll"
    if(-not (Test-Path -LiteralPath $exe -PathType Leaf)){throw 'Generation executable missing.'}
    return @{Id=$id;Directory=$dir;Executable=$exe;Packages=@{Version=$m.ProductVersion;Feed=$feed}}
}
function Activate-ValidatedGeneration($candidate,[string]$verifier){
    $id=$candidate.Id;$directory=$candidate.Directory;$exe=$candidate.Executable;$packages=$candidate.Packages;$stage=Join-Path $directory 'ssms-stage'
    if(-not (Test-Path -LiteralPath $stage)){[void](Invoke-Step 'stage-ssms' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Ssms\Update-SsmsWorkspace.ps1'),'-Operation','Stage','-GenerationManifest',(Join-Path $directory 'generation.json'),'-OutputDirectory',$stage) @(@{id="workspace:$repo";mode='S'},@{id="build-dir:$stage";mode='X'}))}
    [void](Invoke-Step 'validate-ssms-stage' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Ssms\Update-SsmsWorkspace.ps1'),'-Operation','Validate','-OutputDirectory',$stage) @(@{id="workspace:$repo";mode='S'},@{id="build-dir:$stage";mode='S'}))
    $stageLedger=Get-Content -Raw -LiteralPath (Join-Path $stage 'managed-ssms-files.json')|ConvertFrom-Json
    if($stageLedger.Generation -cne $id){throw 'SSMS stage generation mismatch.'}
    [void](Invoke-Step 'verify-source-before-cutover' $dotnet @($verifier,'verify-source',$directory) @(@{id="profile:$directory";mode='S'},@{id='sqlserver:localhost';mode='S'},@{id='cpu:heavy';mode='X'}))
    $previous=$null;if(Test-Path -LiteralPath $activePath){$previous=Get-Content -Raw -LiteralPath $activePath|ConvertFrom-Json;if($previous.Generation -ceq $id){throw 'Generation already active.'};Stop-OwnedServer $previous}
    $next=$null
    try{$next=Start-OwnedServer $directory $exe;[void](Invoke-Step 'health-after-cutover' $dotnet @($exe,'health',$directory) @(@{id="profile:$directory\health";mode='S'}));[void](Invoke-Step 'publish-ssms' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $PSScriptRoot 'Publish-TestWorkspace.ps1'),'-GenerationDirectory',$directory,'-PackageVersion',$packages.Version,'-Feed',$packages.Feed,'-PackageCache',$localCache,'-ComparisonPackageRoot',$packageRoot) @(@{id="workspace:$repo";mode='X'},@{id="build-dir:$PSScriptRoot\Ssms";mode='X'}));Write-Json $activePath $next}
    catch{if($next){Stop-OwnedServer $next};if($script:CandidateStopUnconfirmed){throw};if($previous){$restored=Start-OwnedServer $previous.Directory $previous.Executable;Write-Json $activePath $restored};throw}
    Write-Host "Activated validated generation: $id"
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
    $candidate=Get-ValidatedGeneration $id
    if($DeferActivation){
        Write-Host "Validated generation ready for activation: $id"
    }else{
        Activate-ValidatedGeneration $candidate $exe
        Remove-RetiredGenerations $directory
    }
}
if($Operation -eq 'Validate'){
    if([string]::IsNullOrWhiteSpace($GenerationId) -or $GenerationId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$'){throw 'An imported generation ID is required.'}
    $entries=@(Get-Content -LiteralPath (Join-Path $ManagedRoot 'managed-generations.json') -Raw|ConvertFrom-Json|Where-Object {$_.Id -ceq $GenerationId})
    if($entries.Count -ne 1 -or $entries[0].RelativePath -cne ('generations\'+$GenerationId)){throw 'Generation ledger mismatch.'}
    $directory=Resolve-Managed $entries[0].RelativePath
    if([IO.File]::ReadAllText((Join-Path $directory '.seamless-generation-owner')) -cne $GenerationId){throw 'Generation owner mismatch.'}
    $manifest=Get-Content -LiteralPath (Join-Path $directory 'generation.json') -Raw|ConvertFrom-Json
    if($manifest.Id -cne $GenerationId -or $manifest.State -cne 'Imported' -or $manifest.ProductVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$'){throw 'An imported generation with a fixed package version is required.'}
    if(Test-Path -LiteralPath $activePath){$active=Get-Content -LiteralPath $activePath -Raw|ConvertFrom-Json;if($active.Generation -ceq $GenerationId){throw 'Cannot validate data held by the active server.'}}
    $dataLock=New-Object IO.FileStream((Join-Path $directory 'data\.seamless-open.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try{
        $copy=Resolve-Managed ('generations\'+$GenerationId+'\direct')
        if(Test-Path -LiteralPath $copy){
            $copyManifest=Get-Content -LiteralPath (Join-Path $copy 'generation.json') -Raw|ConvertFrom-Json
            if($copyManifest.Id -cne $GenerationId -or $copyManifest.ProductVersion -cne $manifest.ProductVersion){throw 'Validation copy ownership mismatch.'}
            $copyLock=New-Object IO.FileStream((Join-Path $copy 'data\.seamless-open.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$copyLock.Dispose()
            $attemptRelative='generations\'+$GenerationId+'\validation-attempts\'+[DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')+'-'+[Guid]::NewGuid().ToString('N')
            $attempt=Resolve-Managed $attemptRelative;$archive=Resolve-Managed ($attemptRelative+'\direct');New-Item -ItemType Directory -Path $attempt|Out-Null
            foreach($name in @('wire-validation','generated-database-acceptance','validation')){foreach($extension in @('json','csv','html')){$report=Join-Path $directory ($name+'.'+$extension);if(Test-Path -LiteralPath $report){[IO.File]::Copy($report,(Join-Path $attempt ([IO.Path]::GetFileName($report))),$false)}}}
            Move-Item -LiteralPath $copy -Destination $archive -ErrorAction Stop
            Write-Json (Join-Path $attempt 'archive.json') @{Generation=$GenerationId;Action='Preserve validation copy and reports before retry';Source=$copy;Archive=$archive;Utc=[DateTime]::UtcNow.ToString('O')}
        }
    }finally{$dataLock.Dispose()}
    $packages=@{Version=$manifest.ProductVersion;Feed=(Join-Path $ArchiveRoot ('feeds\'+$manifest.ProductVersion))}
    $exe=Build-Consumer $packages $directory;Verify-Provenance $packages $directory
    [void](Invoke-Step 'redeploy-query-module' $dotnet @($exe,'module',$directory) (Execution-Locks $directory))
    [void](Invoke-Step 'revalidate-package-tds-native' $dotnet @($exe,'validate',$directory) (Execution-Locks $directory))
    [void](Get-ValidatedGeneration $GenerationId)
    Write-Host "Validated imported generation: $GenerationId"
}
if($Operation -eq 'Restart'){
    if(-not (Test-Path -LiteralPath $activePath)){throw 'No active generation is recorded.'}
    $record=Get-Content -Raw -LiteralPath $activePath|ConvertFrom-Json
    $candidate=Get-ValidatedGeneration $record.Generation
    if([IO.Path]::GetFullPath($record.Directory) -cne $candidate.Directory -or [IO.Path]::GetFullPath($record.Executable) -cne $candidate.Executable){throw 'Active server identity does not match the validated generation.'}
    # 同じ検証済み保存世代の再起動は、元RDBMSの可用性や現在のスキーマに依存しない。
    Stop-OwnedServer $record
    $next=$null
    try{
        $next=Start-OwnedServer $candidate.Directory $candidate.Executable
        [void](Invoke-Step 'health-after-restart' $dotnet @($candidate.Executable,'health',$candidate.Directory) @(@{id="profile:$($candidate.Directory)\health";mode='S'}))
        Write-Json $activePath $next
    }catch{if($next){Stop-OwnedServer $next};throw}
    Write-Host "Restarted validated generation: $($candidate.Id)"
}
if($Operation -eq 'Activate'){
    $candidate=Get-ValidatedGeneration $GenerationId
    $bootstrap=Resolve-Managed ('bootstrap\verify-'+[Guid]::NewGuid().ToString('N'))
    $verifier=Build-Consumer $candidate.Packages $bootstrap
    Activate-ValidatedGeneration $candidate $verifier
}
if($Operation -in @('Compare','All')){if(-not (Test-Path -LiteralPath $activePath)){throw 'No active generation exists.'};$current=Get-Content -Raw -LiteralPath $activePath|ConvertFrom-Json;[void](Invoke-Step 'compare-four-routes' $dotnet @($current.Executable,'compare',$current.Directory) ((Execution-Locks $current.Directory)+@(@{id='measurement:seamless-sqlserver';mode='X'})))}

}finally{$pipelineLease.Dispose();if($cpuPolicy){$currentProcess=[Diagnostics.Process]::GetCurrentProcess();try{$currentProcess.ProcessorAffinity=$cpuPolicy.OriginalAffinityMask}finally{$currentProcess.Dispose()}};if($null -eq $previousProcessorCount){Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue}else{Set-Item Env:DOTNET_PROCESSOR_COUNT $previousProcessorCount};if($null -eq $previousTelemetry){Remove-Item Env:POWERSHELL_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue}else{Set-Item Env:POWERSHELL_TELEMETRY_OPTOUT $previousTelemetry}}
