[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [string]$Project='Tests/Seamless.Database.Transactions.Tests/Seamless.Database.Transactions.Tests.csproj',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$Filter,
    [switch]$BuildOnly,
    [switch]$SnapshotOnly,
    [switch]$EmitGeneratedSources,
    [switch]$ForceCompile
)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -InitializeOnly|Out-Null
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPath=[IO.Path]::GetFullPath($TaskRoot)
$logPath=[IO.Path]::GetFullPath($LogRoot)
$taskDrive=[IO.Path]::GetPathRoot($taskPath)
if($taskDrive -notin @('D:\','F:\') -or [IO.Path]::GetPathRoot($logPath) -ne 'F:\'){
    throw 'TaskRoot must be on D or F and LogRoot must be on F.'
}
[IO.Directory]::CreateDirectory($taskPath)|Out-Null
[IO.Directory]::CreateDirectory($logPath)|Out-Null
$validationLease=[IO.FileStream]::new((Join-Path $taskPath 'validation.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
$snapshot=Join-Path $taskPath 'source'
[IO.Directory]::CreateDirectory($snapshot)|Out-Null
$env:TEMP=Join-Path $taskPath 'temp'
$env:TMP=$env:TEMP
$env:DOTNET_CLI_HOME=Join-Path $logPath 'dotnet-home'
$env:NUGET_PACKAGES=Join-Path $logPath 'packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $logPath 'http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:MSBUILDDISABLENODEREUSE='1'
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$manifest=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
function Copy-Source([string]$path){
    $absolute=[IO.Path]::GetFullPath($path)
    if(-not $absolute.StartsWith($sourceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
        throw "Source outside repository: $absolute"
    }
    $relative=[IO.Path]::GetRelativePath($sourceRoot,$absolute)
    $target=Join-Path $snapshot $relative
    $hash=(Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash
    $manifest[$relative]=$hash
    if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash){return}
    [IO.Directory]::CreateDirectory((Split-Path $target))|Out-Null
    Copy-Item -LiteralPath $absolute -Destination $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){
        throw "Source changed while copying the validation snapshot: $relative"
    }
    [IO.File]::SetLastWriteTimeUtc($target,[DateTime]::UtcNow)
}
$pending=[Collections.Generic.Queue[string]]::new()
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$pending.Enqueue([IO.Path]::GetFullPath((Join-Path $sourceRoot $Project)))
while($pending.Count){
    $currentProject=$pending.Dequeue()
    if(-not $seen.Add($currentProject)){continue}
    $directory=Split-Path $currentProject
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($directory,$file.FullName)
        if($relative -match '(^|[\\/])(bin|obj|Artifacts|\.git)([\\/]|$)'){continue}
        if($relative -match '^Work[\\/]' -and (Split-Path $directory -Leaf) -ne 'Seamless.Collections.Persisted'){continue}
        Copy-Source $file.FullName
    }
    [xml]$xml=Get-Content -LiteralPath $currentProject -Raw
    foreach($reference in $xml.SelectNodes('//ProjectReference')){
        $pending.Enqueue([IO.Path]::GetFullPath((Join-Path $directory $reference.Include)))
    }
    foreach($compile in $xml.SelectNodes('//Compile[@Include] | //EmbeddedResource[@Include]')){
        if($compile.Include.Contains('$') -or $compile.Include.IndexOfAny([char[]]'*?') -ge 0){continue}
        $linked=[IO.Path]::GetFullPath((Join-Path $directory $compile.Include))
        if(Test-Path -LiteralPath $linked -PathType Leaf){Copy-Source $linked}
    }
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Shared') -Recurse -File){
    if($file.FullName -match '[\\/](bin|obj|\.git)[\\/]'){continue}
    Copy-Source $file.FullName
}
foreach($file in Get-ChildItem -LiteralPath $sourceRoot -File){
    if($file.Name -like 'Directory.Build.*' -or $file.Name -in @('global.json','NuGet.Config','.editorconfig')){Copy-Source $file.FullName}
}
$manifestPath=Join-Path $logPath 'source-manifest.json'
if(Test-Path -LiteralPath $manifestPath){
    $previous=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -AsHashtable
    foreach($relative in $previous.Keys){
        if($manifest.ContainsKey($relative)){continue}
        $obsolete=[IO.Path]::GetFullPath((Join-Path $snapshot $relative))
        if(-not $obsolete.StartsWith($snapshot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
            throw 'Invalid previous source manifest path.'
        }
        if(Test-Path -LiteralPath $obsolete -PathType Leaf){Remove-Item -LiteralPath $obsolete}
    }
}
$manifest|ConvertTo-Json -Depth 3|Set-Content -LiteralPath $manifestPath -Encoding utf8
$status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status
$memory=$status|ConvertFrom-Json
if(-not $memory.healthy -or -not $memory.lastStatus.codingActive -or $memory.currentUsedPercent -ge 85){
    throw 'Memory guard is unavailable or memory pressure prevents validation.'
}
if($SnapshotOnly){
    [pscustomobject]@{Outcome='SnapshotReady';SourceSnapshot=$snapshot;SourceManifest=$manifestPath;FileCount=$manifest.Count;SourceManifestSha256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash}
    return
}
# NuGet fallbackはCを読めても、Native DTO fixtureはFの明示analyzerパスを使う。
# 新規LogRootでも再現できるよう、既存packageをFへ複製する。Cへrestore/出力しない。
if($seen.Contains([IO.Path]::GetFullPath((Join-Path $sourceRoot 'Tests/Seamless.Database.Gateway.Tests/Seamless.Database.Gateway.Tests.csproj')))){
    $analyzerPackage=Join-Path $env:NUGET_PACKAGES 'memorypack.generator/1.21.4'
    $analyzerFile=Join-Path $analyzerPackage 'analyzers/dotnet/cs/MemoryPack.Generator.dll'
    if(-not(Test-Path -LiteralPath $analyzerFile)){
        if(Test-Path -LiteralPath $analyzerPackage){throw 'The isolated MemoryPack generator package is incomplete; inspect the cache before reuse.'}
        $analyzerSource='C:\Users\kazuh\.nuget\packages\memorypack.generator\1.21.4'
        $sourceFile=Join-Path $analyzerSource 'analyzers/dotnet/cs/MemoryPack.Generator.dll'
        if(-not(Test-Path -LiteralPath $sourceFile)){throw 'The offline MemoryPack generator fixture dependency is missing.'}
        [IO.Directory]::CreateDirectory((Split-Path $analyzerPackage))|Out-Null
        Copy-Item -LiteralPath $analyzerSource -Destination $analyzerPackage -Recurse
        if((Get-FileHash -LiteralPath $sourceFile).Hash -ne (Get-FileHash -LiteralPath $analyzerFile).Hash){throw 'The isolated MemoryPack generator hash does not match the read-only cache.'}
    }
}
$stage='test'
if($BuildOnly){$stage='build'}
$run=Join-Path $logPath ($Configuration+'-'+$stage+'-'+[Guid]::NewGuid().ToString('N'))
$env:SEAMLESS_TEST_DATA_ROOT=Join-Path (Join-Path $taskDrive 'CodexTemp\m') ([Guid]::NewGuid().ToString('N').Substring(0,12))
$env:SEAMLESS_TEST_CACHE_ROOT=Join-Path $logPath 'test-cache'
$env:SEAMLESS_TEST_GENERATOR_ASSEMBLY=Join-Path $snapshot ('Seamless.Database.Generator/bin/'+$Configuration+'/net10.0/Seamless.Database.Generator.dll')
[IO.Directory]::CreateDirectory($env:SEAMLESS_TEST_CACHE_ROOT)|Out-Null
[IO.Directory]::CreateDirectory($env:SEAMLESS_TEST_DATA_ROOT)|Out-Null
[IO.Directory]::CreateDirectory($run)|Out-Null
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $run 'source-manifest.json')
@{Project=$Project;Configuration=$Configuration;Filter=$Filter;EmitGeneratedSources=[bool]$EmitGeneratedSources;ForceCompile=[bool]$ForceCompile;RunnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;DataRoot=$env:SEAMLESS_TEST_DATA_ROOT;SourceManifestSha256=(Get-FileHash -LiteralPath $manifestPath).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'validation-spec.json') -Encoding utf8
$arguments=@($stage,(Join-Path $snapshot $Project),'-c',$Configuration,'-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-p:RestoreSources=C:\Users\kazuh\.nuget\packages','-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages','--disable-build-servers')
if($ForceCompile){
    # 内容が同じ既存コピーも最終検証では再コンパイルする。コピー元の古い更新時刻で旧DLLを再利用させない。
    foreach($relative in $manifest.Keys){
        if([IO.Path]::GetExtension($relative) -eq '.cs'){[IO.File]::SetLastWriteTimeUtc((Join-Path $snapshot $relative),[DateTime]::UtcNow)}
    }
}
if($EmitGeneratedSources){
    $arguments+='-p:EmitCompilerGeneratedFiles=true'
    # 通常ビルドの出力が最新でも、検査対象プロジェクトの生成コードを確実に保存する。
    $projectDirectory=[IO.Path]::GetDirectoryName($Project).Replace('\','/')+'/'
    foreach($relative in $manifest.Keys){
        if($relative.Replace('\','/').StartsWith($projectDirectory,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetExtension($relative) -eq '.cs'){
            [IO.File]::SetLastWriteTimeUtc((Join-Path $snapshot $relative),[DateTime]::UtcNow)
        }
    }
}
if(-not $BuildOnly){
    $arguments+=@('--results-directory',$run,'--logger','trx;LogFileName=results.trx')
    if($Filter){$arguments+=@('--filter',$Filter)}
}
$result=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments $arguments -WorkingDirectory $snapshot -ArtifactDirectory $run -TimeoutSeconds 3600
$result
if($result.Outcome -ne 'Passed'){throw "Validation $($result.Outcome): $($result.Error); logs: $run"}
}finally{$validationLease.Dispose()}
