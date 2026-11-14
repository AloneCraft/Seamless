[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [string]$DataRoot='D:/CodexTemp/datagrip-stage1-20261102-01',
    [string]$CacheRoot='F:/CodexTemp/datagrip-stage1-20261102-01'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-')){throw 'Use isolated D:/ and F:/ stage1 roots.'}
& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
[IO.Directory]::CreateDirectory($DataRoot) | Out-Null
[IO.Directory]::CreateDirectory($CacheRoot) | Out-Null
$buildLock=[IO.FileStream]::new((Join-Path $CacheRoot '.build.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    $env:TEMP=Join-Path $DataRoot 'tmp';$env:TMP=$env:TEMP
    [IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    $env:DOTNET_CLI_HOME=Join-Path $CacheRoot 'cli';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';$env:MSBUILDDISABLENODEREUSE='1'
    $env:NUGET_PACKAGES='F:/CodexTemp/nuget-packages';$env:NUGET_HTTP_CACHE_PATH=Join-Path $CacheRoot 'http';$env:NUGET_SCRATCH=Join-Path $CacheRoot 'nuget-tmp'
    $props=Join-Path $CacheRoot 'Stage1.Build.props'
    $content=@'
<Project>
  <Import Project="G:/GitHub/Seamless/Directory.Build.props" />
  <PropertyGroup>
    <BaseIntermediateOutputPath>__CACHE__/obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
    <MSBuildProjectExtensionsPath>$(BaseIntermediateOutputPath)</MSBuildProjectExtensionsPath>
    <BaseOutputPath>__DATA__/bin/$(MSBuildProjectName)/</BaseOutputPath>
    <DefaultItemExcludes>$(DefaultItemExcludes);**/obj/**;**/bin/**</DefaultItemExcludes>
    <UseSharedCompilation>false</UseSharedCompilation>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
'@
    [IO.File]::WriteAllText($props,$content.Replace('__CACHE__',$CacheRoot).Replace('__DATA__',$DataRoot))
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command dotnet).Source -Arguments @('build',"$PSScriptRoot/Server/DataGrip.Stage1.Server.csproj",'-c',$Configuration,'--nologo','-m:1',('-p:DirectoryBuildPropsPath='+$props),'-p:UseSharedCompilation=false') -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot "build-$Configuration") -TimeoutSeconds 900
    $result | ConvertTo-Json -Depth 6
    if($result.Outcome -ne 'Passed'){throw ('Build failed: '+$result.Error)}
    $classes=Join-Path $DataRoot 'jdbc/classes';[IO.Directory]::CreateDirectory($classes) | Out-Null
    $javac=(Get-Command javac).Source
    $testSources=@(Get-ChildItem -LiteralPath "$PSScriptRoot/jdbc/test/dev/seamless/jdbc" -Filter '*.java' | ForEach-Object FullName)
    $javaArguments=@('-J-XX:ActiveProcessorCount=8','--release','21','-encoding','UTF-8','-d',$classes,"$PSScriptRoot/jdbc/src/dev/seamless/jdbc/SeamlessDriver.java")+$testSources
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable $javac -Arguments $javaArguments -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'javac') -TimeoutSeconds 120
    if($result.Outcome -ne 'Passed'){throw ('javac failed: '+$result.Error)}
    $services=Join-Path $classes 'META-INF/services';[IO.Directory]::CreateDirectory($services) | Out-Null
    [IO.File]::WriteAllText((Join-Path $services 'java.sql.Driver'),"dev.seamless.jdbc.SeamlessDriver`n")
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Join-Path (Split-Path $javac) 'jar.exe') -Arguments @('-J-XX:ActiveProcessorCount=8','--create','--file',(Join-Path $DataRoot 'jdbc/seamless-jdbc-stage1.jar'),'-C',$classes,'.') -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'jar') -TimeoutSeconds 60
    if($result.Outcome -ne 'Passed'){throw ('jar failed: '+$result.Error)}
}finally{$buildLock.Dispose()}
