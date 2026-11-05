[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$WorkRoot='D:\sx1104',
    [string]$LogRoot='F:\CodexTemp\SeamlessSource-20261104-01',
    [string]$Artifacts='D:\CodexTemp\SeamlessAuth-20261103-01\artifacts'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
$dotnet=(Get-Command dotnet).Source
$env:TEMP='D:\CodexTemp\SeamlessAuth-20261103-01\temp'
$env:TMP=$env:TEMP
$env:DOTNET_CLI_HOME='F:\CodexTemp\SeamlessAuth-20261103-01\dotnet-home'
$env:NUGET_PACKAGES='F:\CodexTemp\SeamlessManagement-20261102\packages'
$env:NUGET_HTTP_CACHE_PATH='F:\CodexTemp\SeamlessAuth-20261103-01\http-cache'
$env:SEAMLESS_MANAGEMENT_TEST_ROOT=$WorkRoot
$env:SEAMLESS_SOURCE_REPOSITORY=$repository
$env:SEAMLESS_MANAGEMENT_UI_TEST_EXE=Join-Path $Artifacts "bin/SeamlessManager/$($Configuration.ToLowerInvariant())/Seamless.Management.UI.exe"
$env:SEAMLESS_MANAGEMENT_TEST_LOG=Join-Path $LogRoot "ui-$Configuration"
$common=@('--artifacts-path',$Artifacts,'-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false')
function Invoke-Check([string]$Name,[string[]]$CommandArguments){
    $receipt=& $runner -Executable $dotnet -Arguments $CommandArguments -WorkingDirectory $repository -ArtifactDirectory (Join-Path $LogRoot "$Configuration-$Name") -TimeoutSeconds 360
    $receipt|ConvertTo-Json -Depth 8
    if($receipt.Outcome -ne 'Passed'){throw "$Name failed. See $LogRoot/$Configuration-$Name"}
}
Invoke-Check 'gui-build' (@('build','Seamless.Management.UI/SeamlessManager.csproj','-c',$Configuration,'--no-restore','-v:q')+$common)
Invoke-Check 'tests' (@('test','Tests/Management.Tests/Management.Tests.csproj','-c',$Configuration,'--no-restore','--filter','FullyQualifiedName~SourceWorkspaceTests|FullyQualifiedName~ManagementServer.Connect|FullyQualifiedName~Management.Tests.DatabaseDesigner','--logger','trx;LogFileName=source.trx','--results-directory',(Join-Path $LogRoot "results-$Configuration"))+$common)
$manifests=@(Get-ChildItem -LiteralPath $env:SEAMLESS_MANAGEMENT_TEST_LOG -Recurse -Filter '*.workspace.json')
if($manifests.Count -ne 2){throw 'Expected two GUI-created workspaces.'}
$number=0
foreach($manifest in $manifests){
    $workspace=Get-Content -LiteralPath $manifest.FullName -Raw|ConvertFrom-Json
    if(-not ([IO.Path]::GetFullPath($workspace.project)).StartsWith(([IO.Path]::GetFullPath($WorkRoot)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){throw 'Workspace escaped the test root.'}
    $number++
    Invoke-Check "project-build-$number" (@('build',$workspace.project,'-c',$Configuration,'-p:RestoreIgnoreFailedSources=true','-v:q')+$common)
}
