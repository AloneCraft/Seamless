[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$RunRoot,
    [ValidateSet('Gateway','WindowsManagement','ManagementChecks')][string]$Suite='Gateway',
    [ValidateRange(0,100)][int]$Longest=8
)

$ErrorActionPreference='Stop'

function Get-RecordedProcessState($Identity){
    if(!$Identity.pid -or !$Identity.creationUtc){return 'Unknown'}
    $process=$null
    try{
        $process=[Diagnostics.Process]::GetProcessById([int]$Identity.pid)
        if(!$process.HasExited -and $process.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$Identity.creationUtc).UtcDateTime){return 'Running'}
        return 'Missing'
    }catch [ArgumentException]{
        return 'Missing'
    }catch{
        return 'Unknown'
    }finally{
        if($process){$process.Dispose()}
    }
}

function Read-TestResults([string]$Path){
    [xml]$report=Get-Content -LiteralPath $Path -Raw
    if($report.DocumentElement.LocalName -ne 'TestRun'){throw "Expected a TRX test run: $Path"}
    $results=@($report.TestRun.Results.UnitTestResult|Where-Object {$_})
    $intervals=@(foreach($item in $results){
        if(!$item.startTime -or !$item.endTime){continue}
        $start=[DateTimeOffset]$item.startTime
        $end=[DateTimeOffset]$item.endTime
        if($start -gt $end){throw "Invalid test interval in $Path"}
        [pscustomobject]@{Name=$item.testName;Outcome=$item.outcome;Start=$start;End=$end;Seconds=($end-$start).TotalSeconds}
    })
    $wall=$null
    if($intervals.Count){
        $earliest=($intervals|Measure-Object Start -Minimum).Minimum
        $latest=($intervals|Measure-Object End -Maximum).Maximum
        $wall=[Math]::Round(($latest-$earliest).TotalSeconds,3)
    }
    $slowest=@(foreach($item in ($intervals|Sort-Object Seconds -Descending|Select-Object -First $Longest)){
        $overlap=@($intervals|Where-Object {$_ -ne $item -and $_.Start -lt $item.End -and $_.End -gt $item.Start}).Count
        [pscustomobject]@{Name=$item.Name;Outcome=$item.Outcome;Seconds=[Math]::Round($item.Seconds,3);OverlappingCaseIntervals=$overlap}
    })
    [pscustomobject]@{
        Trx=$Path
        Cases=$results.Count
        Outcomes=@($results|Group-Object outcome|Select-Object Name,Count)
        NotExecuted=@($results|Where-Object outcome -eq 'NotExecuted'|ForEach-Object {
            [pscustomobject]@{Name=$_.testName;Reason=[string]$_.Output.ErrorInfo.Message}
        })
        CaseWallSeconds=$wall
        SumOfCaseSeconds=[Math]::Round(($intervals|Measure-Object Seconds -Sum).Sum,3)
        Slowest=$slowest
    }
}

$layout=[ordered]@{
    'build-Debug'=$null
    'run-Debug'='result-Debug/gateway.trx'
    'build-Release'=$null
    'run-Release'='result-Release/gateway.trx'
    'build'=$null
    'run'='result/gateway.trx'
    'pack'=$null
    'verify'=$null
}
if($Suite -eq 'WindowsManagement'){
    $layout=[ordered]@{
        'server-debug'='result-server-debug/server.trx'
        'build-gui-debug'=$null
        'gui-debug'='result-gui-debug/gui.trx'
        'server-release'='result-server-release/server.trx'
        'build-gui-release'=$null
        'gui-release'='result-gui-release/gui.trx'
    }
}
if($Suite -eq 'ManagementChecks'){
    $layout=[ordered]@{'ui-build'=$null;'.'='checks.trx'}
}

foreach($root in $RunRoot){
    $root=(Resolve-Path -LiteralPath $root).ProviderPath
    $repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if(([IO.Path]::GetPathRoot($root) -ne 'F:\' -and !$root.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) -or !(Test-Path -LiteralPath $root -PathType Container)){throw 'Use an existing verification directory in the repository or on F:.'}
    $stages=@(foreach($name in $layout.Keys){
        $directory=Join-Path $root $name
        $receipt=Get-Item -LiteralPath (Join-Path $directory 'receipt.json') -ErrorAction SilentlyContinue
        $startRecord=Get-Item -LiteralPath (Join-Path $directory 'process-start.json') -ErrorAction SilentlyContinue
        $row=[ordered]@{Stage=$name;Outcome='Missing';Receipt=$null;StartRecord=$null;RunnerState=$null;CancellationRequested=$false;Pid=$null;ProcessStartedUtc=$null;ReceiptWrittenUtc=$null;ProcessToReceiptSeconds=$null;GapFromPreviousReceiptSeconds=$null;TestResults=$null}
        if($Suite -eq 'ManagementChecks' -and $name -eq '.'){$row.Stage='checks'}
        $started=$null
        if($startRecord){
            $started=Get-Content -LiteralPath $startRecord.FullName -Raw|ConvertFrom-Json
            $row.StartRecord=$startRecord.FullName
            $request=$started.Task.cancellationRequestFile
            if($request -match '^cancel-[a-f0-9-]{36}\.request$' -and $request -eq "cancel-$($started.Task.id).request"){
                $row.CancellationRequested=Test-Path -LiteralPath (Join-Path $directory $request) -PathType Leaf
            }
        }
        if($receipt){
            $record=Get-Content -LiteralPath $receipt.FullName -Raw|ConvertFrom-Json
            if(!$record.Outcome){throw "Invalid process receipt: $($receipt.FullName)"}
            if($started -and ($started.Task.id -ne $record.Task.id -or $started.Task.pid -ne $record.Task.pid -or ([DateTimeOffset]$started.Task.creationUtc) -ne ([DateTimeOffset]$record.Task.creationUtc))){
                throw "The process start and terminal receipt identify different attempts: $directory"
            }
            $row.Outcome=$record.Outcome
            $row.Receipt=$receipt.FullName
            $row.Pid=$record.Task.pid
            $row.ReceiptWrittenUtc=[DateTimeOffset]$receipt.LastWriteTimeUtc
            if($record.Task.creationUtc){
                $row.ProcessStartedUtc=[DateTimeOffset]$record.Task.creationUtc
                $row.ProcessToReceiptSeconds=[Math]::Round(($row.ReceiptWrittenUtc-$row.ProcessStartedUtc).TotalSeconds,3)
            }
        }elseif($started){
            $row.Pid=$started.Task.pid
            $row.ProcessStartedUtc=[DateTimeOffset]$started.Task.creationUtc
            $row.RunnerState=Get-RecordedProcessState $started.Runner
            $row.Outcome=if((Get-RecordedProcessState $started.Task) -eq 'Running'){'Running'}else{'Unknown'}
        }elseif(Test-Path -LiteralPath (Join-Path $directory 'process-attempt.lock') -PathType Leaf){
            $row.Outcome='Unknown'
        }elseif(Test-Path -LiteralPath $directory -PathType Container){
            $row.Outcome='NoReceipt'
        }
        if($layout[$name]){
            $trx=Join-Path $root $layout[$name]
            if(Test-Path -LiteralPath $trx -PathType Leaf){$row.TestResults=Read-TestResults $trx}
        }
        [pscustomobject]$row
    })
    if($Suite -eq 'ManagementChecks'){
        $uiBuild=$stages[0]
        $checks=$stages[1]
        if($uiBuild.Outcome -in 'Cancelled','Failed','Timeout','Unavailable' -and $checks.Outcome -in 'Missing','NoReceipt'){
            $checks.Outcome='NotRun'
        }
    }
    $previous=$null
    foreach($stage in ($stages|Where-Object {$_.ProcessStartedUtc -and $_.ReceiptWrittenUtc}|Sort-Object ProcessStartedUtc)){
        if($previous){
            $stage.GapFromPreviousReceiptSeconds=[Math]::Round(($stage.ProcessStartedUtc-$previous.ReceiptWrittenUtc).TotalSeconds,3)
        }
        $previous=$stage
    }
    [pscustomobject]@{RunRoot=$root;Suite=$Suite;Stages=$stages}
}
