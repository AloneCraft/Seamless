[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WorkRoot,
    [Parameter(Mandatory)][ValidateSet('build','ui','preview','generated','trace-core','trace-recorder','trace-native','history','typed','typed-crash')][string]$Phase,
    [switch]$Inner
)
$ErrorActionPreference='Stop'
$work=(Resolve-Path -LiteralPath $WorkRoot).ProviderPath.TrimEnd('\','/')
$prepared=Get-Content -LiteralPath (Join-Path $work 'prepared.json') -Raw|ConvertFrom-Json
if($prepared.WorkRoot -ne $work -or $prepared.Configuration -ne 'Release' -or !$work.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected prepared Windows Release evidence root.'}
$hashes=$prepared.HarnessSha256
foreach($entry in $hashes.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $work $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value){throw ('The prepared harness changed: '+$entry.Name)}}
if(!$Inner){
    $pwsh=(Get-Process -Id $PID).Path
    $result=& (Join-Path $work 'Invoke-LocalProcess.ps1') -Executable $pwsh -Arguments @('-NoProfile','-File',(Join-Path $work 'QueryVisualizationWindows.Tests.ps1'),'-WorkRoot',$work,'-Phase',$Phase,'-Inner') -WorkingDirectory $prepared.SourceSnapshot -ArtifactDirectory (Join-Path $work ($Phase+'-run')) -TimeoutSeconds 1500 -MaxLogicalProcessors 8 -AllowCancellation
    $result
    if($result.Outcome -ne 'Passed'){exit 1}
    exit 0
}
if([Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity.ToInt64() -ne 255){throw 'The phase must run inside the shared CPU0-7 wrapper.'}
$claim=[IO.FileStream]::new((Join-Path $work ($Phase+'.claim')),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
$claim.Dispose()
$base='F:\Seamless\linux-desktop-recovery-20261109'
$env:TEMP=Join-Path $base 'windows-managed-tmp';$env:TMP=$env:TEMP
$env:NUGET_PACKAGES=Join-Path $base 'windows-dotnet-home\.nuget\packages'
$env:DOTNET_CLI_HOME=Join-Path $base 'windows-dotnet-home'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $base 'windows-http-cache'
$env:MSBUILDDISABLENODEREUSE='1';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1';$env:DOTNET_PROCESSOR_COUNT='8'
$env:SEAMLESS_MANAGEMENT_TEST_ROOT='F:\Seamless\m9\db'
$env:SEAMLESS_TEST_DATA_ROOT=Join-Path $base 'windows-manager-data'
$env:SEAMLESS_TEST_CACHE_ROOT=Join-Path $base 'windows-manager-cache'
$env:SEAMLESS_SOURCE_REPOSITORY=$prepared.SourceSnapshot;$env:SEAMLESS_TEST_REPOSITORY=$prepared.SourceSnapshot
$env:DOTNET_HOST_PATH=(Get-Command dotnet -CommandType Application).Source
$source=$prepared.SourceSnapshot
$artifacts=$prepared.ArtifactsRoot
$manifestPath=Join-Path $work 'source-manifest.json'
$project=Join-Path $source 'Tests/Management.Tests/Management.Tests.csproj'
function Save-Proof([string]$name,$value){
    $stream=[IO.FileStream]::new((Join-Path $work $name),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write([Text.UTF8Encoding]::new($false).GetBytes(($value|ConvertTo-Json -Depth 8)));$stream.Flush($true)}finally{$stream.Dispose()}
}
function Get-FilesProof([string]$root,[switch]$DebugOnly){
    $result=[ordered]@{}
    foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        if($DebugOnly -and $relative -notmatch '(^|/)debug(/|_|$)'){continue}
        $result[$relative]=[ordered]@{Sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;Length=$file.Length;LastWriteUtcTicks=$file.LastWriteTimeUtc.Ticks}
    }
    return $result
}
function Compare-Files($before,$after,[string]$label){
    if($before.Count -ne $after.Count){throw ($label+' file set changed.')}
    foreach($key in $before.Keys){if(!$after.Contains($key) -or ($before[$key]|ConvertTo-Json -Compress) -ne ($after[$key]|ConvertTo-Json -Compress)){throw ($label+' changed: '+$key)}}
}
function Verify-Source([string]$suffix){
    if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $prepared.SourceManifestSha256){throw 'The fixed source manifest changed.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -AsHashtable
    $actual=@(Get-ChildItem -LiteralPath $source -Recurse -File)
    if($prepared.SourceFileCount -lt 1 -or $prepared.SourceFileCount -gt 65536 -or $actual.Count -ne $prepared.SourceFileCount -or $manifest.Count -ne $prepared.SourceFileCount){throw 'The fixed source file set changed.'}
    if((!$manifest.ContainsKey('Tests/Management.Tests/Management.Tests.csproj') -and !$manifest.ContainsKey('Tests\Management.Tests\Management.Tests.csproj')) -or !(Test-Path -LiteralPath $project -PathType Leaf)){throw 'The fixed Management.Tests project is missing.'}
    foreach($relative in $manifest.Keys){$path=[IO.Path]::GetFullPath((Join-Path $source $relative));if(!$path.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $manifest[$relative]){throw ('Fixed source mismatch: '+$relative)}}
    Save-Proof ($Phase+'-source-'+$suffix+'.json') @{Source=$source;FileCount=$actual.Count;ManifestSha256=$prepared.SourceManifestSha256;ObservedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')}
}
$filterMap=@{
    preview='FullyQualifiedName~Management.Tests.GeneratedControlFlowPresentation.|FullyQualifiedName~Management.Tests.PlanElementMeaning.|FullyQualifiedName~Management.Tests.TreeExpansion.|FullyQualifiedName~Management.Tests.ManagerDatabaseLoading.|FullyQualifiedName~Management.Tests.ContextualAdditions.|FullyQualifiedName~Management.Tests.ResultsGridOperations.|FullyQualifiedName~Management.Tests.ResultsGridCells.'
    ui='FullyQualifiedName~QueryPlanPresentation|FullyQualifiedName~PlanGraph|FullyQualifiedName~QueryTracePresentation|FullyQualifiedName~QueryTraceLayout|FullyQualifiedName~QueryExecutionHistory.HistoryView|FullyQualifiedName~ManagementTypedStore.OfflineCopiesAndRepeatedUpgrade|FullyQualifiedName~ManagementTypedStore.UnknownFormatIsRejected|FullyQualifiedName~ManagementTypedStore.MissingCatalogRow'
    generated='FullyQualifiedName~Management.Tests.GeneratedControlFlow.|FullyQualifiedName~Management.Tests.GeneratedCodeContracts.|FullyQualifiedName~Management.Tests.QueryPlanBinding.|FullyQualifiedName~Management.Tests.PlanSnapshots.'
    'trace-core'='FullyQualifiedName~Management.Tests.QueryTraceCore.'
    'trace-recorder'='FullyQualifiedName~Management.Tests.QueryTraceRecorderTests.'
    'trace-native'='FullyQualifiedName~Management.Tests.QueryTraceIntegration.|FullyQualifiedName~Management.Tests.QueryTraceSamplingIntegration.'
    history='FullyQualifiedName~Management.Tests.QueryExecutionHistory.|FullyQualifiedName~Management.Tests.ExecutionHistoryIntegrity.|FullyQualifiedName~Management.Tests.QueryExecutionHistoryAuthorization.'
    typed='FullyQualifiedName~Management.Tests.ManagementTypedStore.'
    'typed-crash'='FullyQualifiedName~Management.Tests.ManagementTypedUpgradeCrash.'
}
$expected=@{ui=44;preview=38;generated=39;'trace-core'=9;'trace-recorder'=8;'trace-native'=4;history=17;typed=14;'typed-crash'=3}
$debug=Get-FilesProof $artifacts -DebugOnly
Save-Proof ($Phase+'-debug-before.json') $debug
Verify-Source before
$result=[ordered]@{Phase=$Phase;Configuration='Release';SourceManifestSha256=$prepared.SourceManifestSha256;Status='Failed';CommandExitCode=$null;Error=$null;CpuMask=255}
try{
    Set-Location $source
    Save-Proof ($Phase+'-input.json') @{Project=$project;Artifacts=$artifacts;Dotnet=$env:DOTNET_HOST_PATH;Harness=$hashes;ExpectedCount=$expected[$Phase];Filter=$filterMap[$Phase];ObservedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')}
    if($Phase -eq 'build'){
        $properties=& dotnet msbuild $project -getProperty:ArtifactsPath,BaseOutputPath,BaseIntermediateOutputPath,RestorePackagesPath -p:UseArtifactsOutput=true ('-p:ArtifactsPath='+$artifacts) ('-p:RestorePackagesPath='+$env:NUGET_PACKAGES) -p:Configuration=Release -nologo
        if($LASTEXITCODE -ne 0){throw 'The project output path evaluation failed.'}
        $paths=($properties -join "`n")|ConvertFrom-Json
        foreach($key in @('ArtifactsPath','BaseOutputPath','BaseIntermediateOutputPath')){$value=[IO.Path]::GetFullPath($paths.Properties.$key).TrimEnd('\','/');if($value -ne $artifacts -and !$value.StartsWith($artifacts+'\',[StringComparison]::OrdinalIgnoreCase)){throw ('Build output escaped owned artifacts: '+$key)}}
        if([IO.Path]::GetFullPath($paths.Properties.RestorePackagesPath).TrimEnd('\','/') -ne $env:NUGET_PACKAGES.TrimEnd('\','/')){throw 'Unexpected package cache.'}
        Save-Proof 'build-paths.json' $paths
        & dotnet build $project --artifacts-path $artifacts -c Release -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
        $result.CommandExitCode=$LASTEXITCODE
        if($LASTEXITCODE -ne 0){throw 'The fixed Release build failed.'}
        $output=Join-Path $artifacts 'bin/Management.Tests/release'
        if(!(Test-Path -LiteralPath (Join-Path $output 'Management.Tests.dll'))){throw 'The Release test DLL is missing.'}
        Save-Proof 'release-output.json' (Get-FilesProof $output)
    }else{
        $build=Get-Content -LiteralPath (Join-Path $work 'build-result.json') -Raw|ConvertFrom-Json
        if($build.Status -ne 'Passed'){throw 'The successful fixed Release build is required.'}
        $output=Join-Path $artifacts 'bin/Management.Tests/release'
        $before=Get-Content -LiteralPath (Join-Path $work 'release-output.json') -Raw|ConvertFrom-Json -AsHashtable
        Compare-Files $before (Get-FilesProof $output) 'The fixed Release output'
        $settings=Join-Path $base 'serial.runsettings'
        & dotnet test $project --artifacts-path $artifacts -c Release --no-build --no-restore --settings $settings --filter $filterMap[$Phase] --logger 'console;verbosity=normal' --logger 'trx;LogFileName=checks.trx' --results-directory (Join-Path $work $Phase)
        $result.CommandExitCode=$LASTEXITCODE
        if($LASTEXITCODE -ne 0){throw 'The explicit Release test phase failed.'}
        $trx=Join-Path $work ($Phase+'/checks.trx')
        [xml]$xml=Get-Content -LiteralPath $trx -Raw
        $ns=[Xml.XmlNamespaceManager]::new($xml.NameTable);$ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $tests=@($xml.SelectNodes('//t:UnitTestResult',$ns));$counters=$xml.SelectSingleNode('//t:Counters',$ns)
        if($tests.Count -ne $expected[$Phase] -or $tests.Count -eq 0 -or [int]$counters.total -ne $tests.Count -or [int]$counters.executed -ne $tests.Count -or @($tests|Where-Object outcome -ne 'Passed').Count -ne 0){throw 'The exact selected test cohort did not pass completely.'}
        $result.TestCount=$tests.Count;$result.TestNames=@($tests|ForEach-Object testName);$result.TrxSha256=(Get-FileHash -LiteralPath $trx -Algorithm SHA256).Hash
        Compare-Files $before (Get-FilesProof $output) 'The fixed Release output'
    }
    $result.Status='Passed'
}catch{$result.Error=$_.Exception.ToString();Write-Error -ErrorRecord $_ -ErrorAction Continue}
finally{
    try{Verify-Source after;$result.SourceVerified=$true}catch{$result.Status='Failed';$result.SourceError=$_.Exception.ToString()}
    try{$debugAfter=Get-FilesProof $artifacts -DebugOnly;Save-Proof ($Phase+'-debug-after.json') $debugAfter;Compare-Files $debug $debugAfter 'The existing Debug output';$result.DebugUnchanged=$true}catch{$result.Status='Failed';$result.DebugError=$_.Exception.ToString()}
    $result.CompletedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');Save-Proof ($Phase+'-result.json') $result
}
if($result.Status -ne 'Passed'){exit 1}
