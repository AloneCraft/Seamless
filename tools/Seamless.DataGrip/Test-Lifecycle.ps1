[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$CacheRoot,
    [ValidateSet('Debug','Release')][string[]]$Configurations=@('Debug','Release'),
    [ValidatePattern('^run-[a-zA-Z0-9-]+$')][string]$RunPrefix=('run-context-'+[guid]::NewGuid().ToString('N').Substring(0,8)),
    [string]$BuildRoot,
    [ValidateSet('Stage1Acceptance','Stage1GuiProbe','Stage2JdbcAcceptance')][string[]]$TestClasses=@('Stage1Acceptance','Stage1GuiProbe','Stage2JdbcAcceptance')
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-')){throw 'Use dedicated stage1 roots.'}
if(-not $BuildRoot){$BuildRoot=$DataRoot}
if(-not $BuildRoot.StartsWith('D:/CodexTemp/datagrip-stage1-')){throw 'Use a dedicated build root.'}
& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
[IO.Directory]::CreateDirectory($CacheRoot) | Out-Null
$env:TEMP=Join-Path $DataRoot 'tmp';$env:TMP=$env:TEMP
foreach($configuration in $Configurations){
    $runName=$RunPrefix+'-'+$configuration.ToLowerInvariant()
    $run=Join-Path $DataRoot $runName
    if(Test-Path -LiteralPath $run){throw 'Choose a new DataRoot or use a fresh test run. Existing runs are never reset.'}
    foreach($reopen in @($false,$true)){
        $phase='first';if($reopen){$phase='reopen'}
        $artifact=Join-Path $CacheRoot ($runName+'-'+$phase);[IO.Directory]::CreateDirectory($artifact) | Out-Null
        $arguments=@('-NoProfile','-File',"$PSScriptRoot/Start-Stage1.ps1",'-DataRoot',$DataRoot,'-CacheRoot',$CacheRoot,'-BuildRoot',$BuildRoot,'-RunName',$runName,'-Configuration',$configuration)
        if($reopen){$arguments+='-Reopen'}
        $previousOwner=$null;if($reopen){$previousOwner=Get-Content -LiteralPath (Join-Path $run 'owner.json') -Raw | ConvertFrom-Json}
        $runner=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifact 'runner.stdout.log') -RedirectStandardError (Join-Path $artifact 'runner.stderr.log')
        $ready=$null
        try{
            for($attempt=0;$attempt -lt 120;$attempt++){
                if($runner.HasExited){throw 'Fixture runner exited before readiness.'}
                $path=Join-Path $run 'ready.json'
                if(Test-Path -LiteralPath $path){
                    $candidate=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
                    if(-not $previousOwner -or $candidate.pid -ne $previousOwner.pid -or ([datetime]$candidate.creationUtc).Ticks -ne ([datetime]$previousOwner.creationUtc).Ticks){$ready=$candidate;break}
                }
                Start-Sleep -Milliseconds 250
            }
            if(-not $ready){throw 'Fixture readiness timed out.'}
            foreach($class in $TestClasses){
                $result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command java).Source -Arguments @('-XX:ActiveProcessorCount=8','-cp',((Join-Path $DataRoot 'jdbc/classes')+';'+(Join-Path $BuildRoot 'jdbc/classes')),('dev.seamless.jdbc.'+$class),$ready.url) -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $artifact $class) -TimeoutSeconds 30
                [pscustomobject]@{run=$runName;configuration=$configuration;phase=$phase;test=$class;outcome=$result.Outcome} | ConvertTo-Json -Compress
                if($result.Outcome -ne 'Passed'){throw ($class+' failed: '+$result.Error)}
            }
        }finally{
            if($ready){
                $owned=Get-Process -Id $ready.pid -ErrorAction Stop
                if($owned.StartTime.ToUniversalTime().Ticks -ne ([datetime]$ready.creationUtc).ToUniversalTime().Ticks){throw 'Fixture process identity mismatch.'}
                [IO.File]::WriteAllText((Join-Path $run 'stop'),'stop')
            }
            if(-not $runner.WaitForExit(20000)){throw 'Fixture stop unconfirmed; ownership must be retained.'}
            if($runner.ExitCode -ne 0){throw 'Fixture runner failed during shutdown.'}
            Copy-Item -LiteralPath (Join-Path $CacheRoot ($runName+'-process/receipt.json')) -Destination (Join-Path $artifact 'server-receipt.json') -ErrorAction Stop
        }
        if(-not $reopen){
            $owner=Join-Path $run 'owner.json';$hash=(Get-FileHash -LiteralPath $owner).Hash
            $dll=Join-Path $BuildRoot "bin/DataGrip.Stage1.Server/$configuration/net10.0/DataGrip.Stage1.Server.dll"
            $generator=Join-Path $BuildRoot "bin/Seamless.Database.Generator/$configuration/net10.0/Seamless.Database.Generator.dll"
            $guard=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command dotnet).Source -Arguments @($dll,$run,(Join-Path $CacheRoot $runName),$generator) -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $artifact 'reinitialization-guard') -TimeoutSeconds 10
            if($guard.Outcome -ne 'Failed' -or (Get-FileHash -LiteralPath $owner).Hash -ne $hash){throw 'Existing fixture was not protected.'}
            if((Get-Content -LiteralPath (Join-Path $artifact 'reinitialization-guard/stderr.log') -Raw) -notmatch 'Refuse to reuse a previous run without explicit --reopen'){throw 'Unexpected guard failure.'}
        }
    }
}
