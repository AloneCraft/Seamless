[CmdletBinding()]
param(
    [string]$WorkRoot=('D:\CodexTemp\SeamlessTemplate-'+[guid]::NewGuid().ToString('N')),
    [string]$LogRoot=('F:\CodexTemp\SeamlessTemplate-'+[guid]::NewGuid().ToString('N')),
    [ValidateSet('Debug','Release')][string[]]$Configurations=@('Debug','Release'),
    [switch]$SourceOnly
)

$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
$template=Join-Path $PSScriptRoot 'Seamless.Application'
$runner=Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1'
foreach($directory in @($WorkRoot,$LogRoot)){
    if(-not [IO.Path]::IsPathFullyQualified($directory)-or [IO.Path]::GetPathRoot($directory)-eq 'C:\'){
        throw '作業・ログにはC以外の絶対パスが必要です。'
    }
    if(Test-Path -LiteralPath $directory){throw "未使用の検証ディレクトリが必要です: $directory"}
    [void][IO.Directory]::CreateDirectory($directory)
}
$env:TEMP=Join-Path $WorkRoot 'temp';$env:TMP=$env:TEMP
[IO.File]::Copy((Join-Path $template 'global.json'),(Join-Path $WorkRoot 'global.json'))
[void][IO.Directory]::CreateDirectory($env:TEMP)
$env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home'
$env:NUGET_PACKAGES='F:\CodexTemp\SeamlessManagement-20261102\packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $LogRoot 'http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_NOLOGO='1'
$dotnet=(Get-Command dotnet).Source
$steps=[Collections.Generic.List[object]]::new()

function Invoke-Step([string]$Name,[string[]]$CommandArguments){
    $receipt=& $runner -Executable $dotnet -Arguments $CommandArguments -WorkingDirectory $WorkRoot -ArtifactDirectory (Join-Path $LogRoot $Name) -TimeoutSeconds 1200 -MaxLogicalProcessors 8
    $steps.Add([pscustomobject]@{Name=$Name;Outcome=$receipt.Outcome;Receipt=$receipt.Task.artifactDirectory})
    if($receipt.Outcome-ne 'Passed'){
        foreach($file in @('stdout.log','stderr.log')){
            $path=Join-Path $receipt.Task.artifactDirectory $file
            if(Test-Path -LiteralPath $path){Get-Content -LiteralPath $path -Tail 35|Write-Host}
        }
        throw "$Name : $($receipt.Error)"
    }
    Write-Host "PASS $Name"
}

function Test-Application([string]$Name,[string]$Directory,[string]$AssemblyPrefix){
    $artifacts=Join-Path $WorkRoot "$Name-artifacts"
    foreach($configuration in $Configurations){
        Invoke-Step "$Name-build-$configuration" @('build',(Join-Path $Directory "$AssemblyPrefix.slnx"),'-c',$configuration,'--artifacts-path',$artifacts,'-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false',"-p:SeamlessRepositoryRoot=$repository")
        $executable=Join-Path $artifacts "bin/Server/$($configuration.ToLowerInvariant())/$AssemblyPrefix.Server.dll"
        Invoke-Step "$Name-smoke-$configuration" @($executable,'--self-test')
    }
}

try{
    Test-Application 'source' $template 'Seamless.Application'
    if(-not $SourceOnly){
        $hive=Join-Path $LogRoot 'template-hive'
        Invoke-Step 'template-install' @('new','install',$template,'--force','--debug:custom-hive',$hive)
        $generated=Join-Path $WorkRoot 'RenamedApplication'
        Invoke-Step 'template-generate' @('new','seamless','--name','RenamedApplication','--output',$generated,'--debug:custom-hive',$hive)
        [xml]$solution=Get-Content -LiteralPath (Join-Path $generated 'RenamedApplication.slnx') -Raw
        if(@($solution.Solution.Project).Count-ne 4){throw '生成したソリューションのプロジェクト数が4ではありません。'}
        foreach($project in $solution.Solution.Project){if(-not (Test-Path -LiteralPath (Join-Path $generated $project.Path))){throw "生成したプロジェクトがありません: $($project.Path)"}}
        Test-Application 'generated' $generated 'RenamedApplication'
    }
}finally{
    [IO.File]::WriteAllText((Join-Path $LogRoot 'summary.json'),($steps|ConvertTo-Json -Depth 5))
    Write-Host "Work: $WorkRoot"
    Write-Host "Logs: $LogRoot"
}
