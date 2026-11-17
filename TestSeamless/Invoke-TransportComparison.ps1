[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineGatewayAssembly,
    [Parameter(Mandatory)][string]$ReleaseTestDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [Parameter(Mandatory)][string]$LogDirectory,
    [ValidateSet('baseline','kestrel')][string[]]$Variants=@('baseline','kestrel'),
    [ValidateRange(1,600)][int]$PortRecoveryWaitSeconds=240
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($path in @($BaselineGatewayAssembly,$ReleaseTestDirectory)){
    if(-not [IO.Path]::IsPathFullyQualified($path)){
        throw '基準DLLとReleaseテスト成果物には絶対パスを指定してください。'
    }
    $full=[IO.Path]::GetFullPath($path)
    if(-not $full.StartsWith($repository+'\',[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetPathRoot($full) -ne 'F:\'){
        throw '読み取り元はリポジトリ内か、Fドライブに保全した成果物を指定してください。'
    }
}
foreach($path in @($WorkDirectory,$LogDirectory)){
    if(-not [IO.Path]::IsPathFullyQualified($path) -or -not ([IO.Path]::GetFullPath($path).TrimEnd('\')+'\').StartsWith('F:\Seamless\',[StringComparison]::OrdinalIgnoreCase)){
        throw '隔離する比較の作業先・ログ先にはF:\Seamless配下の絶対パスを指定してください。'
    }
}
if(-not (Test-Path -LiteralPath $BaselineGatewayAssembly -PathType Leaf)){throw '移行前に凍結したGateway DLLが必要です。'}
$test=[IO.Path]::GetFullPath($ReleaseTestDirectory)
if(-not (Test-Path -LiteralPath (Join-Path $test 'Management.Tests.dll'))){throw 'Releaseテスト成果物がありません。'}
$run=[guid]::NewGuid().ToString('N');$work=Join-Path $WorkDirectory $run;$logs=Join-Path $LogDirectory $run
$guard=& 'C:\Python314\python.exe' -B 'G:\AI\Skills\MachineMemoryGuard\MemoryGuard.py' --status | ConvertFrom-Json
if(!$guard.healthy -or !$guard.lastStatus.codingActive -or $guard.currentUsedPercent -ge 85){throw 'メモリ監視または空きメモリを確認してください。'}
New-Item -ItemType Directory -Path $work,$logs | Out-Null
& (Join-Path $repository 'TestSeamless\Invoke-LocalProcess.ps1') -InitializeOnly | Out-Null
$env:TEMP=Join-Path $work 'temp';$env:TMP=$env:TEMP;New-Item -ItemType Directory -Path $env:TEMP | Out-Null
$env:DOTNET_CLI_HOME=Join-Path $work 'dotnet-home';$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$manifest=@()
foreach($variant in $Variants){
    # 前の1万接続が残したTIME_WAITを次の方式の性能差として数えない。
    $waiting=[Diagnostics.Stopwatch]::StartNew()
    while($true){
        $connections=Get-NetTCPConnection -ErrorAction Stop
        $timeWait=@($connections | Where-Object State -eq TimeWait).Count
        [ordered]@{Utc=[DateTimeOffset]::UtcNow;TimeWait=$timeWait;WaitSeconds=$waiting.Elapsed.TotalSeconds} | ConvertTo-Json | Set-Content (Join-Path $logs ($variant+'-ports.json')) -Encoding utf8
        if($timeWait -le 2048){break}
        if($waiting.Elapsed.TotalSeconds -ge $PortRecoveryWaitSeconds){throw 'TIME_WAITが多いため負荷開始を見送りました。OS設定変更や要求の自動再送は行いません。'}
        Write-Output ('ポート回復を待機中: TIME_WAIT='+$timeWait)
        Start-Sleep -Seconds 5
    }
    $target=Join-Path $work $variant;New-Item -ItemType Directory -Path $target | Out-Null
    Copy-Item -Path (Join-Path $test '*') -Destination $target -Recurse
    if($variant -eq 'baseline'){Copy-Item -LiteralPath $BaselineGatewayAssembly -Destination (Join-Path $target 'Server.dll')}
    $assembly=Join-Path $target 'Server.dll'
    $manifest+=[ordered]@{Variant=$variant;Assembly=$assembly;Sha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash}
    $result=& (Join-Path $repository 'TestSeamless\Invoke-LocalProcess.ps1') -Executable (Get-Command dotnet).Source -Arguments @((Join-Path $target 'Management.Tests.dll'),'--transport-probe') -WorkingDirectory $work -ArtifactDirectory (Join-Path $logs $variant) -TimeoutSeconds 300
    [ordered]@{Runs=$manifest;Scope='Loopback native RPC; separate processes; same client and dependencies; common affinity max 8'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logs 'manifest.json') -Encoding utf8
    if($result.Outcome -ne 'Passed'){throw ('比較の条件を満たしませんでした。再送・OS設定の変更は行いません。結果: '+(Join-Path $logs $variant))}
}
Write-Output $logs
