[CmdletBinding()]
param([Parameter(Mandatory)][string]$WindowsRun)
$ErrorActionPreference='Stop'
$run=(Resolve-Path -LiteralPath $WindowsRun).ProviderPath.TrimEnd('\','/')
if(!$run.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'The Linux run must be under F:\Seamless.'}
$preparedPath=Join-Path $run 'prepared.json'
$prepared=Get-Content -LiteralPath $preparedPath -Raw|ConvertFrom-Json
if($prepared.WindowsRun -ne $run -or $prepared.Configuration -ne 'Release'){throw 'Expected the existing prepared Release run.'}
$manifestPath=Join-Path $run 'source-manifest.json'
if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $prepared.SourceManifestSha256){throw 'The prepared source manifest changed.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -AsHashtable
if(!$manifest.ContainsKey('Tests/Management.Tests/Management.Tests.csproj') -and !$manifest.ContainsKey('Tests\Management.Tests\Management.Tests.csproj')){throw 'The fixed source closure does not contain Management.Tests.'}
$files=[ordered]@{}
foreach($name in @('QueryVisualizationLinux.Tests.sh','QueryVisualizationLinux.Tests.Verify.py')){
    $target=Join-Path $run $name
    if(Test-Path -LiteralPath $target){throw ('The new test harness path already exists: '+$name)}
    $content=(Get-Content -LiteralPath (Join-Path $PSScriptRoot $name) -Raw).Replace("`r`n","`n")
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($content)
    $stream=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
    $files[$name]=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
}
$record=[ordered]@{LinuxRun=$prepared.LinuxRun;SourceManifestSha256=$prepared.SourceManifestSha256;SourceFileCount=$manifest.Count;Configuration='Release';HarnessSha256=$files;ProtectedPid=90899;ProtectedProcessIds=$prepared.ProtectedProcessIds;BuildMemoryHigh='1400M';BuildMemoryMax='1700M';CpuList='0-7';PreparedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')}
$path=Join-Path $run 'tests-prepared.json'
$stream=[IO.FileStream]::new($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write([Text.UTF8Encoding]::new($false).GetBytes(($record|ConvertTo-Json -Depth 4)));$stream.Flush($true)}finally{$stream.Dispose()}
$record|ConvertTo-Json -Depth 4
