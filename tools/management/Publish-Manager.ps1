[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BundledSdkDirectory,
    [string]$WorkRoot='F:\Seamless\ManagerPublish',
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$PackageFallbackRoot
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$work=[IO.Path]::GetFullPath($WorkRoot)
$sdk=(Resolve-Path -LiteralPath $BundledSdkDirectory).ProviderPath
foreach($path in @($work,$sdk)){
    if(!$path.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw '作業領域とSDKはF:\Seamless配下を指定してください。'}
}
$manifest=Get-Content -LiteralPath (Join-Path $sdk 'bundled-sdk.json') -Raw|ConvertFrom-Json
if($manifest.runtimeIdentifier -ne 'win-x64' -or $manifest.files.Count -eq 0){throw 'Windows x64 SDKの検証記録が必要です。'}
$run=Join-Path $work ([guid]::NewGuid().ToString('N'))
$artifacts=Join-Path $run 'artifacts'
$distribution=Join-Path $run 'distribution'
New-Item -ItemType Directory -Path $run,$distribution|Out-Null
$env:TEMP=$run
$env:TMP=$run
$env:DOTNET_CLI_HOME=Join-Path $run 'cli'
$env:NUGET_PACKAGES=Join-Path $work 'packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $work 'http'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:MSBUILDDISABLENODEREUSE='1'
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
& $runner -InitializeOnly|Out-Null
foreach($file in $manifest.files){
    $path=[IO.Path]::GetFullPath((Join-Path $sdk $file.path))
    if(!$path.StartsWith($sdk+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path).Hash -cne $file.sha256){throw ('SDKの指紋が一致しません。 '+$file.path)}
}
$project=Join-Path $repository 'Seamless/Management/Manager.csproj'
$common=@($project,'-c',$Configuration,'-r','win-x64','--artifacts-path',$artifacts,'-p:PublishSingleFile=true','-p:SelfContained=true','-p:UseSharedCompilation=false','-p:NuGetAudit=false','-m:1')
if($PackageFallbackRoot){$common+=('-p:RestoreFallbackFolders='+[IO.Path]::GetFullPath($PackageFallbackRoot))}
function Invoke-PublishStage([string]$Name,[string[]]$CommandArguments){
    $result=& $runner -Executable (Get-Command dotnet).Source -Arguments $CommandArguments -WorkingDirectory $repository -ArtifactDirectory (Join-Path $run $Name) -TimeoutSeconds 3600
    if($result.Outcome -ne 'Passed'){throw ('Manager配布工程が失敗しました。 '+$Name)}
}
Invoke-PublishStage 'build' (@('build')+$common)
$configurationDirectory=$Configuration.ToLowerInvariant()+'_win-x64'
$built=Join-Path $artifacts ('bin/Manager/'+$configurationDirectory)
& (Join-Path $PSScriptRoot 'New-SourcePackageFeed.ps1') -PublishedDirectory $built -ArtifactsPath $artifacts -LogRoot (Join-Path $run 'sdk-feed') -Configuration $Configuration -RuntimeIdentifier win-x64 -SingleFile -NoBuild -PackageFallbackRoot $PackageFallbackRoot|Out-Host
$feed=Join-Path $built 'packages'
$cohort=Get-Content -LiteralPath (Join-Path $feed 'source-packages.json') -Raw|ConvertFrom-Json
$publish=Join-Path $run 'publish'
$arguments=@('publish')+$common+@('--no-build','-p:BuildProjectReferences=false',('-p:SeamlessBundledDotNetRoot='+$sdk),('-p:SeamlessSourcePackageFeed='+$feed),'-o',$publish)
Invoke-PublishStage 'publish-log' $arguments
foreach($assembly in $cohort.assemblies.PSObject.Properties){
    if((Get-FileHash -LiteralPath (Join-Path $built $assembly.Name)).Hash -cne $assembly.Value){throw ('発行中にSDKの入力が変わりました。 '+$assembly.Name)}
}
$executable=Join-Path $distribution 'Manager.exe'
Copy-Item -LiteralPath (Join-Path $publish 'Manager.exe') -Destination $executable
[ordered]@{configuration=$Configuration;runtime='win-x64';files=1;executable=$executable;bytes=(Get-Item -LiteralPath $executable).Length;sha256=(Get-FileHash -LiteralPath $executable).Hash;sdkVersion=$manifest.sdkVersion;sourcePackageVersion=$cohort.version}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'distribution.json')
Write-Output $distribution
