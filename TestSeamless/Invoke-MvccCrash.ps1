[CmdletBinding()]
param([Parameter(Mandatory)][string]$TaskRoot,[Parameter(Mandatory)][string]$LogRoot)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Invoke-MvccValidation.ps1') -TaskRoot $TaskRoot -LogRoot $LogRoot -Project 'Benchmark/Experiments/Experiments.csproj' -ExperimentMode ProductTransactions -Configuration Release -BuildOnly
$identity=[Guid]::NewGuid().ToString('N')
$data=Join-Path 'F:\Seamless\MvccData' $identity.Substring(0,12)
$artifacts=Join-Path $LogRoot ('abrupt-recovery-'+$identity)
New-Item -ItemType Directory -Path $artifacts -Force|Out-Null
$snapshot=Join-Path $TaskRoot 'source'
$dll=Join-Path $snapshot 'Benchmark/Experiments/bin/ProductTransactions/Release/net10.0/ProductTransactions.dll'
Copy-Item -LiteralPath (Join-Path $LogRoot 'source-manifest.json') -Destination (Join-Path $artifacts 'source-manifest.json')
$writer=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,'--crash-write',$data,(Join-Path $artifacts 'committed.json')) -WorkingDirectory $snapshot -ArtifactDirectory (Join-Path $artifacts 'writer') -TimeoutSeconds 120
if($writer.Task.exitCode -ne 73 -or -not(Test-Path -LiteralPath (Join-Path $artifacts 'committed.json'))){throw 'The writer did not reach the expected abrupt-exit point.'}
$reader=& (Join-Path $PSScriptRoot 'Invoke-LocalProcess.ps1') -Executable 'C:\Program Files\dotnet\dotnet.exe' -Arguments @($dll,'--crash-read',$data,(Join-Path $artifacts 'recovered.json')) -WorkingDirectory $snapshot -ArtifactDirectory (Join-Path $artifacts 'reader') -TimeoutSeconds 120
if($reader.Outcome -ne 'Passed'){throw "Recovery failed: $($reader.Error)"}
@{Outcome='Passed';ExpectedWriterExit=73;Writer=$writer;Reader=$reader;DataRoot=$data;SourceManifestSha256=(Get-FileHash -LiteralPath (Join-Path $artifacts 'source-manifest.json')).Hash}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $artifacts 'summary.json') -Encoding utf8
[pscustomobject]@{Outcome='Passed';Artifacts=$artifacts}
