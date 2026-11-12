[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidateRoot,
    [Parameter(Mandatory)][string]$SourceSnapshot,
    [Parameter(Mandatory)][string]$SourceManifest
)
$ErrorActionPreference='Stop'
$candidate=(Resolve-Path -LiteralPath $CandidateRoot).ProviderPath.TrimEnd('\','/')
$source=(Resolve-Path -LiteralPath $SourceSnapshot).ProviderPath.TrimEnd('\','/')
$manifest=(Resolve-Path -LiteralPath $SourceManifest).ProviderPath
foreach($path in @($candidate,$source,$manifest)){if(!$path.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'The candidate and fixed source must be under F:\Seamless.'}}
[IO.Directory]::CreateDirectory((Join-Path $candidate 'artifacts'))|Out-Null
$map=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json -AsHashtable
if($map.Count -lt 1 -or $map.Count -gt 65536){throw 'The fixed test closure file count is invalid.'}
if((!$map.ContainsKey('Tests/Management.Tests/Management.Tests.csproj') -and !$map.ContainsKey('Tests\Management.Tests\Management.Tests.csproj')) -or !(Test-Path -LiteralPath (Join-Path $source 'Tests/Management.Tests/Management.Tests.csproj') -PathType Leaf)){throw 'The fixed source closure must contain the actual Management.Tests project.'}
foreach($relative in $map.Keys){
    $path=[IO.Path]::GetFullPath((Join-Path $source $relative))
    if(!$path.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $map[$relative]){throw ('Fixed source mismatch: '+$relative)}
}
$actual=@(Get-ChildItem -LiteralPath $source -Recurse -File)
if($actual.Count -ne $map.Count){throw 'The fixed source file set differs from its manifest.'}
$work=Join-Path $candidate 'near-release'
if(Test-Path -LiteralPath $work){throw 'The near-release evidence root must be unused.'}
$null=New-Item -ItemType Directory -Path $work
$hashes=[ordered]@{}
foreach($name in @('QueryVisualizationWindows.Tests.ps1','Invoke-LocalProcess.ps1')){
    [IO.File]::Copy((Join-Path $PSScriptRoot $name),(Join-Path $work $name),$false)
    $hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $work $name) -Algorithm SHA256).Hash
}
[IO.File]::Copy($manifest,(Join-Path $work 'source-manifest.json'),$false)
$record=[ordered]@{CandidateRoot=$candidate;WorkRoot=$work;SourceSnapshot=$source;SourceManifestSha256=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash;SourceFileCount=$map.Count;ArtifactsRoot=(Join-Path $candidate 'artifacts');Configuration='Release';CpuMask=255;HarnessSha256=$hashes;PreparedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')}
[IO.File]::WriteAllText((Join-Path $work 'prepared.json'),($record|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$record|ConvertTo-Json -Depth 4
