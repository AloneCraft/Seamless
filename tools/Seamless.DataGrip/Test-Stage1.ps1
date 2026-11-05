[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$CacheRoot,
    [Parameter(Mandatory)][string]$ReadyFile
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-')){throw 'Use dedicated stage1 roots.'}
$ready=Get-Content -LiteralPath $ReadyFile -Raw | ConvertFrom-Json
if($ready.url -cne ('jdbc:seamless://127.0.0.1:'+$ready.port+'/DataGripStage1')){throw 'Refuse a non-fixture URL.'}
$process=Get-Process -Id $ready.pid -ErrorAction Stop
if($process.StartTime.ToUniversalTime().Ticks -ne ([datetime]$ready.creationUtc).ToUniversalTime().Ticks){throw 'Server process identity mismatch.'}
$env:TEMP=Join-Path $DataRoot 'tmp';$env:TMP=$env:TEMP
$result=& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command java).Source -Arguments @('-XX:ActiveProcessorCount=8','-cp',(Join-Path $DataRoot 'jdbc/classes'),'dev.seamless.jdbc.Stage1Acceptance',$ready.url) -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot 'jdbc-acceptance') -TimeoutSeconds 60
$result | ConvertTo-Json -Depth 6
if($result.Outcome -ne 'Passed'){throw ('JDBC acceptance failed: '+$result.Error)}
