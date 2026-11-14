[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$WorkRoot='F:\Seamless\SourceWorkspace',
    [string]$LogRoot,
    [string]$Artifacts,
    [string]$PackageFallbackRoot
)
$ErrorActionPreference='Stop'
if(!$LogRoot){$LogRoot=Join-Path $WorkRoot ('checks-'+[guid]::NewGuid().ToString('N'))}
if(!$Artifacts){$Artifacts=Join-Path $WorkRoot 'artifacts'}
foreach($path in @($WorkRoot,$LogRoot,$Artifacts)){
    if(-not [IO.Path]::IsPathFullyQualified($path) -or ![IO.Path]::GetFullPath($path).StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
        throw 'WorkRoot、LogRoot、ArtifactsはF:\Seamless配下の絶対パスを指定してください。'
    }
}
if(Test-Path -LiteralPath $LogRoot){throw '新しいLogRootを指定してください。'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
$dotnet=(Get-Command dotnet).Source
$env:TEMP=Join-Path $WorkRoot 'temp'
$env:TMP=$env:TEMP
$env:DOTNET_CLI_HOME=$WorkRoot
$env:NUGET_PACKAGES=Join-Path $WorkRoot 'packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $WorkRoot 'http'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:MSBUILDDISABLENODEREUSE='1'
$env:SEAMLESS_MANAGEMENT_TEST_ROOT=Join-Path $WorkRoot ('ws'+[guid]::NewGuid().ToString('N').Substring(0,9))
$env:SEAMLESS_SOURCE_REPOSITORY=$repository
Remove-Item Env:SEAMLESS_MANAGEMENT_UI_TEST_EXE -ErrorAction SilentlyContinue
$env:SEAMLESS_MANAGEMENT_TEST_LOG=Join-Path $LogRoot "web-$Configuration"
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:SEAMLESS_MANAGEMENT_TEST_ROOT|Out-Null
$common=@('-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false')
if($PackageFallbackRoot){
    if(![IO.Path]::IsPathFullyQualified($PackageFallbackRoot) -or !(Test-Path -LiteralPath $PackageFallbackRoot -PathType Container)){
        throw 'PackageFallbackRootは既存キャッシュの絶対パスを指定してください。'
    }
    $common+=('-p:RestoreFallbackFolders='+[IO.Path]::GetFullPath($PackageFallbackRoot))
}
function Invoke-Check([string]$Name,[string[]]$CommandArguments,[string]$WorkingDirectory=$repository,[int]$TimeoutSeconds=360){
    $receipt=& $runner -Executable $dotnet -Arguments $CommandArguments -WorkingDirectory $WorkingDirectory -ArtifactDirectory (Join-Path $LogRoot "$Configuration-$Name") -TimeoutSeconds $TimeoutSeconds
    $receipt|ConvertTo-Json -Depth 8
    if($receipt.Outcome -ne 'Passed'){throw "$Name failed. See $LogRoot/$Configuration-$Name"}
}
$testBuild=@('--artifacts-path',$Artifacts)+$common
Invoke-Check 'web-build' (@('build','Seamless.Management.Server/WindowsServer.csproj','-c',$Configuration,'-p:RestoreIgnoreFailedSources=true','-v:q')+$testBuild)
Invoke-Check 'tests' (@('test','Tests/Management.Tests/Management.Tests.csproj','-c',$Configuration,'-p:RestoreIgnoreFailedSources=true','--filter','FullyQualifiedName~SourceWorkspaceTests|FullyQualifiedName~SourceProjects|FullyQualifiedName~SourceProjectDrafts|FullyQualifiedName~SourceProjectBuildInspection|FullyQualifiedName~SourceBuildArtifactStorage|FullyQualifiedName~SourceStructureTests|FullyQualifiedName~プロジェクト作成接続|FullyQualifiedName~文書状態|FullyQualifiedName~LocalBrowserSessionsTests|FullyQualifiedName~ManagerWebHostTests|FullyQualifiedName~DraftStartup|FullyQualifiedName~Management.Tests.DatabaseDesigner|FullyQualifiedName~RemovedTableTests|FullyQualifiedName~NativeRowEditingTests','--logger','trx;LogFileName=source.trx','--results-directory',(Join-Path $LogRoot "results-$Configuration"))+$testBuild) -TimeoutSeconds 540
[xml]$trx=Get-Content -LiteralPath (Join-Path $LogRoot "results-$Configuration/source.trx") -Raw
$counts=$trx.TestRun.ResultSummary.Counters
if([int]$counts.passed -eq 0 -or [int]$counts.passed -ne [int]$counts.total){
    throw 'ソース連携試験が0件、または全件合格を確認できません。'
}
$manifests=@(Get-ChildItem -LiteralPath $env:SEAMLESS_MANAGEMENT_TEST_LOG -Recurse -Filter '*.workspace.json')
if($manifests.Count -ne 1){throw '新規DB用プロジェクトの生成記録が一つ必要です。'}
$number=0
foreach($manifest in $manifests){
    $workspace=Get-Content -LiteralPath $manifest.FullName -Raw|ConvertFrom-Json
    if(-not ([IO.Path]::GetFullPath($workspace.project)).StartsWith(([IO.Path]::GetFullPath($WorkRoot)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){throw 'Workspace escaped the test root.'}
    $directory=Split-Path $workspace.project
    $taskFile=Get-Content -LiteralPath (Join-Path $directory '.vscode/tasks.json') -Raw|ConvertFrom-Json
    $tasks=@($taskFile.tasks|Where-Object {$_.group.kind -eq 'build' -and $_.group.isDefault})
    if($tasks.Count -ne 1 -or $tasks[0].type -cne 'process' -or $tasks[0].command -cne 'dotnet' -or
        $tasks[0].args.Count -ne 2 -or $tasks[0].args[0] -cne 'build' -or $tasks[0].args[1] -cne [IO.Path]::GetFileName($workspace.project)){
        throw '生成したVS Codeタスクの既定ビルドが想定した形式と一致しません。'
    }
    $number++
    $projectArtifacts=Join-Path $LogRoot "project-$number-artifacts"
    Invoke-Check "project-build-$number" (@($tasks[0].args)+@('-c',$Configuration,'--artifacts-path',$projectArtifacts,'-p:RestoreIgnoreFailedSources=true','-v:q')+$common) $directory
}
