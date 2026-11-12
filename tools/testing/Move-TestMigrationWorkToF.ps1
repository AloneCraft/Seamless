[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../../TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
$sourceRoot=[IO.Path]::GetFullPath('D:\CodexTemp\test-retirement-20261104')
$targetRoot=[IO.Path]::GetFullPath('F:\CodexTemp\test-retirement-20261104')
$sourceData=[IO.Path]::GetFullPath('D:\CodexTemp\stm')
$targetData=[IO.Path]::GetFullPath('F:\CodexTemp\stm')
if($sourceRoot -ne 'D:\CodexTemp\test-retirement-20261104' -or $targetRoot -ne 'F:\CodexTemp\test-retirement-20261104'){throw 'Unexpected migration roots'}
if(-not (Test-Path -LiteralPath $sourceRoot)){throw 'The source task directory does not exist'}
$running=Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('dotnet.exe','testhost.exe','Benchmark.Dynamic.Worker.exe') -and $_.CommandLine -and $_.CommandLine.Contains($sourceRoot) }
if($running){throw 'Owned verification processes are still running'}
$names=@(Get-ChildItem -LiteralPath $targetRoot -Directory | ForEach-Object Name)+@('ui-driver-availability-final2','ui-release-final3','generator-release-current-final1','dynamic-release-current-contracts-final1','worker-regression-persistence-final1','worker-regression-persistence-final2','dynamic-core-debug-current-final1')
$owners=@{}
foreach($name in $names){$id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($name))).Substring(0,8);$owners[$id]=$name}
$roots=@([pscustomobject]@{Source=$sourceRoot;Destination=$targetRoot;Owner='test-retirement-20261104'})
foreach($directory in Get-ChildItem -LiteralPath $sourceData -Directory){
    if($owners.ContainsKey($directory.Name)){
        $roots+=[pscustomobject]@{Source=$directory.FullName;Destination=(Join-Path $targetData $directory.Name);Owner=$owners[$directory.Name]}
    }
}
$files=[Collections.Generic.List[object]]::new()
$directories=[Collections.Generic.List[string]]::new()
foreach($root in $roots){
    $resolved=[IO.Path]::GetFullPath($root.Source)
    if($resolved -ne $sourceRoot -and ($resolved -notmatch '^D:\\CodexTemp\\stm\\[0-9A-F]{8}$' -or -not $owners.ContainsKey([IO.Path]::GetFileName($resolved)))){throw "Unsafe source: $resolved"}
    $items=@(Get-Item -LiteralPath $resolved)+@(Get-ChildItem -LiteralPath $resolved -Force -Recurse)
    if(@($items | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count){throw "Reparse point found under $resolved"}
    foreach($item in $items){
        $relative=[IO.Path]::GetRelativePath($resolved,$item.FullName)
        $destination=[IO.Path]::GetFullPath((Join-Path $root.Destination $relative))
        if($destination -ne $root.Destination -and -not $destination.StartsWith($root.Destination+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Destination escaped its root'}
        if($item.PSIsContainer){$directories.Add($destination);continue}
        if($item.FullName -eq (Join-Path $sourceRoot 'artifact-access.lock')){continue}
        if((Test-Path -LiteralPath $destination) -and (Get-Item -LiteralPath $destination).Length -ne $item.Length){throw "Incomplete destination requires review: $destination"}
        $files.Add([pscustomobject]@{Source=$item.FullName;Destination=$destination;Bytes=$item.Length;SHA256=$null})
    }
}
$bytes=($files | Measure-Object Bytes -Sum).Sum
if((Get-PSDrive F).Free -lt $bytes+1GB){throw 'Insufficient free space on F'}
$sourceLock=$null;$targetLock=$null
$ledger=Join-Path $targetRoot 'd-to-f-transfer.json'
if(Test-Path -LiteralPath $ledger){throw 'A transfer ledger already exists'}
try{
    $sourceLock=[IO.File]::Open((Join-Path $sourceRoot 'artifact-access.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $targetLock=[IO.File]::Open((Join-Path $targetRoot 'artifact-access.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    foreach($directory in $directories){[IO.Directory]::CreateDirectory($directory) | Out-Null}
    # 同じ8CPUの範囲内で独立ファイルを処理する。中断済みコピーは内容一致時だけ再利用する。
    $expectedFileCount=$files.Count
    $files=@($files | ForEach-Object -Parallel {
        $ErrorActionPreference='Stop'
        $file=$_
        if(-not [IO.File]::Exists($file.Destination)){[IO.File]::Copy($file.Source,$file.Destination,$false)}
        $sourceStream=[IO.File]::OpenRead($file.Source)
        try{$originalHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sourceStream))}finally{$sourceStream.Dispose()}
        $targetStream=[IO.File]::OpenRead($file.Destination)
        try{$targetHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($targetStream))}finally{$targetStream.Dispose()}
        if($originalHash -ne $targetHash){throw "Hash mismatch: $($file.Source)"}
        $file.SHA256=$originalHash
        $file
    } -ThrottleLimit 8)
    if($files.Count -ne $expectedFileCount -or @($files | Where-Object {-not $_.SHA256}).Count){throw 'A file was not verified'}
    $report=[ordered]@{Status='CopiedAndVerified';FileCount=$files.Count;Bytes=$bytes;Roots=$roots;Files=$files;CompletedAt=$null}
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ledger -Encoding utf8
    $sourceLock.Dispose();$sourceLock=$null
    foreach($root in $roots){
        $resolved=[IO.Path]::GetFullPath($root.Source)
        if($resolved -ne $sourceRoot -and $resolved -notmatch '^D:\\CodexTemp\\stm\\[0-9A-F]{8}$'){throw 'Deletion escaped its verified roots'}
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    # この作業のツールが作った空のNuGetロック置場だけを、再帰削除せず除去する。
    foreach($empty in @('D:\CodexTemp\stm\NuGetScratch\lock','D:\CodexTemp\stm\NuGetScratch','D:\CodexTemp\stm')){
        if((Test-Path -LiteralPath $empty) -and @(Get-ChildItem -LiteralPath $empty -Force).Count -eq 0){Remove-Item -LiteralPath $empty -Force}
    }
    if(@($roots | Where-Object {Test-Path -LiteralPath $_.Source}).Count){throw 'A verified D source remains'}
    $report.Status='MovedAndDRemoved';$report.CompletedAt=[DateTimeOffset]::UtcNow
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ledger -Encoding utf8
    [pscustomobject]@{Status=$report.Status;Files=$files.Count;Bytes=$bytes;OwnedDataRoots=$roots.Count-1;Ledger=$ledger} | ConvertTo-Json
}finally{
    if($sourceLock){$sourceLock.Dispose()}
    if($targetLock){$targetLock.Dispose()}
}
