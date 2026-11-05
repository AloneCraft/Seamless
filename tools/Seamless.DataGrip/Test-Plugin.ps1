[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$CacheRoot,
    [string]$IdeRoot='C:/Program Files/JetBrains/DataGrip 2026.1.3',
    [ValidatePattern('^ide-[a-zA-Z0-9-]+$')][string]$RunName=('ide-'+[guid]::NewGuid().ToString('N').Substring(0,8))
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-')){throw 'Use dedicated D/F roots.'}
& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
$sandbox=Join-Path $DataRoot $RunName;$artifact=Join-Path $CacheRoot $RunName
if((Test-Path -LiteralPath $sandbox) -or (Test-Path -LiteralPath $artifact)){throw 'Use a new private IDE run; existing profiles are never replaced.'}
[IO.Directory]::CreateDirectory($sandbox) | Out-Null;[IO.Directory]::CreateDirectory($artifact) | Out-Null
$ownership=[IO.FileStream]::new((Join-Path $sandbox '.owner.lock'),[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$previousEnvironment=@{}
foreach($name in @('TEMP','TMP','APPDATA','LOCALAPPDATA')){$previousEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
    foreach($pair in @(@('seamless','seamless-datagrip-stage2.jar'),@('seamless-check','seamless-datagrip-acceptance.jar'))){
        $directory=Join-Path $sandbox ('plugins/'+$pair[0]+'/lib');[IO.Directory]::CreateDirectory($directory) | Out-Null
        Copy-Item -LiteralPath (Join-Path $DataRoot ('plugin/'+$pair[1])) -Destination $directory
    }
    $product=Get-Content -LiteralPath (Join-Path $IdeRoot 'product-info.json') -Raw | ConvertFrom-Json
    if($product.productCode -ne 'DB' -or $product.buildNumber -notmatch '^261\.'){throw 'DataGrip 261 is required.'}
    $launch=$product.launch | Where-Object {$_.os -eq 'Windows' -and $_.arch -eq 'amd64'} | Select-Object -First 1
    $env:TEMP=Join-Path $sandbox 'tmp';$env:TMP=$env:TEMP;[IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    $env:APPDATA=Join-Path $sandbox 'roaming';$env:LOCALAPPDATA=Join-Path $artifact 'local'
    $options=@('-Xms128m','-Xmx750m','-XX:ActiveProcessorCount=8','-XX:+IgnoreUnrecognizedVMOptions')
    $options+=@($launch.additionalJvmArguments | ForEach-Object {$_.Replace('%IDE_HOME%',$IdeRoot)})
    $options+=@('-Djava.awt.headless=true',('-Didea.home.path='+$IdeRoot),('-Didea.config.path='+$sandbox+'/config'),('-Didea.system.path='+$artifact+'/system'),('-Didea.plugins.path='+$sandbox+'/plugins'),('-Didea.log.path='+$artifact+'/log'),('-Duser.home='+$sandbox+'/home'),('-Djava.io.tmpdir='+$env:TEMP),'-Didea.paths.selector=SeamlessDataGripStage2')
    $classpath=($launch.bootClassPathJarNames | ForEach-Object {Join-Path $IdeRoot ('lib/'+$_)}) -join ';'
    $output=Join-Path $artifact 'generated-update.sql'
    $options+=@('-cp',$classpath,$launch.mainClass,'seamless-stage2-check',$output)
    $argumentFile=Join-Path $artifact 'java.args'
    ($options | ForEach-Object {'"'+$_.Replace('\','\\').Replace('"','\"')+'"'}) | Set-Content -LiteralPath $argumentFile -Encoding utf8NoBOM
    [pscustomobject]@{pid=$PID;creationUtc=(Get-Process -Id $PID).StartTime.ToUniversalTime();sandbox=$sandbox;artifact=$artifact;build=$product.buildNumber} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sandbox 'owner.json') -Encoding utf8
    $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Join-Path $IdeRoot $launch.javaExecutablePath) -Arguments @('@'+$argumentFile) -WorkingDirectory $sandbox -ArtifactDirectory (Join-Path $artifact 'process') -TimeoutSeconds 90
    if($result.Outcome -ne 'Passed' -or -not (Test-Path -LiteralPath $output)){throw ('Headless IDE plugin acceptance failed: '+$result.Outcome)}
    [pscustomobject]@{ide=$product.version;build=$product.buildNumber;headlessPluginLoadVerified=$true;guiEditingVerified=$false;generatedUpdate=$output} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifact 'result.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $artifact 'result.json') -Raw
}finally{
    foreach($name in $previousEnvironment.Keys){[Environment]::SetEnvironmentVariable($name,$previousEnvironment[$name],'Process')}
    $ownership.Dispose()
}
