param([Parameter(Mandatory=$true)][string]$TestRoot)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
$update=Join-Path $PSScriptRoot 'Update-SsmsWorkspace.ps1'
if(Test-Path -LiteralPath $TestRoot){throw 'Use a fresh test root.'}
[void](New-Item -ItemType Directory -Path $TestRoot)
$published=Join-Path $TestRoot 'published'
$results=New-Object 'System.Collections.Generic.List[object]'
function Check([string]$Name,[bool]$Condition){if(-not $Condition){throw "Failed: $Name"};$results.Add(@{Name=$Name;Status='Passed'})}
function Manifest([string]$Id,[string[]]$Names){
    $path=Join-Path $TestRoot ($Id+'.json')
    $databases=@($Names|ForEach-Object {@{Name=$_;Catalog=@{Schemas=@(@{Name='odd]schema';Tables=@(@{Name='class'});Views=@(@{Name='view name'})})}}})
    @{Id=$Id;SourceServer='localhost';SourceIntegratedSecurity=$true;Databases=$databases}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}
function Stage([string]$Id,[string[]]$Names){$stage=Join-Path $TestRoot $Id;$manifest=Manifest $Id $Names;& $update -Operation Stage -GenerationManifest $manifest -OutputDirectory $stage -PublishDirectory $published|Out-Null;return $stage}
function Must-Fail([string]$Name,[scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $Name $failed}
function Get-Sha256([string]$Path) {
    $algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose();$algorithm.Dispose() }
}
function Hashes([string]$Root){return (@(Get-ChildItem -LiteralPath $Root -File -Recurse|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+(Get-Sha256 $_.FullName)}) -join "`n")}
$env:ConnectionStrings__Source=$null
$first=Stage 'first' @('first database','keep')
Check 'Stage has no publication side effect' (-not (Test-Path -LiteralPath $published))
& $update -Operation Publish -StagingDirectory $first -OutputDirectory $published|Out-Null
& $update -Operation Validate -OutputDirectory $published|Out-Null
Check 'Manifest metadata works without environment connection string' (Test-Path -LiteralPath (Join-Path $published '.seamless-ssms-owner'))
$old=Get-Content -Raw -LiteralPath (Join-Path $published 'managed-ssms-files.json')|ConvertFrom-Json
[xml]$firstProject=[IO.File]::ReadAllText((Join-Path $first 'SQLServer\SQLServer.ssmssqlproj'))
$catalogNode=$firstProject.SelectSingleNode('//FileNode[@Name="first database Catalog.sql"]/FullPath')
if($null -eq $catalogNode){throw 'First database catalog query is missing.'}
$firstDatabaseSegment=([string]$catalogNode.InnerText).Split('\')[1]
$handwritten=Join-Path $published 'SQLServer\Generated\handwritten.sql'
[IO.File]::WriteAllText($handwritten,'SELECT 123;')
$second=Stage 'second' @('keep')
& $update -Operation Publish -StagingDirectory $second -OutputDirectory $published|Out-Null
$new=Get-Content -Raw -LiteralPath (Join-Path $published 'managed-ssms-files.json')|ConvertFrom-Json
$removed=@($old.Files|Where-Object {$new.Files.Path -notcontains $_.Path})
$expectedRetired=@($old.Files|Where-Object {$_.Path -like ('SQLServer\Generated\'+$firstDatabaseSegment+'\*') -or $_.Path -like ('SeamlessDB\Generated\'+$firstDatabaseSegment+'\*')})
Check 'Deleted database has generated files to retire' ($expectedRetired.Count -gt 0 -and $removed.Count -eq $expectedRetired.Count -and @($removed|Where-Object {$expectedRetired.Path -cnotcontains $_.Path}).Count -eq 0)
Check 'Only obsolete ledger entries are removed' (@($removed|Where-Object {Test-Path -LiteralPath (Join-Path $published $_.Path)}).Count -eq 0)
$retiredDirectories=@('SQLServer','SeamlessDB')|ForEach-Object {Join-Path $published ($_+'\Generated\'+$firstDatabaseSegment)}
Check 'Deleted database generated directories are removed when empty' (@($retiredDirectories|Where-Object {Test-Path -LiteralPath $_}).Count -eq 0)
Check 'Handwritten SQL survives publication' ([IO.File]::ReadAllText($handwritten) -ceq 'SELECT 123;')
$sql=@($new.Files|Where-Object Path -like '*.sql')[0]
[IO.File]::AppendAllText((Join-Path $published $sql.Path),'-- user edit')
$third=Stage 'third' @('replacement')
$before=Hashes $published
Must-Fail 'Edited managed SQL prevents destructive publication' {& $update -Operation Publish -StagingDirectory $third -OutputDirectory $published}
Check 'Failed preflight leaves all target files unchanged' ((Hashes $published) -ceq $before)
$bad=Join-Path $third 'managed-ssms-files.json'
$badDocument=Get-Content -Raw -LiteralPath $bad|ConvertFrom-Json
$badDocument.Files[0].Path='..\outside.sql'
$badDocument|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $bad -Encoding UTF8
Must-Fail 'Ledger traversal is rejected' {& $update -Operation Validate -OutputDirectory $third}
$fourth=Stage 'fourth' @('keep')
$badDocument=Get-Content -Raw -LiteralPath (Join-Path $fourth 'managed-ssms-files.json')|ConvertFrom-Json
$project=@($badDocument.Files|Where-Object Path -like '*SQLServer.ssmssqlproj')[0]
$projectPath=Join-Path $fourth $project.Path
[xml]$xml=[IO.File]::ReadAllText($projectPath)
$xml.SelectSingleNode('//FileNode/FullPath').InnerText='..\..\missing.sql'
$xml.Save($projectPath)
$project.Sha256=(Get-Sha256 $projectPath)
$badDocument|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $fourth 'managed-ssms-files.json') -Encoding UTF8
Must-Fail 'Missing or foreign XML reference is rejected' {& $update -Operation Validate -OutputDirectory $fourth}
$fifth=Stage 'fifth' @('keep')
$ownerPath=Join-Path $fifth '.seamless-ssms-owner'
[IO.File]::WriteAllText($ownerPath,'another-owner')
Must-Fail 'Wrong workspace owner is rejected' {& $update -Operation Validate -OutputDirectory $fifth}
$sixth=Stage 'sixth' @('keep')
$sixthLedgerPath=Join-Path $sixth 'managed-ssms-files.json'
$sixthLedger=Get-Content -Raw -LiteralPath $sixthLedgerPath|ConvertFrom-Json
$sixthLedger.Files=@($sixthLedger.Files)+@($sixthLedger.Files[0])
$sixthLedger|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $sixthLedgerPath -Encoding UTF8
Must-Fail 'Duplicate ledger paths are rejected' {& $update -Operation Validate -OutputDirectory $sixth}
$seventh=Stage 'seventh' @('keep')
$link=Join-Path $seventh 'SQLServer\Generated'
$real=Join-Path $seventh 'SQLServer\Generated.real'
Move-Item -LiteralPath $link -Destination $real
$junctionCreated=$false
try { $junction=New-Item -ItemType Junction -Path $link -Target $real;$junctionCreated=$true } catch { $results.Add(@{Name='Reparse points in managed paths are rejected';Status='Unavailable';Reason=$_.Exception.Message}) }
if($junctionCreated) { Must-Fail 'Reparse points in managed paths are rejected' {& $update -Operation Validate -OutputDirectory $seventh} }
$adventure=Stage 'adventure' @('AdventureWorks2016','keep')
foreach($side in @('SQLServer','SeamlessDB')) {
    [xml]$project=[IO.File]::ReadAllText((Join-Path $adventure ($side+'\'+$side+'.ssmssqlproj')))
    $paths=@($project.SelectNodes('//FileNode/FullPath')|ForEach-Object {$_.InnerText})
    Check ($side+' references AdventureWorks Basic') (@($paths|Where-Object {$_ -like '*AdventureWorks\Basic.sql'}).Count -eq 1)
    $methodCount=@($paths|Where-Object {$_ -like '*AdventureWorks\GeneratedMethods.sql'}).Count
    $expectedMethods=0;if($side -ceq 'SeamlessDB'){$expectedMethods=1}
    Check ($side+' generated methods reference is side specific') ($methodCount -eq $expectedMethods)
    $catalogNodes=@($project.SelectNodes('//FileNode')|Where-Object {$_.GetAttribute('Name') -like '* Catalog.sql'})
    Check ($side+' has a catalog query for every database') ($catalogNodes.Count -eq 2)
    foreach($catalog in $catalogNodes){
        $catalogSql=[IO.File]::ReadAllText((Join-Path (Join-Path $adventure $side) ($catalog.SelectSingleNode('FullPath').InnerText)))
        Check ($side+' catalog names its target database and modules: '+($catalog.GetAttribute('Name'))) ($catalogSql -match 'USE \[' -and $catalogSql -match 'sys\.objects' -and $catalogSql -match 'sys\.sql_modules')
    }
}
[xml]$withoutAdventure=[IO.File]::ReadAllText((Join-Path $first 'SQLServer\SQLServer.ssmssqlproj'))
Check 'AdventureWorks files omitted when database absent' (@($withoutAdventure.SelectNodes('//FileNode/FullPath')|Where-Object {$_.InnerText -like '*AdventureWorks\*'}).Count -eq 0)
$wrongSide=Stage 'wrong-side' @('AdventureWorks2016')
$wrongLedgerPath=Join-Path $wrongSide 'managed-ssms-files.json'
$wrongLedger=Get-Content -Raw -LiteralPath $wrongLedgerPath|ConvertFrom-Json
$sourceProjectPath=Join-Path $wrongSide 'SeamlessDB\SeamlessDB.ssmssqlproj'
$targetProjectPath=Join-Path $wrongSide 'SQLServer\SQLServer.ssmssqlproj'
[xml]$sourceProject=[IO.File]::ReadAllText($sourceProjectPath)
[xml]$targetProject=[IO.File]::ReadAllText($targetProjectPath)
$methodNode=$sourceProject.SelectSingleNode('//FileNode[@Name="AdventureWorks GeneratedMethods.sql"]')
if($null -eq $methodNode){throw 'GeneratedMethods source reference is missing.'}
$targetItems=$targetProject.SelectSingleNode('//LogicalFolder[@Name="Queries"]/Items')
[void]$targetItems.AppendChild($targetProject.ImportNode($methodNode,$true))
$targetProject.Save($targetProjectPath)
$targetEntry=@($wrongLedger.Files|Where-Object {$_.Path -ceq 'SQLServer\SQLServer.ssmssqlproj'})[0]
$targetEntry.Sha256=Get-Sha256 $targetProjectPath
$wrongLedger|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $wrongLedgerPath -Encoding UTF8
Must-Fail 'SQLServer rejects Seamless-only generated method reference' {& $update -Operation Validate -OutputDirectory $wrongSide}
$results|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $TestRoot 'results.json') -Encoding UTF8
Write-Output ('Passed: '+@($results|Where-Object Status -eq 'Passed').Count+'; Unavailable: '+@($results|Where-Object Status -eq 'Unavailable').Count+' SSMS publication checks.')
