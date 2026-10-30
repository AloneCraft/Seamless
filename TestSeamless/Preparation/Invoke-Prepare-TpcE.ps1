param(
    [string]$WorkDirectory,
    [string]$Server='localhost',
    [string]$Python='C:\Python314\python.exe',
    [string]$Bgo='G:\GitHub\CodexBackgroundOperations\src\BackgroundOps\bin\Release\net10.0-windows\BackgroundOps.exe',
    [string]$BgoState='D:\CodexBackgroundOps'
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$storage=& (Join-Path (Split-Path $PSScriptRoot -Parent) 'Initialize-Storage.ps1')
if(-not $WorkDirectory){$WorkDirectory=Join-Path $storage.ArchiveRoot 'SourcePreparation\prepared-500'}
$WorkDirectory=[IO.Path]::GetFullPath($WorkDirectory)
$manifest=Join-Path $WorkDirectory 'manifest.json'
$flat=Join-Path $WorkDirectory 'flat'
$importedManifest=Join-Path $repo 'Work\TestSeamless\SourcePreparation\manifest-500.json'
if(-not $PSBoundParameters.ContainsKey('WorkDirectory') -and (Test-Path -LiteralPath $importedManifest)){$manifest=$importedManifest;$flat=Join-Path $repo 'Work\TestSeamless\SourcePreparation\flat-500';$WorkDirectory=Split-Path $manifest -Parent}
$resources=@(@{id="workspace:$repo";mode='S'},@{id="profile:$WorkDirectory";mode='X'},@{id='cpu:heavy';mode='X'})
function Invoke-Bgo([string]$name,[string]$executable,[string[]]$arguments,[object[]]$locks) {
    $spec=@{policyVersion='1';agent='tpce-preparation';application='tpce-preparation';operation=$name;classification='A';method='cli';requiresUserInput=$false;mayActivateWindow=$false;executable=$executable;arguments=$arguments;workingDirectory=$WorkDirectory;resources=$locks;waitTimeoutSeconds=3600;runTimeoutSeconds=7200}
    $path=Join-Path $WorkDirectory ($name+'.spec.json');$spec|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 -LiteralPath $path
    $reply=& $Bgo run --spec $path --state $BgoState|ConvertFrom-Json
    if($reply.outcome -ne 'Passed') {throw "BGO $name failed: $($reply.error); $($reply.task.artifactDirectory)"}
    return $reply.task
}
if(-not (Test-Path -LiteralPath $manifest)) {
    if((Test-Path -LiteralPath $WorkDirectory) -and @(Get-ChildItem -Force -LiteralPath $WorkDirectory).Count -ne 0) {throw 'Preparation directory is not empty and has no manifest; preserve it and select a new empty directory.'}
    New-Item -ItemType Directory -Force -Path $flat|Out-Null
    $egen=Join-Path $repo 'Work\TpcE\egen';$loader=Join-Path $repo 'Work\TpcE\egen-build\EGenLoader.exe'
    $egenArguments=@('-b','1','-c','1000','-t','1000','-f','500','-w','10','-i',((Join-Path $egen 'flat_in')+'\'),'-o',($flat+'\'),'-x')
    $generation=Invoke-Bgo 'tpce-generate' $loader $egenArguments $resources
    $check=Join-Path $WorkDirectory 'preflight.json'
    [void](Invoke-Bgo 'tpce-preflight' $Python @((Join-Path $PSScriptRoot 'preflight.py'),$flat,(Join-Path $PSScriptRoot 'tpce-tables.json'),$check) $resources)
    $powershell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $receipt=Join-Path $generation.artifactDirectory 'result.json'
    [void](Invoke-Bgo 'tpce-manifest' $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'New-GenerationManifest.ps1'),'-FlatDirectory',$flat,'-ManifestPath',$manifest,'-GenerationSpec',(Join-Path $WorkDirectory 'tpce-generate.spec.json'),'-GenerationReceipt',$receipt,'-PreflightResult',$check) $resources)
}
$powershell=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$sqlLocks=$resources+@(@{id="sqlserver:$Server";mode='X'})
[void](Invoke-Bgo 'tpce-import' $powershell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Prepare-TpcE.ps1'),'-Server',$Server,'-FlatDirectory',$flat,'-ManifestPath',$manifest) $sqlLocks)
Write-Output "TPC_E preparation verified. Manifest: $manifest"
