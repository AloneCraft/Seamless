[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$WindowsRunRoot,
    [Parameter(Mandatory)][string]$SourceSnapshot,
    [Parameter(Mandatory)][string]$SourceManifest,
    [ValidateSet('Debug','Release')][string]$Configuration='Release'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$parent=(Resolve-Path -LiteralPath $WindowsRunRoot).ProviderPath
if(!$parent.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'WindowsRunRoot must be under F:\Seamless.'}
$snapshot=(Resolve-Path -LiteralPath $SourceSnapshot).ProviderPath.TrimEnd('\','/')
if(!$snapshot.StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){throw 'SourceSnapshot must be an isolated source snapshot under F:\Seamless.'}
$manifestPath=(Resolve-Path -LiteralPath $SourceManifest).ProviderPath
$manifestHash=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json -AsHashtable
if(!$manifest.ContainsKey('Seamless.Management.Server/WindowsServer.csproj') -and !$manifest.ContainsKey('Seamless.Management.Server\WindowsServer.csproj')){throw 'The source manifest does not contain the normal server project.'}
foreach($relative in $manifest.Keys){
    $inputPath=[IO.Path]::GetFullPath((Join-Path $snapshot $relative))
    if(!$inputPath.StartsWith($snapshot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'A source manifest path escapes the fixed snapshot.'}
    if((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $manifest[$relative]){throw ('Fixed source hash mismatch: '+$relative)}
}
$linuxSnapshot='/mnt/f/'+$snapshot.Substring(3).Replace('\','/')
$program=Join-Path $parent 'probe/Program.cs'
$project=Join-Path $parent 'probe/QueryVisualizationAcceptance.csproj'
foreach($source in @($program,$project)){if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw ('Missing fixed P0 input: '+$source)}}
if(!(Get-Content -LiteralPath $program -Raw).Contains('Assembly.Load("Seamless.FLinq.Generator")')){throw 'The fixed P0 generator load must be retained.'}
$nonce=[guid]::NewGuid().ToString('N')
$run=Join-Path $parent ('linux-'+$nonce)
if(Test-Path -LiteralPath $run){throw 'The isolated output already exists.'}
$null=New-Item -ItemType Directory -Path $run
$null=New-Item -ItemType Directory -Path (Join-Path $run 'probe')
$programHash=(Get-FileHash -LiteralPath $program -Algorithm SHA256).Hash
[IO.File]::Copy($program,(Join-Path $run 'probe/Program.cs'),$false)
if((Get-FileHash -LiteralPath $program -Algorithm SHA256).Hash -ne $programHash -or (Get-FileHash -LiteralPath (Join-Path $run 'probe/Program.cs') -Algorithm SHA256).Hash -ne $programHash){throw 'The P0 program changed during preparation.'}
$xml=[Xml.Linq.XDocument]::Load($project)
$references=@($xml.Descendants('ProjectReference'))
if($references.Count -ne 1){throw 'Expected the single normal WindowsServer project reference.'}
$reference=$references[0].Attribute('Include')
$windowsProject=(Join-Path $repository 'Seamless.Management.Server/WindowsServer.csproj').Replace('\','/')
$fixedProject=(Join-Path $snapshot 'Seamless.Management.Server/WindowsServer.csproj').Replace('\','/')
$actualReference=$reference.Value.Replace('\','/')
if(![string]::Equals($actualReference,$windowsProject,[StringComparison]::OrdinalIgnoreCase) -and ![string]::Equals($actualReference,$fixedProject,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected server project reference.'}
$reference.Value=$linuxSnapshot+'/Seamless.Management.Server/WindowsServer.csproj'
$xml.Save((Join-Path $run 'probe/QueryVisualizationAcceptance.csproj'))
[IO.File]::Copy((Join-Path $snapshot 'global.json'),(Join-Path $run 'global.json'),$false)
[IO.File]::Copy($manifestPath,(Join-Path $run 'source-manifest.json'),$false)
if((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne $manifestHash -or (Get-FileHash -LiteralPath (Join-Path $run 'source-manifest.json') -Algorithm SHA256).Hash -ne $manifestHash){throw 'The source manifest changed during preparation.'}
foreach($name in @('QueryVisualizationLinux.sh','QueryVisualizationLinux.Verify.py')){
    $text=Get-Content -LiteralPath (Join-Path $PSScriptRoot $name) -Raw
    [IO.File]::WriteAllText((Join-Path $run $name),$text.Replace("`r`n","`n"),[Text.UTF8Encoding]::new($false))
}
$linux='/mnt/f/'+$run.Substring(3).Replace('\','/')
$harnessHashes=[ordered]@{}
foreach($name in @('QueryVisualizationLinux.sh','QueryVisualizationLinux.Verify.py','global.json')){$harnessHashes[$name]=(Get-FileHash -LiteralPath (Join-Path $run $name) -Algorithm SHA256).Hash}
$record=[ordered]@{
    Nonce=$nonce;Configuration=$Configuration;PreparedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
    WindowsRun=$run;LinuxRun=$linux;SourceProgram=$program;SourceSha256=$programHash
    ProbeProject=(Join-Path $run 'probe/QueryVisualizationAcceptance.csproj')
    ProbeProjectSha256=(Get-FileHash -LiteralPath (Join-Path $run 'probe/QueryVisualizationAcceptance.csproj')).Hash
    SourceSnapshot=$snapshot;LinuxSourceSnapshot=$linuxSnapshot;SourceManifestSha256=$manifestHash
    HarnessSha256=$harnessHashes;PrepareScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    ProtectedPid=90899;ProtectedProcessIds=@(90899,93455);BuildMemoryHigh='1400M';BuildMemoryMax='1700M';CpuList='0-7'
}
[IO.File]::WriteAllText((Join-Path $run 'prepared.json'),($record|ConvertTo-Json -Depth 3),[Text.UTF8Encoding]::new($false))
$record|ConvertTo-Json -Depth 3
