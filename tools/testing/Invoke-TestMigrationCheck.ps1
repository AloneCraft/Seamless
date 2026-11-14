[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Name,
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [ValidateSet('Build','Test','List','BinaryTest','RegressionList','Regression')][string]$Action='Test',
    [string]$Framework,
    [string]$Filter,
    [string]$Database,
    [string]$Results,
    [switch]$CoreOnly,
    [switch]$Rebuild,
    [int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$repository=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
if($Name -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Name must be a simple artifact label.'}
$work='F:\CodexTemp\test-retirement-20261104'
$cache='F:\CodexTemp\test-retirement-20261104'
$logs=Join-Path $cache $Name
if(Test-Path (Join-Path $logs 'receipt.json')){throw 'Use a new artifact label for each check.'}
New-Item -ItemType Directory -Force -Path $work,$cache | Out-Null
$dataID=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Name))).Substring(0,8)
# Tsavoriteのチェックポイント階層を含めてもWindowsのパス長上限内に収める。
$env:SEAMLESS_TEST_DATA_ROOT=Join-Path 'F:\CodexTemp\stm' $dataID
$env:SEAMLESS_TEST_REPOSITORY=$repository
$env:SEAMLESS_TEST_RESULT_ROOT=$logs
$env:SEAMLESS_RELEASE_WORKER_OUTPUT=Join-Path $work 'artifacts/bin/Worker/release'
New-Item -ItemType Directory -Force -Path $env:SEAMLESS_TEST_DATA_ROOT | Out-Null
$env:TEMP=$env:SEAMLESS_TEST_DATA_ROOT
$env:TMP=$env:SEAMLESS_TEST_DATA_ROOT
$env:DOTNET_CLI_HOME=Join-Path $cache 'cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:NUGET_PACKAGES=Join-Path $cache 'packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $cache 'http'
$verb='test'
if($Action -eq 'Build'){$verb='build'}
$arguments=@($verb,$Project,'-c',$Configuration,'--artifacts-path',(Join-Path $work 'artifacts'),'-p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages','-p:UseSharedCompilation=false','-m:1')
if($Framework){$arguments+=@('-f',$Framework)}
if($CoreOnly){$arguments+='-p:CoreOnly=true'}
if($Rebuild){
    if($Action -ne 'Build'){throw 'Rebuild is supported only for Build checks.'}
    $arguments+='-t:Rebuild'
}
if($Filter){$arguments+=@('--filter',$Filter)}
if($Action -eq 'List'){$arguments+='--list-tests'}
if($Action -eq 'Test'){$arguments+=@('--logger','trx','--results-directory',(Join-Path $logs 'results'))}
if($Action -in @('RegressionList','Regression')){
    $arguments=@($Project,'--regressions')
    if($Action -eq 'RegressionList'){$arguments+='--list'}
    if($Filter){$arguments+=@('--case',$Filter)}
    if($Database -or $Results){
        if($Action -ne 'Regression' -or -not [IO.Path]::IsPathFullyQualified($Database) -or -not [IO.Path]::IsPathFullyQualified($Results)){throw 'Regression persistence requires absolute Database and Results paths.'}
        $arguments+=@('--database',$Database,'--results',$Results)
    }
}
if($Action -eq 'BinaryTest'){
    $arguments=@('vstest',$Project,'--logger:trx',('--ResultsDirectory:'+(Join-Path $logs 'results')))
    if($Filter){$arguments+=('--TestCaseFilter:'+$Filter)}
}
$lockPath=Join-Path $work 'artifact-access.lock'
$lock=$null
while($null -eq $lock){
    try{$lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
    catch [IO.IOException]{Start-Sleep -Milliseconds 500}
}
try{
    $memory=Get-CimInstance Win32_OperatingSystem
    $usedPercent=100*(1-$memory.FreePhysicalMemory/$memory.TotalVisibleMemorySize)
    if($usedPercent -ge 85){throw "Memory guard: new verification is unavailable at $([math]::Round($usedPercent,1)) percent physical memory usage."}
    & (Join-Path $repository 'TestSeamless/Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments $arguments -WorkingDirectory $repository -ArtifactDirectory $logs -TimeoutSeconds $TimeoutSeconds | ConvertTo-Json -Depth 6
}finally{$lock.Dispose()}
