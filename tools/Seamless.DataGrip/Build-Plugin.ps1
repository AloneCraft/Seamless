[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$CacheRoot,
    [string]$IdeRoot='C:/Program Files/JetBrains/DataGrip 2026.1.3'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-')){throw 'Use dedicated D/F roots.'}
& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
[IO.Directory]::CreateDirectory($DataRoot) | Out-Null
[IO.Directory]::CreateDirectory($CacheRoot) | Out-Null
$lock=[IO.FileStream]::new((Join-Path $CacheRoot '.plugin-build.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
    $product=Get-Content -LiteralPath (Join-Path $IdeRoot 'product-info.json') -Raw | ConvertFrom-Json
    if($product.productCode -ne 'DB' -or $product.buildNumber -notmatch '^261\.'){throw 'This experimental plugin is limited to DataGrip build 261.'}
    $env:TEMP=Join-Path $DataRoot 'tmp';$env:TMP=$env:TEMP;[IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    $classes=Join-Path $DataRoot 'plugin/classes';$testClasses=Join-Path $DataRoot 'plugin/test-classes'
    [IO.Directory]::CreateDirectory($classes) | Out-Null;[IO.Directory]::CreateDirectory($testClasses) | Out-Null
    $sdk=(Join-Path $IdeRoot 'lib/*')+';'+(Join-Path $IdeRoot 'plugins/DatabaseTools/lib/database-plugin.jar')+';'+(Join-Path $IdeRoot 'plugins/DatabaseTools/lib/modules/*')
    $sources=@(Get-ChildItem -LiteralPath "$PSScriptRoot/plugin/src/dev/seamless/datagrip" -Filter '*.java' | ForEach-Object FullName)
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command javac).Source -Arguments (@('-J-XX:ActiveProcessorCount=8','--release','21','-encoding','UTF-8','-cp',$sdk,'-d',$classes)+$sources) -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'plugin-javac') -TimeoutSeconds 60
    if($result.Outcome -ne 'Passed'){throw 'SDK plugin compilation failed.'}
    Copy-Item -LiteralPath "$PSScriptRoot/plugin/resources/META-INF" -Destination $classes -Recurse -Force
    $tests=@(Get-ChildItem -LiteralPath "$PSScriptRoot/plugin/test/dev/seamless/datagrip" -Filter '*.java' | ForEach-Object FullName)
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command javac).Source -Arguments (@('-J-XX:ActiveProcessorCount=8','--release','21','-encoding','UTF-8','-cp',($classes+';'+$sdk),'-d',$testClasses)+$tests) -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'plugin-test-javac') -TimeoutSeconds 60
    if($result.Outcome -ne 'Passed'){throw 'SDK test compilation failed.'}
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command java).Source -Arguments @('-XX:ActiveProcessorCount=8','-Djava.awt.headless=true','-cp',($testClasses+';'+$classes+';'+$sdk),'dev.seamless.datagrip.SdkAcceptance') -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'plugin-sdk-test') -TimeoutSeconds 30
    if($result.Outcome -ne 'Passed'){throw 'SDK linkage/alias policy test failed.'}
    $jar=Join-Path $DataRoot 'plugin/seamless-datagrip-stage2.jar'
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command jar).Source -Arguments @('-J-XX:ActiveProcessorCount=8','--create','--file',$jar,'-C',$classes,'.') -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'plugin-jar') -TimeoutSeconds 30
    if($result.Outcome -ne 'Passed'){throw 'Plugin packaging failed.'}
    Copy-Item -LiteralPath "$PSScriptRoot/plugin/test-resources/META-INF" -Destination $testClasses -Recurse -Force
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command jar).Source -Arguments @('-J-XX:ActiveProcessorCount=8','--create','--file',(Join-Path $DataRoot 'plugin/seamless-datagrip-acceptance.jar'),'-C',$testClasses,'.') -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'plugin-test-jar') -TimeoutSeconds 30
    if($result.Outcome -ne 'Passed'){throw 'Test command packaging failed.'}
    [pscustomobject]@{version=$product.version;build=$product.buildNumber;artifact=$jar;sha256=(Get-FileHash -LiteralPath $jar).Hash;ideLoadVerified=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $CacheRoot 'plugin-build.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $CacheRoot 'plugin-build.json') -Raw
}finally{$lock.Dispose()}
