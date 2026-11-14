[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$LogRoot,
    [ValidateSet('accept','bench','build')][string]$Stage='accept',
    [string]$Configuration='Release',
    [switch]$Reuse
)
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
[IO.Directory]::CreateDirectory($TaskRoot)|Out-Null
[IO.Directory]::CreateDirectory($LogRoot)|Out-Null
$env:TEMP=Join-Path $LogRoot 'temp';$env:TMP=$env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP)|Out-Null
$env:DOTNET_CLI_HOME=Join-Path $LogRoot 'dotnet-home'
$env:NUGET_PACKAGES=Join-Path $LogRoot 'packages'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $LogRoot 'http-cache'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:MSBUILDDISABLENODEREUSE='1'
$snapshot=Join-Path $TaskRoot 'source'
$project=Join-Path $snapshot 'Benchmark/Experiments/TransactionModes/TransactionModes.csproj'
$activity=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --activity-start
if($LASTEXITCODE -ne 0){throw 'Memory guard registration failed'}
$token=($activity|ConvertFrom-Json).token
try{
    if(-not $Reuse){
        $pending=[Collections.Generic.Queue[string]]::new();$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $pending.Enqueue('Benchmark/Experiments/TransactionModes/TransactionModes.csproj')
        $manifest=[Collections.Generic.List[object]]::new()
        while($pending.Count){
            $relative=$pending.Dequeue();if(-not $seen.Add($relative)){continue}
            $sourceProject=Join-Path $sourceRoot $relative
            $directory=Split-Path $sourceProject
            foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File){
                $local=[IO.Path]::GetRelativePath($directory,$file.FullName)
                if($local -match '(^|[\\/])(bin|obj|Artifacts|\.git)([\\/]|$)'){continue}
                if($local -match '^Work[\\/]' -and (Split-Path $directory -Leaf) -ne 'Seamless.Collections.Persisted'){continue}
                $target=Join-Path $snapshot ([IO.Path]::GetRelativePath($sourceRoot,$file.FullName))
                [IO.Directory]::CreateDirectory((Split-Path $target))|Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target
                $manifest.Add(@{Path=$file.FullName;Sha256=(Get-FileHash -LiteralPath $file.FullName).Hash})
            }
            [xml]$xml=Get-Content -LiteralPath $sourceProject -Raw
            foreach($reference in $xml.SelectNodes('//ProjectReference')){
                $absolute=[IO.Path]::GetFullPath((Join-Path $directory $reference.Include))
                $pending.Enqueue([IO.Path]::GetRelativePath($sourceRoot,$absolute))
            }
        }
        foreach($rootFile in Get-ChildItem -LiteralPath $sourceRoot -Filter 'Directory.Build.*' -File){Copy-Item -LiteralPath $rootFile.FullName -Destination (Join-Path $snapshot $rootFile.Name)}
        $manifest|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $LogRoot 'source-manifest.json') -Encoding utf8
    }
    Set-Location (Split-Path $project)
    & 'C:\Program Files\dotnet\dotnet.exe' --version
    $status=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status
    Write-Output $status
    if(($status|ConvertFrom-Json).currentUsedPercent -ge 85){throw 'Memory pressure: no new build/test started'}
    & 'C:\Program Files\dotnet\dotnet.exe' build $project -c $Configuration -m:1 -p:UseSharedCompilation=false -p:RestoreSources=C:\Users\kazuh\.nuget\packages -p:RestoreFallbackFolders=C:\Users\kazuh\.nuget\packages -p:NuGetAudit=false --disable-build-servers
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    $generated=Join-Path (Split-Path $project) "obj/$Configuration/generated"
    $nativeFiles=@(Get-ChildItem -LiteralPath $generated -Recurse -Filter '*.cs'|Where-Object{(Get-Content -LiteralPath $_.FullName -Raw) -match '\.Iterate\('})
    if($nativeFiles.Count -eq 0){throw 'FLinq native generated code was not found; no performance claim is allowed'}
    $nativeFiles.FullName|Set-Content (Join-Path $LogRoot ($Configuration+'-native-generated.txt'))
    if($Stage -eq 'build'){exit 0}
    & C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --activity-refresh $token
    $assembly=Join-Path (Split-Path $project) "bin/$Configuration/net10.0/TransactionModes.dll"
    Get-FileHash -LiteralPath $assembly
    $data=Join-Path $TaskRoot ('data-'+$Configuration+'-'+$Stage)
    # 再開時も完了済みのレポートを使う一方、fixture の WAL は常に新規領域へ作る。
    if($Stage -eq 'bench'){$data+='-'+[Guid]::NewGuid().ToString('N')}
    $report=Join-Path $LogRoot ($Configuration+'-'+$Stage+'.json')
    $runtimeOut=Join-Path $LogRoot ($Configuration+'-'+$Stage+'.stdout.log')
    $runtimeErr=Join-Path $LogRoot ($Configuration+'-'+$Stage+'.stderr.log')
    $process=Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList @($assembly,$Stage,$data,$report) -WindowStyle Hidden -PassThru -RedirectStandardOutput $runtimeOut -RedirectStandardError $runtimeErr
    $creation=$process.StartTime.ToUniversalTime()
    @{Pid=$process.Id;CreationUtc=$creation.ToString('o');Assembly=$assembly;DataRoot=$data;Affinity=$process.ProcessorAffinity.ToInt64();LauncherSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash}|ConvertTo-Json|Set-Content (Join-Path $LogRoot ($Configuration+'-'+$Stage+'.process.json'))
    while(-not $process.WaitForExit(15000)){
        & C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --activity-refresh $token
        $currentStatus=& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --status
        if(($currentStatus|ConvertFrom-Json).currentUsedPercent -ge 85){
            if($process.StartTime.ToUniversalTime() -eq $creation){$process.Kill($true);$process.WaitForExit()}
            throw 'Own trial cancelled for memory pressure'
        }
    }
    Get-Content -LiteralPath $runtimeOut -Tail 8
    if($process.ExitCode -ne 0){Get-Content -LiteralPath $runtimeErr -Tail 12;throw 'Trial failed'}
}finally{& C:\Python314\python.exe -B G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py --activity-end $token}
