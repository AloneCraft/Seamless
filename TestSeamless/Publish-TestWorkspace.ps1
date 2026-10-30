param([Parameter(Mandatory)][string]$GenerationDirectory,[Parameter(Mandatory)][string]$PackageVersion,[Parameter(Mandatory)][string]$Feed,[Parameter(Mandatory)][string]$PackageCache,[string]$ComparisonPackageRoot)
$ErrorActionPreference='Stop'
$manifest=Get-Content -Raw -LiteralPath (Join-Path $GenerationDirectory 'generation.json')|ConvertFrom-Json
if($manifest.State -notin @('Validated','Active') -or $manifest.ProductVersion -ne $PackageVersion){throw 'The workspace requires the exact validated package/generation pair.'}
$target=Join-Path $PSScriptRoot 'Comparison.Local.props'
if((Test-Path -LiteralPath $target) -and -not ([IO.File]::ReadAllText($target).Contains('Seamless.Comparison.Generated'))){throw 'The local comparison properties are not script-owned.'}
$document=New-Object Xml.XmlDocument
[void]$document.AppendChild($document.CreateComment('Seamless.Comparison.Generated'))
$project=$document.CreateElement('Project');[void]$document.AppendChild($project);$group=$document.CreateElement('PropertyGroup');[void]$project.AppendChild($group)
foreach($pair in @(@('ComparisonPackageRoot',$ComparisonPackageRoot),@('SeamlessPackageVersion',$PackageVersion),@('GenerationDirectory',$GenerationDirectory),@('RestoreSources',($Feed+';'+$PackageCache)))){$item=$document.CreateElement($pair[0]);$item.SetAttribute('Condition',("'`$({0})'==''" -f $pair[0]));$item.InnerText=$pair[1];[void]$group.AppendChild($item)}
$temporary=$target+'.tmp.'+[Guid]::NewGuid().ToString('N');$document.Save($temporary)
try{
    & (Join-Path $PSScriptRoot 'Ssms\Update-SsmsWorkspace.ps1') -Operation Publish -StagingDirectory (Join-Path $GenerationDirectory 'ssms-stage') -OutputDirectory (Join-Path $PSScriptRoot 'Ssms')
    if(Test-Path -LiteralPath $target){[IO.File]::Replace($temporary,$target,($target+'.backup'),$true)}else{[IO.File]::Move($temporary,$target)}
}finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}}
