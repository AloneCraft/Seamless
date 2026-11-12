[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SnapshotManifest,
    [Parameter(Mandatory)][string]$ArtifactRoot,
    [Parameter(Mandatory)][string]$MemoryPackGenerator,
    [Parameter(Mandatory)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$SdkVersion,
    [Parameter(Mandatory)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$RuntimeVersion,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'VerifiedStageFiles.ps1')
$manifestPath=(Resolve-Path -LiteralPath $SnapshotManifest).ProviderPath
$manifestHash=(Get-FileHash -LiteralPath $manifestPath).Hash
$snapshot=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
$source=(Resolve-Path -LiteralPath $snapshot.Repository).ProviderPath.TrimEnd('\')
$artifacts=(Resolve-Path -LiteralPath $ArtifactRoot).ProviderPath.TrimEnd('\')
$output=[IO.Path]::GetFullPath($OutputPath)
if(!$source.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase) -or
    !$artifacts.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase) -or
    !$output.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($artifacts+'\',[StringComparison]::OrdinalIgnoreCase) -or !$snapshot.Files.Count){
    throw 'Use separate fixed source, build artifacts and plan output under F:\Seamless.'
}
$sourceHashes=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($file in $snapshot.Files){
    $path=[IO.Path]::GetFullPath((Join-Path $source $file.Path))
    if(!$path.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $path).Hash -cne $file.Sha256){throw "The fixed source changed: $($file.Path)"}
    $sourceHashes.Add($path,$file.Sha256)
}

function Assert-CandidatePdb([string]$assembly){
    $pdb=[IO.Path]::ChangeExtension($assembly,'.pdb')
    $stream=[IO.File]::OpenRead($pdb)
    $provider=$null
    try{
        $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
        $reader=$provider.GetMetadataReader()
        $matched=0
        foreach($handle in $reader.Documents){
            $document=$reader.GetDocument($handle)
            $name=$reader.GetString($document.Name)
            if(!$sourceHashes.ContainsKey($name) -or $document.Hash.IsNil){continue}
            if($reader.GetGuid($document.HashAlgorithm) -ne [guid]'8829d00f-11b8-4213-878b-770e8597ac16' -or
                [Convert]::ToHexString($reader.GetBlobBytes($document.Hash)) -cne $sourceHashes[$name]){
                throw "Build source checksum differs: $assembly / $name"
            }
            $matched++
        }
        if(!$matched){throw "Build output has no source from the fixed candidate: $assembly"}
        $id=[byte[]]@($reader.DebugMetadataHeader.Id)
        $image=[IO.File]::OpenRead($assembly)
        try{
            $pe=[Reflection.PortableExecutable.PEReader]::new($image)
            try{
                $identity=@(foreach($entry in $pe.ReadDebugDirectory()){
                    if($entry.Type -ne [Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView){continue}
                    $info=$pe.ReadCodeViewDebugDirectoryData($entry)
                    if($info.Guid -eq [guid]::new([byte[]]$id[0..15]) -and $info.Age -eq 1 -and $entry.Stamp -eq [BitConverter]::ToUInt32($id,16)){$entry}
                })
                if($identity.Count -ne 1){throw "Build DLL/PDB identity mismatch: $assembly"}
            }finally{$pe.Dispose()}
        }finally{$image.Dispose()}
    }finally{if($provider){$provider.Dispose()};$stream.Dispose()}
}

function Get-ProjectRuntimeAssemblies([string]$entryAssembly){
    $depsPath=[IO.Path]::ChangeExtension($entryAssembly,'.deps.json')
    $deps=Get-Content -LiteralPath $depsPath -Raw|ConvertFrom-Json -AsHashtable
    $target=$deps.targets[$deps.runtimeTarget.name]
    $directory=[IO.Path]::GetDirectoryName($entryAssembly)
    $assemblies=@(foreach($library in $target.Keys){
        if($deps.libraries[$library].type -ne 'project'){continue}
        foreach($asset in $target[$library].runtime.Keys){
            if([IO.Path]::GetExtension($asset) -eq '.dll'){
                Join-Path $directory ([IO.Path]::GetFileName($asset))
            }
        }
    })
    if($entryAssembly -notin $assemblies){throw "The entry assembly is missing from the project runtime assets: $depsPath"}
    return $assemblies
}

$dotnet=(Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$dotnetRoot=[IO.Path]::GetDirectoryName($dotnet)
$packGenerator=(Resolve-Path -LiteralPath $MemoryPackGenerator).ProviderPath
$commonInputs=@($source,$manifestPath,$packGenerator,
    (Join-Path $dotnetRoot 'host/fxr'),(Join-Path $dotnetRoot "sdk/$SdkVersion"),
    (Join-Path $dotnetRoot "shared/Microsoft.NETCore.App/$RuntimeVersion"),
    (Join-Path $dotnetRoot "shared/Microsoft.AspNetCore.App/$RuntimeVersion"))

$sqlServerSkips=@(
    'Seamless.Database.Gateway.Tests.TdsLargeValueTests.SourceReadModes',
    'Seamless.Database.Gateway.Tests.SqlPrintTests.PrintMatchesSqlServerInfoMessage',
    'Seamless.Database.Gateway.Tests.GatewaySqlServerTypeComparisonTests.StoredBoundaryValuesNullsAndWireMetadataMatchSqlServer',
    'Seamless.Database.Gateway.Tests.GatewaySqlServerTypeComparisonTests.ImportedConstraintNamesAndInlineOwnersMatchSqlServer'
)
$gatewayTests=@(
    'Seamless.Database.Gateway.Tests.GatewayVersionedTransactions.NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows(versioned: False)',
    'Seamless.Database.Gateway.Tests.GatewayVersionedTransactions.NativeOrdinaryTableQueriesUseFLinqWithoutExplicitTransactionRows(versioned: True)',
    'Seamless.Database.Gateway.Tests.GatewayProcessTermination.ProcessKillNeverPublishesOnlyOneDatabaseOrSchema(phase: WalDurable, committed: True)',
    'Seamless.Database.Gateway.Tests.TdsStrictLoginTests.SqlClientSendsFedAuthOnlyAfterStrictTlsCertificateValidation'
)
$managementTests=@(
    'Management.Tests.ManagementServer.Connect(collectReads: False)',
    'Management.Tests.ManagementServer.Connect(collectReads: True)',
    'Management.Tests.ManagerWebHostTests.EmbeddedManagerUsesTheSameHostAndRequiresAnOwnerLaunch',
    'Management.Tests.ManagerWebHostTests.CoLocatedServerSharesTheStoreWithCSharpClientsAndOwnerLaunch',
    'Management.Tests.ManagerWebRemoteTests.HttpsLoginIsSeparateFromTheLocalOwnerSession',
    'Management.Tests.ManagerWebRemoteTests.UserOperationsRecheckGrantsAndDisabledAccounts',
    'Management.Tests.GatewaySubjectStatus.OneManagementDisableRollsBackHttpAndNativeWritesBeforeTheirClientsClose',
    'Management.Tests.GatewayHttpAuthorization.StrictTdsValidatesOidcAndChecksEachSqlOperation',
    'Management.Tests.GatewayTdsTransactionAuthorization.TransactionUsesStartPolicyAndReleasesWritesOnRollbackDisconnectAndDisable'
)
$uiFiles=@('manager-web-http.json')

$stages=@(foreach($suite in 'Gateway','Management'){
    foreach($configuration in 'Debug','Release'){
        $config=$configuration.ToLowerInvariant()
        $name='Seamless.Database.Gateway.Tests'
        if($suite -eq 'Management'){$name='Management.Tests'}
        $directory=Join-Path $artifacts "bin/$name/$config"
        $assembly=Join-Path $directory "$name.dll"
        $generator=Join-Path $artifacts "bin/Seamless.Database.Generator/$config/Seamless.Database.Generator.dll"
        $flinq=Join-Path $artifacts "bin/Seamless.FLinq.Generator/$config/Seamless.FLinq.Generator.dll"
        $prepared=@($directory,$generator,[IO.Path]::ChangeExtension($generator,'.pdb'),$flinq,[IO.Path]::ChangeExtension($flinq,'.pdb'),$packGenerator)
        $stage=[ordered]@{
            Name="$suite-$configuration";Executable=$dotnet;WorkingDirectory=$source
            Arguments=@('vstest',$assembly,'/Logger:trx;LogFileName=checks.trx','/ResultsDirectory:{results}')
            InputPath=$commonInputs+$prepared
            Conditions="Windows x64; $suite $configuration full suite; SDK $SdkVersion; runtime $RuntimeVersion"
            TimeoutSeconds=3600;IsolatedTestData=$true
            SnapshotManifest=$manifestPath;SnapshotSha256=$manifestHash
            PathEnvironment=[ordered]@{
                SEAMLESS_SOURCE_REPOSITORY=$source;SEAMLESS_TEST_GENERATOR_ASSEMBLY=$generator
                SEAMLESS_TEST_FLINQ_GENERATOR_ASSEMBLY=$flinq;SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY=$packGenerator
                SEAMLESS_MANAGEMENT_UI_TEST_EXE=$null
            }
        }
        $requiredPaths=$stage.InputPath+@($assembly)
        $entryAssemblies=@($assembly)
        if($suite -eq 'Gateway'){
            $stage.MinimumPassedTests=410
            $stage.RequiredPassedTest=$gatewayTests
            $stage.AllowedSkippedTest=$sqlServerSkips
        }else{
            $ui=Join-Path $artifacts "bin/WindowsServer/$config/Seamless.Management.Server.exe"
            $stage.PathEnvironment.SEAMLESS_MANAGEMENT_SERVER_TEST_EXE=$ui
            $requiredPaths+=@($ui)
            $entryAssemblies+=[IO.Path]::ChangeExtension($ui,'.dll')
            $prepared+=[IO.Path]::GetDirectoryName($ui)
            $stage.MinimumPassedTests=405
            $stage.RequiredPassedTest=$managementTests
            $stage.RequiredArtifact=$uiFiles
            $stage.ManagementUiEvidence=$true
        }
        foreach($path in $requiredPaths){if(!(Test-Path -LiteralPath $path)){throw "Missing fixed input for $($stage.Name): $path"}}
        $stage.PreparedArtifactPath=$prepared
        $stage.PreparedArtifactSha256=Get-ArtifactIdentityHash @(Get-FileIdentity $prepared)
        $projectAssemblies=@(foreach($entry in $entryAssemblies){Get-ProjectRuntimeAssemblies $entry})
        foreach($path in ($projectAssemblies+@($generator,$flinq)|Sort-Object -Unique)){Assert-CandidatePdb $path}
        $stage
    }
})

$plan=[ordered]@{Format=1;Snapshot=$manifestPath;Stages=$stages}
$file=[IO.File]::Open($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$file.Write([Text.Encoding]::UTF8.GetBytes(($plan|ConvertTo-Json -Depth 9)))}finally{$file.Dispose()}
Write-Output $output
