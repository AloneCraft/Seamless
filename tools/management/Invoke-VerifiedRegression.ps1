[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$AttemptRoot,
    [string]$ReuseFrom
)

$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$stageScript=Join-Path $PSScriptRoot 'Invoke-VerifiedTestStage.ps1'

function Resolve-RunDirectory([string]$path){
    if(![IO.Path]::IsPathFullyQualified($path)){throw 'The regression directory must be absolute.'}
    $path=[IO.Path]::GetFullPath($path).TrimEnd('\')
    if(!$path.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase) -and !$path.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
        throw 'Use a dedicated regression directory in the repository or under F:\Seamless.'
    }
    for($parent=$path;$parent;$parent=[IO.Path]::GetDirectoryName($parent)){
        if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){
            throw "A regression directory cannot traverse a link: $parent"
        }
    }
    return $path
}

function Write-Result([string]$path,$record){
    $temporary="$path.tmp"
    $file=[IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{
        $json=$record|ConvertTo-Json -Depth 8
        $file.Write([Text.Encoding]::UTF8.GetBytes($json))
    }finally{$file.Dispose()}
    [IO.File]::Move($temporary,$path)
}

$planText=Get-Content -LiteralPath $PlanPath -Raw
$plan=$planText|ConvertFrom-Json -AsHashtable
$stages=@($plan.Stages)
if($plan.Format -ne 1 -or !$stages.Count){throw 'The plan must contain one or more ordered stages in format 1.'}
$parameterNames=@('Executable','Arguments','WorkingDirectory','InputPath','Conditions','TimeoutSeconds','AllowedSkippedTest',
    'MinimumPassedTests','RequiredPassedTest','RequiredArtifact','EnvironmentVariable','ManagementUiEvidence','IsolatedTestData','SnapshotManifest','SnapshotSha256',
    'PreparedArtifactPath','PreparedArtifactSha256')
$pathVariables=@('SEAMLESS_SOURCE_REPOSITORY','SEAMLESS_TEST_GENERATOR_ASSEMBLY','SEAMLESS_TEST_FLINQ_GENERATOR_ASSEMBLY',
    'SEAMLESS_TEST_MEMORYPACK_GENERATOR_ASSEMBLY','SEAMLESS_MANAGEMENT_UI_TEST_EXE')
$names=[Collections.Generic.List[string]]::new()
foreach($stage in $stages){
    if($stage -isnot [Collections.IDictionary] -or $stage.Name -notmatch '^[A-Za-z][A-Za-z0-9-]{0,63}$' -or $stage.Name -in $names){
        throw 'Each stage needs a unique name containing only letters, digits and hyphens.'
    }
    if(@($stage.Keys|Where-Object {$_ -notin 'Name','PathEnvironment' -and $_ -notin $parameterNames}).Count){throw "Unknown stage parameter: $($stage.Name)"}
    if($stage.Contains('PathEnvironment')){
        if($stage.PathEnvironment -isnot [Collections.IDictionary]){throw "Invalid path environment: $($stage.Name)"}
        foreach($key in $stage.PathEnvironment.Keys){
            $value=$stage.PathEnvironment[$key]
            if($key -cnotin $pathVariables -or ($null -ne $value -and
                ($value -isnot [string] -or ![IO.Path]::IsPathFullyQualified($value) -or !(Test-Path -LiteralPath $value)))){
                throw "Invalid stage path binding: $key"
            }
        }
    }
    foreach($required in 'Executable','Arguments','WorkingDirectory','InputPath','Conditions'){
        if(!$stage[$required]){throw "Missing $required for $($stage.Name)"}
    }
    $names.Add($stage.Name)
}

$AttemptRoot=Resolve-RunDirectory $AttemptRoot
if(Test-Path -LiteralPath $AttemptRoot){throw 'Choose a new regression directory.'}
$previous=@()
if($ReuseFrom){
    $ReuseFrom=Resolve-RunDirectory $ReuseFrom
    $prior=Get-Content -LiteralPath (Join-Path $ReuseFrom 'regression-result.json') -Raw|ConvertFrom-Json
    if($prior.Format -ne 1 -or !$prior.CanResume -or $prior.Outcome -notin 'Passed','Stopped'){
        throw 'The previous regression has no terminal evidence permitting resume.'
    }
    $previous=@($prior.Accepted|Where-Object {$_})
    for($index=0;$index -lt $previous.Count;$index++){
        if($index -ge $names.Count -or $previous[$index].Name -cne $names[$index] -or
            $previous[$index].Outcome -notin 'Passed','Reused' -or !$previous[$index].Evidence){
            throw 'The previously accepted stages are not an ordered prefix of the new plan.'
        }
    }
}

[IO.Directory]::CreateDirectory($AttemptRoot)|Out-Null
$result=[ordered]@{Format=1;Outcome='Running';CanResume=$false;Planned=$names;Accepted=[Collections.Generic.List[object]]::new();StoppedStage=$null;StopReason=$null}
Write-Result (Join-Path $AttemptRoot 'regression-input.json') ([ordered]@{
    Format=1;Planned=$names;Previous=$ReuseFrom;StartedUtc=[DateTimeOffset]::UtcNow.ToString('o')
    PlanSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($planText)))
    DriverSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash
})
for($index=0;$index -lt $stages.Count;$index++){
    $stage=$stages[$index]
    $stageRoot=Join-Path $AttemptRoot $stage.Name
    $parameters=@{AttemptRoot=$stageRoot}
    foreach($key in $parameterNames){if($stage.Contains($key)){$parameters[$key]=$stage[$key]}}
    if($index -lt $previous.Count){$parameters.ReuseFrom=$previous[$index].Evidence}
    $originalPaths=@{}
    try{
        foreach($key in $stage.PathEnvironment.Keys){
            $originalPaths[$key]=[Environment]::GetEnvironmentVariable($key,'Process')
            [Environment]::SetEnvironmentVariable($key,$stage.PathEnvironment[$key],'Process')
        }
        $outcome=& $stageScript @parameters
        if($outcome.Outcome -notin 'Passed','Reused'){throw "Unaccepted stage outcome: $($stage.Name)"}
        $result.Accepted.Add([ordered]@{Name=$stage.Name;Outcome=$outcome.Outcome;Attempt=$outcome.Attempt;Evidence=$outcome.Evidence})
    }catch{
        $result.Outcome='Stopped'
        $result.StoppedStage=$stage.Name
        $result.StopReason=$_.Exception.Message
        $receiptPath=Join-Path $stageRoot 'receipt.json'
        if(Test-Path -LiteralPath $receiptPath){
            $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json -ErrorAction SilentlyContinue
            $result.CanResume=$receipt.Outcome -in 'Passed','Failed','Cancelled','Timeout'
        }else{
            $result.CanResume=!(Test-Path -LiteralPath (Join-Path $stageRoot 'process-attempt.lock'))
        }
        Write-Result (Join-Path $AttemptRoot 'regression-result.json') $result
        throw
    }finally{
        foreach($key in $originalPaths.Keys){[Environment]::SetEnvironmentVariable($key,$originalPaths[$key],'Process')}
    }
}
$result.Outcome='Passed'
$result.CanResume=$true
$resultPath=Join-Path $AttemptRoot 'regression-result.json'
Write-Result $resultPath $result
[pscustomobject]@{Outcome='Passed';Attempt=$AttemptRoot;Result=$resultPath}
