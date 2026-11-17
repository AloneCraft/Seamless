[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [ValidateSet('Conversion','Verification','Tool','All')][string]$Suite='All',
    [string]$DataRoot='D:/CodexTemp/Seamless/RoslynRoundtrip',
    [string]$CacheRoot='F:/CodexTemp/Seamless/RoslynRoundtrip',
    [string]$Filter
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($path in @($DataRoot,$CacheRoot)){
    if(-not [IO.Path]::IsPathFullyQualified($path) -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($path)) -eq 'C:\'){throw 'Use absolute non-C output directories.'}
    [IO.Directory]::CreateDirectory($path)|Out-Null
}
$lock=[IO.File]::Open((Join-Path $DataRoot 'build.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    & "$repository/TestSeamless/Initialize-Storage.ps1" -ArchiveRoot $CacheRoot -TemporaryRoot (Join-Path $DataRoot 'tmp')|Out-Null
    $env:NUGET_PACKAGES='F:/CodexTemp/nuget-packages'
    $env:MSBUILDDISABLENODEREUSE='1'
    $dotnet=(Get-Command dotnet).Source
    $sdkRoot=Join-Path (Split-Path $dotnet -Parent) 'sdk'
    $sdk=Get-ChildItem -LiteralPath $sdkRoot -Directory | Where-Object Name -Match '^10\.0\.\d+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if(-not $sdk){throw '.NET 10 SDK is required.'}
    [IO.File]::WriteAllText((Join-Path $DataRoot 'global.json'),(@{sdk=@{version=$sdk.Name;rollForward='latestPatch'}}|ConvertTo-Json -Depth 3))
    $props=Join-Path $CacheRoot 'Roundtrip.Build.props'
    $xml=@'
<Project>
  <Import Project="__REPO__/Directory.Build.props" />
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
    [IO.File]::WriteAllText($props,$xml.Replace('__REPO__',$repository.Replace('\','/')).Replace('__CACHE__',$CacheRoot.Replace('\','/')).Replace('__DATA__',$DataRoot.Replace('\','/')))
    $projects=@()
    if($Suite -in @('Conversion','Verification','All')){$projects+="$repository/Tests/Database.Query.Tests/Database.Query.Tests.csproj"}
    if($Suite -in @('Tool','All')){$projects+="$repository/Seamless/Database/Server/Server.csproj"}
    foreach($project in $projects){
        $command='test';if([IO.Path]::GetFileNameWithoutExtension($project) -eq 'Seamless.Database.Tool'){$command='build'}
        $arguments=@($command,$project,'-c',$Configuration,'--nologo','-m:1',('-p:DirectoryBuildPropsPath='+$props),'-p:UseSharedCompilation=false')
        if($command -eq 'test'){
            $arguments+=@('--logger','trx','--results-directory',(Join-Path $CacheRoot "results-$Configuration"))
            $purpose='FullyQualifiedName~Database.Compilation.Tests|FullyQualifiedName~Database.Verification.Tests'
            if($Suite -eq 'Conversion'){$purpose='FullyQualifiedName~Database.Compilation.Tests'}
            if($Suite -eq 'Verification'){$purpose='FullyQualifiedName~Database.Verification.Tests'}
            if($Filter){$arguments+=@('--filter',"($purpose)&($Filter)")}else{$arguments+=@('--filter',"($purpose)&Category!=LiveSqlServer")}
        }
        $run=Join-Path $CacheRoot ('test-'+[IO.Path]::GetFileNameWithoutExtension($project)+'-'+$Configuration+'-'+[guid]::NewGuid().ToString('N'))
        $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable $dotnet -Arguments $arguments -WorkingDirectory $DataRoot -ArtifactDirectory $run -TimeoutSeconds 900
        $result|ConvertTo-Json -Depth 6
        Get-Content -LiteralPath (Join-Path $run 'stdout.log') -Tail 45
        Get-Content -LiteralPath (Join-Path $run 'stderr.log') -Tail 15
        if($result.Outcome -ne 'Passed'){throw "Roundtrip tests failed: $run"}
    }
}finally{$lock.Dispose()}
