param([switch]$Direct,[switch]$UnderLease,[string]$RunId,[string]$PackageVersion,[string]$PackageFeed,[ValidateSet("Debug","Release")][string]$Configuration="Debug")
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
& (Join-Path $repo 'TestSeamless/Initialize-Storage.ps1')|Out-Null
$archive=$env:SEAMLESS_COMPARISON_ARCHIVE_ROOT;$temp=$env:SEAMLESS_COMPARISON_TEMP_ROOT
if(-not $PackageVersion -or -not $PackageFeed){
    $latest=Get-Content -Raw -LiteralPath (Join-Path $archive 'latest-packages.json')|ConvertFrom-Json
    $PackageVersion=$latest.Version;$PackageFeed=$latest.Feed
}
if(-not $PackageVersion -or -not (Test-Path -LiteralPath $PackageFeed)){throw 'A valid package version and feed are required.'}
if(-not $RunId){$RunId=[Guid]::NewGuid().ToString('N')}
if($RunId -notmatch '^[a-f0-9]{32}$'){throw 'Invalid run ID.'}
$fixture=Join-Path (Split-Path $temp -Parent) ("TypedQueryFixture/"+$RunId)
$results=Join-Path $archive ("TypedQueryFixture/"+$RunId)
New-Item -ItemType Directory -Force -Path $results|Out-Null
if(-not $UnderLease -and -not $Direct){
    if(-not $env:BGO_MAILBOX){throw 'BGO_MAILBOX must name the registered private mailbox.'}
    $bgo=$env:SEAMLESS_BGO_EXE
    if(-not $bgo){$bgo='G:/GitHub/CodexBackgroundOperations/src/BackgroundOps/bin/Release/net10.0-windows/BackgroundOps.exe'}
    if(-not (Test-Path -LiteralPath $bgo)){throw 'The BGO CLI is unavailable; use -Direct only on an isolated CI host.'}
    $shell=(Get-Process -Id $PID).Path
    $spec=@{policyVersion='1';agent='seamless-typed-query-fixture';application='dotnet';operation="typed-query-fixture-$RunId";classification='A';method='cli';requiresUserInput=$false;mayActivateWindow=$false;executable=$shell;arguments=@('-NoProfile','-File',$PSCommandPath,'-UnderLease','-RunId',$RunId,'-PackageVersion',$PackageVersion,'-PackageFeed',$PackageFeed,'-Configuration',$Configuration);workingDirectory=$repo;resources=@(@{id="workspace:$repo";mode='S'},@{id='cpu:heavy';mode='X'},@{id="build-dir:$fixture";mode='X'},@{id="profile:$archive";mode='X'},@{id='port:35479';mode='X'});waitTimeoutSeconds=7200;runTimeoutSeconds=1800}
    $specPath=Join-Path $results 'run.spec.json';$spec|ConvertTo-Json -Depth 8|Set-Content -Encoding utf8 $specPath
    $state=$env:BGO_STATE;if(-not $state){$state='D:\CodexBackgroundOps'}
    $reply=& $bgo run --spec $specPath --state $state|ConvertFrom-Json
    $reply|ConvertTo-Json -Depth 12|Set-Content -Encoding utf8 (Join-Path $results 'receipt.json')
    if($reply.outcome -ne 'Passed'){throw "Fixture failed: $results"}
    Write-Output "Passed: $results";return
}
New-Item -ItemType Directory -Force -Path $fixture,(Join-Path $fixture 'bootstrap'),(Join-Path $fixture 'runtime')|Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Bootstrap.cs.template') -Destination (Join-Path $fixture 'bootstrap/Program.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Runtime.cs.template') -Destination (Join-Path $fixture 'runtime/Program.cs')
$writer=[Security.SecurityElement]::Escape((Join-Path $repo 'TestSeamless/QueryModuleWriter.cs'))
$generation=[Security.SecurityElement]::Escape((Join-Path $fixture 'generation/queries/*.cs'))
$bootstrap='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include="Seamless.Database.Client" Version="$(SeamlessPackageVersion)"/><PackageReference Include="Seamless.Database.Rdbms" Version="$(SeamlessPackageVersion)"/><Compile Include="'+$writer+'"/></ItemGroup></Project>'
$runtime='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>14</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><InterceptorsNamespaces>$(InterceptorsNamespaces);Intercept</InterceptorsNamespaces><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles><CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated/$(Configuration)</CompilerGeneratedFilesOutputPath></PropertyGroup><ItemGroup><PackageReference Include="Seamless.Database" Version="$(SeamlessPackageVersion)"/><PackageReference Include="Seamless.Database.Gateway" Version="$(SeamlessPackageVersion)"/><PackageReference Include="Seamless.Database.Client" Version="$(SeamlessPackageVersion)"/><PackageReference Include="Seamless.FLinq.Generator" Version="$(SeamlessPackageVersion)" OutputItemType="Analyzer" ReferenceOutputAssembly="false"/><Compile Include="'+$generation+'"/></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $fixture 'bootstrap/Bootstrap.csproj'),$bootstrap)
[IO.File]::WriteAllText((Join-Path $fixture 'runtime/Runtime.csproj'),$runtime)
$config=Join-Path $results 'NuGet.Config';$dependencies=Join-Path $archive 'dependency-feed'
& (Join-Path $repo 'TestSeamless/Initialize-Storage.ps1')|Out-Null
& (Join-Path $repo 'TestSeamless/Write-RestoreConfiguration.ps1') -Sources @($PackageFeed,$dependencies,'https://api.nuget.org/v3/index.json') -Output $config
function Run-Dotnet([string]$name,[string[]]$arguments){
    & (Join-Path $repo 'TestSeamless/Initialize-Storage.ps1')|Out-Null
    & dotnet @arguments 2>&1|Tee-Object -FilePath (Join-Path $results ($name+'.log'))
    if($LASTEXITCODE -ne 0){throw "$name failed with exit $LASTEXITCODE"}
}
$common=@('-nr:false','-m:1','-p:UseSharedCompilation=false','-p:NuGetAudit=false',"-p:SeamlessPackageVersion=$PackageVersion","-p:RestoreConfigFile=$config")
Run-Dotnet 'bootstrap-build' (@('build',(Join-Path $fixture 'bootstrap/Bootstrap.csproj'),'-c',$Configuration)+$common)
Run-Dotnet 'bootstrap-generate' @((Join-Path $fixture "bootstrap/bin/$Configuration/net10.0/Bootstrap.dll"),(Join-Path $fixture 'generation'),$repo)
Run-Dotnet 'fixture-build' (@('build',(Join-Path $fixture 'runtime/Runtime.csproj'),'-c',$Configuration)+$common)
Run-Dotnet 'fixture-run' @((Join-Path $fixture "runtime/bin/$Configuration/net10.0/Runtime.dll"),(Join-Path $fixture 'generation'))
$generated=Get-ChildItem -Path (Join-Path $fixture "runtime/obj/generated/$Configuration") -Filter '*.cs' -Recurse
$hand=$generated|Where-Object {$_.Name -like '*Handwritten*'}|Select-Object -First 1
$count=$generated|Where-Object {$_.Name -like '*QueryModule*'}|Select-Object -First 1
if(-not $hand -or -not $count){throw 'Expected FLinq generated sources are missing.'}
$handText=[IO.File]::ReadAllText($hand.FullName);$countText=[IO.File]::ReadAllText($count.FullName)
if(-not $handText.Contains('InterceptsLocation') -or -not $handText.Contains('OptimizedLinq_CountMatching')){throw 'Handwritten FLinq interceptor is missing.'}
if(-not $countText.Contains('OptimizedLinq_Query0') -or $countText -notmatch 'while\(rest_source\.MoveNext\(\)\)[\s\S]*?__StaticMethod_TypedQueries_CheckCancellation'){throw 'COUNT per-row cancellation interceptor is missing.'}
[pscustomobject]@{PackageVersion=$PackageVersion;Configuration=$Configuration;Status='Passed';CountInterceptor=$count.FullName;HandwrittenInterceptor=$hand.FullName;FixtureDirectory=$fixture}|ConvertTo-Json|Set-Content -Encoding utf8 (Join-Path $results 'summary.json')
