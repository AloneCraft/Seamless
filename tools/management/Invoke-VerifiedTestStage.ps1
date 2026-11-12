[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AttemptRoot,
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string[]]$Arguments,
    [Parameter(Mandatory)][string]$WorkingDirectory,
    [Parameter(Mandatory)][string[]]$InputPath,
    [Parameter(Mandatory)][string]$Conditions,
    [string]$ReuseFrom,
    [ValidateRange(1,2147483)][int]$TimeoutSeconds=900,
    [string[]]$AllowedSkippedTest=@(),
    [ValidateRange(1,2147483647)][int]$MinimumPassedTests=1,
    [string[]]$RequiredPassedTest=@(),
    [string[]]$RequiredArtifact=@(),
    [string[]]$EnvironmentVariable=@(),
    [string]$SnapshotManifest,
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$SnapshotSha256,
    [string[]]$PreparedArtifactPath=@(),
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreparedArtifactSha256,
    [switch]$ManagementUiEvidence,
    [switch]$IsolatedTestData
)

$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
$identityScript=Join-Path $PSScriptRoot 'VerifiedStageFiles.ps1'
. $identityScript
$testDataVariables=[ordered]@{
    SEAMLESS_MANAGEMENT_TEST_ROOT='m'
    SEAMLESS_TEST_DATA_ROOT='t'
    SEAMLESS_TEST_CACHE_ROOT='c'
}

function Resolve-EvidenceRoot([string]$path){
    if(![IO.Path]::IsPathFullyQualified($path)){throw 'Evidence requires an absolute path.'}
    $path=[IO.Path]::GetFullPath($path)
    if(!$path.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase) -and !$path.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
        throw 'Use a dedicated evidence directory in the repository or under F:\Seamless.'
    }
    for($parent=$path;$parent;$parent=[IO.Path]::GetDirectoryName($parent)){
        if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){
            throw "An evidence directory cannot traverse a link: $parent"
        }
    }
    return $path
}

function Get-IdentityJson {
    $paths=@(@($InputPath)+@($Executable,$runner,$PSCommandPath,$identityScript))
    $uiDirectory=$null
    if($ManagementUiEvidence){
        $uiDirectory=[IO.Path]::GetDirectoryName($env:SEAMLESS_MANAGEMENT_SERVER_TEST_EXE)
        $paths+=$uiDirectory
    }
    $paths=@($paths|Sort-Object -Unique)
    if(($SnapshotManifest -and !$PreparedArtifactSha256) -or
        [bool]$PreparedArtifactPath.Count -ne [bool]$PreparedArtifactSha256 -or
        @($PreparedArtifactPath|Where-Object {$_ -notin $paths}).Count){
        throw 'Prepared artifact paths and hash must identify sealed stage inputs.'
    }
    $artifactFiles=@()
    if($PreparedArtifactSha256){
        $artifactFiles=@(Get-FileIdentity $PreparedArtifactPath)
        if((Get-ArtifactIdentityHash $artifactFiles) -cne $PreparedArtifactSha256){
            throw 'The prepared build artifacts changed after the regression plan was generated.'
        }
    }
    $remaining=@($paths|Where-Object {$_ -notin $PreparedArtifactPath})
    $files=@($artifactFiles+@(Get-FileIdentity $remaining)|Sort-Object {$_.Path} -Unique)
    if($SnapshotManifest){
        $indexed=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($file in $files){$indexed.Add($file.Path,$file.Sha256)}
        if(!$SnapshotSha256 -or !$indexed.ContainsKey($SnapshotManifest) -or $indexed[$SnapshotManifest] -ne $SnapshotSha256){
            throw 'The sealed source manifest does not match the prepared candidate.'
        }
        $snapshot=Get-Content -LiteralPath $SnapshotManifest -Raw|ConvertFrom-Json
        foreach($file in $snapshot.Files){
            $path=[IO.Path]::GetFullPath((Join-Path $snapshot.Repository $file.Path))
            if(!$indexed.ContainsKey($path) -or $indexed[$path] -ne $file.Sha256){throw "The prepared source changed: $($file.Path)"}
        }
    }
    [ordered]@{
        Format=3;Conditions=$Conditions;Arguments=$Arguments;WorkingDirectory=$WorkingDirectory
        TimeoutSeconds=$TimeoutSeconds;AllowedSkippedTest=@($AllowedSkippedTest|Sort-Object -Unique)
        MinimumPassedTests=$MinimumPassedTests;RequiredPassedTest=@($RequiredPassedTest|Sort-Object -Unique)
        RequiredArtifact=@($RequiredArtifact|Sort-Object -Unique);ManagementUiDirectory=$uiDirectory
        IsolatedTestData=[bool]$IsolatedTestData;SnapshotManifest=$SnapshotManifest;SnapshotSha256=$SnapshotSha256;Environment=Get-EnvironmentIdentity
        PreparedArtifactPath=$PreparedArtifactPath;PreparedArtifactSha256=$PreparedArtifactSha256
        Affinity=('0x{0:X}' -f $initial.AffinityMask.ToInt64())
        Windows=[Environment]::OSVersion.Version.ToString();InputPaths=$paths;Inputs=$files
    } | ConvertTo-Json -Depth 6 -Compress
}

function Get-EnvironmentIdentity {
    $variables=@(Get-ChildItem Env: | Where-Object Name -Match '^(SEAMLESS_|DOTNET_|COREHOST_|COMPlus_|VSTEST_|NUGET_)' | Select-Object -ExpandProperty Name)
    $names=@(($variables+$EnvironmentVariable) | ForEach-Object {$_.ToUpperInvariant()} | Sort-Object -Unique)
    if($ManagementUiEvidence){$names=@($names|Where-Object {$_ -ne 'SEAMLESS_MANAGEMENT_TEST_LOG'})}
    if($IsolatedTestData){$names=@($names|Where-Object {$_ -notin $testDataVariables.Keys})}
    $state=@(foreach($name in $names){
        [ordered]@{Name=$name;Value=[Environment]::GetEnvironmentVariable($name,'Process')}
    })|ConvertTo-Json -Depth 3 -Compress -AsArray
    [ordered]@{Names=$names;Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($state)))}
}

function Read-PassingEvidence([string]$path){
    $receipt=Get-Content -LiteralPath (Join-Path $path 'receipt.json') -Raw|ConvertFrom-Json
    $start=Get-Content -LiteralPath (Join-Path $path 'process-start.json') -Raw|ConvertFrom-Json
    if($receipt.Outcome -ne 'Passed' -or $receipt.Task.exitCode -ne 0 -or
        $receipt.Task.id -ne $start.Task.id -or $receipt.Task.pid -ne $start.Task.pid -or
        ([datetimeoffset]$receipt.Task.creationUtc) -ne ([datetimeoffset]$start.Task.creationUtc) -or
        $receipt.Task.affinity -ne ('0x{0:X}' -f $initial.AffinityMask.ToInt64())){
        throw 'A matching successful process receipt is required.'
    }
    [xml]$trx=Get-Content -LiteralPath (Join-Path $path 'checks.trx') -Raw
    $run=$trx.TestRun
    $cases=@($run.Results.UnitTestResult|Where-Object {$_})
    $passed=@($cases|Where-Object outcome -eq 'Passed')
    $skipped=@($cases|Where-Object outcome -eq 'NotExecuted')
    $counts=$run.ResultSummary.Counters
    if($run.ResultSummary.outcome -ne 'Completed' -or !$run.Times.start -or !$run.Times.finish -or
        ([datetimeoffset]$run.Times.start) -lt ([datetimeoffset]$start.Task.creationUtc) -or
        ([datetimeoffset]$run.Times.finish) -lt ([datetimeoffset]$run.Times.start) -or
        !$passed.Count -or $cases.Count -ne [int]$counts.total -or $passed.Count -ne [int]$counts.passed -or
        $passed.Count -ne [int]$counts.executed -or [int]$counts.failed -ne 0 -or $cases.Count -ne ($passed.Count+$skipped.Count) -or
        @($skipped|Where-Object {$_.testName -cnotin $AllowedSkippedTest}).Count){
        throw 'The TRX is stale, empty, failed, incomplete, or contains an unexpected skip.'
    }
    $passedNames=@($passed|ForEach-Object {$_.testName})
    $missing=@($RequiredPassedTest|Where-Object {$_ -cnotin $passedNames})
    if($passed.Count -lt $MinimumPassedTests -or $missing.Count){
        throw ('The TRX does not meet required coverage. Passed: '+$passed.Count+'; missing: '+($missing -join ', '))
    }
    $artifacts=@(foreach($relative in $RequiredArtifact){
        if([IO.Path]::IsPathRooted($relative) -or ($relative -split '[\\/]') -contains '..'){
            throw "Required artifacts must be relative to the attempt directory: $relative"
        }
        $artifact=Resolve-EvidenceRoot (Join-Path $path $relative)
        if(!$artifact.StartsWith($path.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){
            throw "The artifact is outside the attempt directory: $relative"
        }
        $file=Get-Item -LiteralPath $artifact -ErrorAction SilentlyContinue
        if(!$file -or $file.PSIsContainer -or $file.Length -eq 0){throw "Required artifact is absent or empty: $relative"}
        $file.FullName
    })
    $names='receipt.json','process-start.json','checks.trx'
    if($IsolatedTestData){$names+=@('test-runtime.json')}
    $files=@($names|ForEach-Object {Join-Path $path $_})+$artifacts
    [ordered]@{
        Outcome='Passed';Passed=$passed.Count;Skipped=$skipped.Count
        InputSha256=(Get-FileHash -LiteralPath (Join-Path $path 'stage-input.json')).Hash
        Evidence=@(Get-FileIdentity $files)
    } | ConvertTo-Json -Depth 6 -Compress
}

function Write-NewRecord([string]$path,[string]$value){
    $stream=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{
        $writer=[IO.StreamWriter]::new($stream,[Text.UTF8Encoding]::new($false))
        try{$writer.Write($value)}finally{$writer.Dispose()}
    }finally{$stream.Dispose()}
}

$AttemptRoot=Resolve-EvidenceRoot $AttemptRoot
if(Test-Path -LiteralPath $AttemptRoot){throw 'Choose a fresh attempt directory.'}
if($IsolatedTestData -and !$AttemptRoot.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
    throw 'Isolated test data requires a directory under F:\Seamless.'
}
if(!($Arguments | Where-Object {$_.Contains('{results}')})){throw 'Include a {results} placeholder so this attempt produces its own checks.trx.'}
if($ManagementUiEvidence -and (!$RequiredArtifact.Count -or !$env:SEAMLESS_MANAGEMENT_SERVER_TEST_EXE -or
    ![IO.Path]::IsPathFullyQualified($env:SEAMLESS_MANAGEMENT_SERVER_TEST_EXE) -or
    !(Test-Path -LiteralPath $env:SEAMLESS_MANAGEMENT_SERVER_TEST_EXE -PathType Leaf))){
    throw 'Management Web evidence requires explicit HTTP artifacts and an absolute, existing server executable.'
}
$originalAffinity=[Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity
$originalCount=$env:DOTNET_PROCESSOR_COUNT
$originalTelemetry=$env:POWERSHELL_TELEMETRY_OPTOUT
try{
    $initial=& $runner -InitializeOnly
    $identity=Get-IdentityJson
    if($ReuseFrom){
        $ReuseFrom=Resolve-EvidenceRoot $ReuseFrom
        $priorIdentity=Get-Content -LiteralPath (Join-Path $ReuseFrom 'stage-input.json') -Raw
        if($priorIdentity -cne $identity){throw 'The previous stage inputs differ. Start a new attempt without reuse.'}
        $sealed=Get-Content -LiteralPath (Join-Path $ReuseFrom 'stage-passed.json') -Raw
        $proof=Read-PassingEvidence $ReuseFrom
        if($sealed -cne $proof){throw 'The previous stage evidence changed.'}
        [IO.Directory]::CreateDirectory($AttemptRoot)|Out-Null
        Write-NewRecord (Join-Path $AttemptRoot 'stage-reuse.json') ([ordered]@{
            Outcome='Reused';OriginalAttempt=$ReuseFrom
            InputsSha256=(Get-FileHash -LiteralPath (Join-Path $ReuseFrom 'stage-input.json')).Hash
            EvidenceSha256=(Get-FileHash -LiteralPath (Join-Path $ReuseFrom 'stage-passed.json')).Hash
        }|ConvertTo-Json)
        [pscustomobject]@{Outcome='Reused';Attempt=$AttemptRoot;Evidence=$ReuseFrom}
        return
    }

    [IO.Directory]::CreateDirectory($AttemptRoot)|Out-Null
    Write-NewRecord (Join-Path $AttemptRoot 'stage-input.json') $identity
    $expanded=@($Arguments|ForEach-Object {$_.Replace('{results}',$AttemptRoot)})
    $originalUiLog=$env:SEAMLESS_MANAGEMENT_TEST_LOG
    $originalData=@{}
    try{
        if($ManagementUiEvidence){$env:SEAMLESS_MANAGEMENT_TEST_LOG=$AttemptRoot}
        if($IsolatedTestData){
            # Tsavoriteのチェックポイントは深い階層を作るため、証跡先の長さから分離する。
            $dataRoot=Join-Path 'F:\Seamless' ('r'+[guid]::NewGuid().ToString('N').Substring(0,12))
            if(Test-Path -LiteralPath $dataRoot){throw 'The isolated test data directory already exists.'}
            $dataPaths=[ordered]@{}
            foreach($name in $testDataVariables.Keys){
                $originalData[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
                $dataPath=Join-Path $dataRoot $testDataVariables[$name]
                [IO.Directory]::CreateDirectory($dataPath)|Out-Null
                [Environment]::SetEnvironmentVariable($name,$dataPath,'Process')
                $dataPaths[$name]=$dataPath
            }
            Write-NewRecord (Join-Path $AttemptRoot 'test-runtime.json') ($dataPaths|ConvertTo-Json)
        }
        $result=& $runner -Executable $Executable -Arguments $expanded -WorkingDirectory $WorkingDirectory -ArtifactDirectory $AttemptRoot -TimeoutSeconds $TimeoutSeconds -AllowCancellation
    }finally{
        if($ManagementUiEvidence){$env:SEAMLESS_MANAGEMENT_TEST_LOG=$originalUiLog}
        foreach($name in $originalData.Keys){[Environment]::SetEnvironmentVariable($name,$originalData[$name],'Process')}
    }
    if($result.Outcome -ne 'Passed'){throw "Stage ended with $($result.Outcome). See $AttemptRoot."}
    if((Get-IdentityJson) -cne $identity){throw 'The stage inputs changed while it was running.'}
    Write-NewRecord (Join-Path $AttemptRoot 'stage-passed.json') (Read-PassingEvidence $AttemptRoot)
    [pscustomobject]@{Outcome='Passed';Attempt=$AttemptRoot;Evidence=$AttemptRoot}
}finally{
    [Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity=$originalAffinity
    $env:DOTNET_PROCESSOR_COUNT=$originalCount
    $env:POWERSHELL_TELEMETRY_OPTOUT=$originalTelemetry
}
