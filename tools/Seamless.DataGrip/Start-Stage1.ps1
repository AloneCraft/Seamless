[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataRoot,
    [Parameter(Mandatory)][string]$CacheRoot,
    [Parameter(Mandatory)][string]$RunName,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [ValidateRange(0,65535)][int]$Port=0,
    [switch]$Reopen,
    [string]$BuildRoot
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $DataRoot.StartsWith('D:/CodexTemp/datagrip-stage1-') -or -not $CacheRoot.StartsWith('F:/CodexTemp/datagrip-stage1-') -or $RunName -notmatch '^run-[a-zA-Z0-9-]+$'){throw 'Use dedicated stage1 roots and a simple run name.'}
if(-not $BuildRoot){$BuildRoot=$DataRoot}
if(-not $BuildRoot.StartsWith('D:/CodexTemp/datagrip-stage1-')){throw 'Use a dedicated build root.'}
$env:TEMP=Join-Path $DataRoot 'tmp';$env:TMP=$env:TEMP;$env:DOTNET_CLI_HOME=Join-Path $CacheRoot 'cli';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:MSBUILDDISABLENODEREUSE='1'
$dll=Join-Path $BuildRoot "bin/DataGrip.Stage1.Server/$Configuration/net10.0/DataGrip.Stage1.Server.dll"
$generator=Join-Path $BuildRoot "bin/Seamless/Database/Generator/$Configuration/net10.0/Seamless.Database.Generator.dll"
$arguments=@($dll,(Join-Path $DataRoot $RunName),(Join-Path $CacheRoot $RunName),$generator,[string]$Port)
if($Reopen){$arguments+='--reopen'}
& "$repository/TestSeamless/Invoke-LocalProcess.ps1" -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $DataRoot -ArtifactDirectory (Join-Path $CacheRoot ($RunName+'-process')) -TimeoutSeconds 7200
