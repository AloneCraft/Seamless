param([Parameter(Mandatory)][string]$Plan)
$ErrorActionPreference='Stop'
$settings=Get-Content -Raw -LiteralPath $Plan|ConvertFrom-Json
$restoreConfig=Join-Path $settings.Feed 'NuGet.Config'
& (Join-Path $PSScriptRoot 'Write-RestoreConfiguration.ps1') -Sources @($settings.Sources.Split(';')) -Output $restoreConfig
$artifactArguments=@();if($settings.PSObject.Properties['ArtifactsPath'] -and $settings.ArtifactsPath){$artifactArguments=@('--artifacts-path',$settings.ArtifactsPath)}
$buildArguments=@()
if($settings.PSObject.Properties['NoBuild'] -and $settings.NoBuild){$buildArguments=@('--no-build','--no-restore','-p:BuildProjectReferences=false')}
foreach($project in $settings.Projects){
    & $settings.Dotnet pack $project @artifactArguments @buildArguments --configuration $settings.Configuration --output $settings.Feed --nologo -m:1 -nr:false -p:UseSharedCompilation=false "-p:Version=$($settings.Version)" "-p:PackageVersion=$($settings.Version)" -p:IsPackable=true -p:IncludeSymbols=false -p:NuGetAudit=false "-p:RestoreConfigFile=$restoreConfig"
    if($LASTEXITCODE -ne 0){throw "Product package failed: $project"}
}
