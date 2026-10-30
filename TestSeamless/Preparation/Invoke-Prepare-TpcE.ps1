param(
    [string]$WorkDirectory,
    [string]$Server='localhost',
    [string]$Python='C:\Python314\python.exe'
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$localProcess=Join-Path (Split-Path $PSScriptRoot -Parent) 'Invoke-LocalProcess.ps1'
$previousCount=[Environment]::GetEnvironmentVariable('DOTNET_PROCESSOR_COUNT','Process')
$previousTelemetry=[Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT','Process')
$affinity=& $localProcess -InitializeOnly -MaxLogicalProcessors 8
try{
    $storage=& (Join-Path (Split-Path $PSScriptRoot -Parent) 'Initialize-Storage.ps1')
    if(-not $WorkDirectory){$WorkDirectory=Join-Path $storage.ArchiveRoot 'SourcePreparation\prepared-500'}
    $WorkDirectory=[IO.Path]::GetFullPath($WorkDirectory)
    if([IO.Path]::GetPathRoot($WorkDirectory) -eq 'C:\'){throw 'TPC-E preparation output must not use the C drive.'}
    $manifest=Join-Path $WorkDirectory 'manifest.json'
    $flat=Join-Path $WorkDirectory 'flat'
    $importedManifest=Join-Path $repo 'Work\TestSeamless\SourcePreparation\manifest-500.json'
    if(-not $PSBoundParameters.ContainsKey('WorkDirectory') -and (Test-Path -LiteralPath $importedManifest)){$manifest=$importedManifest;$flat=Join-Path $repo 'Work\TestSeamless\SourcePreparation\flat-500';$WorkDirectory=Split-Path $manifest -Parent}
    if(-not (Test-Path -LiteralPath $manifest)){
        if((Test-Path -LiteralPath $WorkDirectory) -and @(Get-ChildItem -Force -LiteralPath $WorkDirectory).Count -ne 0){throw 'Preparation directory is not empty and has no manifest; preserve it and select a new empty directory.'}
        New-Item -ItemType Directory -Force -Path $flat|Out-Null
    }
    $runDirectory=Join-Path $WorkDirectory ('local-executions/'+[guid]::NewGuid().ToString('N'))
    function Invoke-Local([string]$name,[string]$executable,[string[]]$arguments){
        $directory=Join-Path $runDirectory $name
        New-Item -ItemType Directory -Force -Path $directory|Out-Null
        $spec=@{operation=$name;executable=[IO.Path]::GetFullPath($executable);arguments=$arguments;workingDirectory=$WorkDirectory;executionMode='Direct';maxLogicalProcessors=8}
        $specPath=Join-Path $directory 'spec.json'
        [IO.File]::WriteAllText($specPath,($spec|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        $reply=& $localProcess -Executable $spec.executable -Arguments $arguments -WorkingDirectory $WorkDirectory -ArtifactDirectory $directory -TimeoutSeconds 7200 -MaxLogicalProcessors 8
        $status='Failed';if($reply.Outcome -eq 'Passed'){$status='Succeeded'}
        $record=@{status=$status;id=$reply.Task.id;exitCode=$reply.Task.exitCode;operation=$name;executable=$spec.executable;specHash=(Get-FileHash -LiteralPath $specPath -Algorithm SHA256).Hash;artifactDirectory=$directory;executionMode='Direct';maxLogicalProcessors=8;outcome=$reply.Outcome;error=$reply.Error}
        [IO.File]::WriteAllText((Join-Path $directory 'result.json'),($record|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        if($reply.Outcome -ne 'Passed'){throw "Local operation $name failed ($($reply.Outcome)): $($reply.Error); $directory"}
        return [pscustomobject]@{Task=$reply.Task;SpecPath=$specPath;ResultPath=(Join-Path $directory 'result.json')}
    }
    if(-not (Test-Path -LiteralPath $manifest)){
        $egen=Join-Path $repo 'Work\TpcE\egen';$loader=Join-Path $repo 'Work\TpcE\egen-build\EGenLoader.exe'
        $egenArguments=@('-b','1','-c','1000','-t','1000','-f','500','-w','10','-i',((Join-Path $egen 'flat_in')+'\'),'-o',($flat+'\'),'-x')
        $generation=Invoke-Local 'tpce-generate' $loader $egenArguments
        $check=Join-Path $WorkDirectory 'preflight.json'
        [void](Invoke-Local 'tpce-preflight' $Python @((Join-Path $PSScriptRoot 'preflight.py'),$flat,(Join-Path $PSScriptRoot 'tpce-tables.json'),$check))
        $powershell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        [void](Invoke-Local 'tpce-manifest' $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'New-GenerationManifest.ps1'),'-FlatDirectory',$flat,'-ManifestPath',$manifest,'-GenerationSpec',$generation.SpecPath,'-GenerationReceipt',$generation.ResultPath,'-PreflightResult',$check))
    }
    $powershell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    [void](Invoke-Local 'tpce-import' $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Prepare-TpcE.ps1'),'-Server',$Server,'-FlatDirectory',$flat,'-ManifestPath',$manifest))
    Write-Output "TPC_E preparation verified. Manifest: $manifest"
}finally{
    $process=[Diagnostics.Process]::GetCurrentProcess()
    try{$process.ProcessorAffinity=$affinity.OriginalAffinityMask}finally{
        $process.Dispose()
        if($null -eq $previousCount){Remove-Item Env:DOTNET_PROCESSOR_COUNT -ErrorAction SilentlyContinue}else{[Environment]::SetEnvironmentVariable('DOTNET_PROCESSOR_COUNT',$previousCount,'Process')}
        if($null -eq $previousTelemetry){Remove-Item Env:POWERSHELL_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue}else{[Environment]::SetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT',$previousTelemetry,'Process')}
    }
}
