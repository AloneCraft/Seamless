#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')][string]$Name,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string[]]$Arguments,
    [ValidateSet('SSD','HDD')][string]$Storage='SSD',
    [ValidateRange(1,2147483647)][int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$folder=Join-Path $root 'Work\Consolidation'
$drive='D'
if($Storage -eq 'HDD'){$drive='F'}
$partition=Get-Partition -DriveLetter $drive -ErrorAction Stop
$disk=$partition | Get-Disk -ErrorAction Stop
$physical=@(Get-PhysicalDisk -ErrorAction Stop | Where-Object { $_.DeviceId -eq [string]$disk.Number })
if($physical.Count -ne 1 -or [string]$physical[0].MediaType -ne $Storage){
    throw "Storage mismatch: $drive must be $Storage. No fallback is allowed."
}
$dataRoot="${drive}:\SeamlessTestData"
function Assert-StoragePath([string]$Path){
    $full=[IO.Path]::GetFullPath($Path)
    if($full -ne $dataRoot -and -not $full.StartsWith($dataRoot+'\',[StringComparison]::OrdinalIgnoreCase)){
        throw "Storage path must be below $dataRoot : $full"
    }
    $ancestor=$full
    while($ancestor){
        if(Test-Path -LiteralPath $ancestor){
            $item=Get-Item -LiteralPath $ancestor -Force
            if(-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){
                throw "Storage path must contain only ordinary directories: $ancestor"
            }
        }
        $ancestor=[IO.Path]::GetDirectoryName($ancestor)
    }
}
Assert-StoragePath $dataRoot
if(-not ('ConsolidationMemory' -as [type])){
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ConsolidationMemory {
    [StructLayout(LayoutKind.Sequential)] public struct State {
        public uint Length,Load;
        public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,Extended;
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref State state);
    public static uint Load() {
        var state=new State { Length=(uint)Marshal.SizeOf<State>() };
        if(!GlobalMemoryStatusEx(ref state)) throw new System.ComponentModel.Win32Exception();
        return state.Load;
    }
}
'@
}
$load=[ConsolidationMemory]::Load()
if($load -ge 85){throw "Not started: physical memory $load%."}
$runID=$Name+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-'+[Guid]::NewGuid().ToString('N')
$runDirectory=Join-Path $dataRoot $runID
$temporaryDirectory=Join-Path $runDirectory 'temp'
$testDirectory=Join-Path $runDirectory 'data'
foreach($directory in @($runDirectory,$temporaryDirectory,$testDirectory)){
    Assert-StoragePath $directory
    $null=[IO.Directory]::CreateDirectory($directory)
    Assert-StoragePath $directory
}
$null=[IO.Directory]::CreateDirectory($folder)
$effectiveArguments=[Collections.Generic.List[string]]::new()
foreach($argument in $Arguments){$effectiveArguments.Add($argument)}
for($index=0;$index -lt $effectiveArguments.Count;$index++){
    if($effectiveArguments[$index] -in @('--working','--work')){
        if($index+1 -ge $effectiveArguments.Count){throw 'Storage option requires a path.'}
        $path=[IO.Path]::GetFullPath($effectiveArguments[$index+1],$root)
        Assert-StoragePath $path
        $effectiveArguments[$index+1]=$path
    }
}
$scalable=@($effectiveArguments | Where-Object { [IO.Path]::GetFileName($_) -match '^Benchmark\.Scalable\.Headless\.(dll|csproj)$' }).Count -gt 0
if($scalable -and $effectiveArguments.Contains('run')){
    $requestIndex=$effectiveArguments.IndexOf('--request')
    if($requestIndex -lt 0 -or $requestIndex+1 -ge $effectiveArguments.Count){throw 'Scalable run requires --request FILE.'}
    $source=[IO.Path]::GetFullPath($effectiveArguments[$requestIndex+1],$root)
    $request=Get-Content -LiteralPath $source -Raw | ConvertFrom-Json -AsHashtable
    foreach($key in @($request.Keys)){
        if([string]::Equals($key,'TemporaryDirectory',[StringComparison]::OrdinalIgnoreCase)){$null=$request.Remove($key)}
    }
    $request['TemporaryDirectory']=$temporaryDirectory
    $copy=Join-Path $runDirectory 'request.json'
    $request | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $copy -Encoding utf8
    $effectiveArguments[$requestIndex+1]=$copy
}
$sweep=@($effectiveArguments | Where-Object { [IO.Path]::GetFileName($_) -match '^Benchmark\.Tsavorite\.ParameterSweep\.Headless\.(dll|csproj)$' }).Count -gt 0
if($sweep -and -not $effectiveArguments.Contains('--working')){
    $effectiveArguments.Add('--working')
    $effectiveArguments.Add((Join-Path $testDirectory 'ParameterSweep'))
}
$start=[Diagnostics.ProcessStartInfo]::new('dotnet')
$start.WorkingDirectory=$root
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$start.Environment['TEMP']=$temporaryDirectory
$start.Environment['TMP']=$temporaryDirectory
$start.Environment['SEAMLESS_TEST_DATA_ROOT']=$testDirectory
$start.Environment['SEAMLESS_TEST_STORAGE']=$Storage
foreach($argument in $effectiveArguments){$start.ArgumentList.Add($argument)}
$metadata=[ordered]@{
    RunID=$runID;Storage=$Storage;Drive=$drive;DiskNumber=$disk.Number;Model=$disk.FriendlyName
    DataDirectory=$testDirectory;TemporaryDirectory=$temporaryDirectory;WorkingDirectory=$root
    StartedAt=[DateTimeOffset]::Now;RequestedArguments=@($Arguments);Arguments=@($effectiveArguments);Status='Prepared'
}
$metadataPath=Join-Path $folder "$runID.storage.json"
$logPath=Join-Path $folder "$runID.log"
$metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $metadataPath -Encoding utf8
Write-Output "Storage=$Storage drive=${drive}: data=$testDirectory temp=$temporaryDirectory metadata=$metadataPath"
$child=[Diagnostics.Process]::new()
$child.StartInfo=$start
$started=$false
$reason=''
$peak=$load
try{
    $null=$child.Start()
    $started=$true
    $metadata.Status='Running'
    $metadata.ProcessID=$child.Id
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $metadataPath -Encoding utf8
    $output=$child.StandardOutput.ReadToEndAsync()
    $errors=$child.StandardError.ReadToEndAsync()
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while(-not $child.WaitForExit(500)){
        $load=[ConsolidationMemory]::Load()
        $peak=[Math]::Max($peak,$load)
        if($load -ge 88){$reason="Stopped: physical memory $load%.";break}
        if($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds){$reason="Stopped: timeout $TimeoutSeconds seconds.";break}
    }
    if($reason -and -not $child.HasExited){$child.Kill($true);$child.WaitForExit()}
    [IO.File]::WriteAllText($logPath,$output.GetAwaiter().GetResult()+$errors.GetAwaiter().GetResult())
    $metadata.ExitCode=$child.ExitCode
    $metadata.Status='Failed'
    if(-not $reason -and $child.ExitCode -eq 0){$metadata.Status='Completed'}
    $metadata.Reason=$reason
    Write-Output "$Name exit=$($child.ExitCode) peak=$peak% seconds=$([Math]::Round($timer.Elapsed.TotalSeconds,1)) $reason log=$logPath"
    Get-Content -LiteralPath $logPath -Tail 12
    if($reason){throw $reason}
    exit $child.ExitCode
}finally{
    if($started -and -not $child.HasExited){$child.Kill($true);$child.WaitForExit()}
    if($metadata.Status -in @('Prepared','Running')){$metadata.Status='Interrupted'}
    $metadata.EndedAt=[DateTimeOffset]::Now
    $metadata.PeakPhysicalMemoryPercent=$peak
    $metadata | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $metadataPath -Encoding utf8
    $child.Dispose()
}
