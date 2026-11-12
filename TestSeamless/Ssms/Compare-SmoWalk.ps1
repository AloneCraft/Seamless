#Requires -Version 5.1
<#
.SYNOPSIS
Invoke-SmoWalk.ps1 の結果を、本物の SQL Server と Seamless で突き合わせる。

.DESCRIPTION
同じパス・同じ名前の記録を比べ、次の4種類に分けて summary.md と summary.json に書く。
  seamless-error : Seamless だけが失敗した(本物は値を返した)
  missing        : 本物にはあるが、Seamless の巡回では届かなかった(上の階層の失敗など)
  different      : 両方が値を返したが、値が違う
  extra          : Seamless にだけある
読み取り専用で、入力ファイルは変更しない。
#>
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Target,
    [Parameter(Mandatory)][string]$OutputDirectory,
    # 環境によって必ず違う値(作成日時・ID・パス・サーバー名など)は「違い」から外す。
    [string[]]$IgnoredNames=@('CreateDate','DateLastModified','ID','DatabaseGuid','RecoveryForkGuid','LastBackupDate','LastLogBackupDate','LastDifferentialBackupDate','PrimaryFilePath','DefaultFile','DefaultLog','MasterDBPath','MasterDBLogPath','BackupDirectory','ErrorLogPath','RootDirectory','InstallDataDirectory','InstallSharedDirectory','NetName','ComputerNamePhysicalNetBIOS','Name','Urn','ServiceAccount','ServiceName','Size','SpaceAvailable','DataSpaceUsage','IndexSpaceUsage','RowCount','LastGoodCheckDbTime','FileName','UsedSpace','AvailableMemory','PhysicalMemory','Processors','ProcessorUsage','OSVersion','Platform','HostDistribution','HostRelease','HostServicePackLevel','HostSku','ResourceLastUpdateDateTime','ResourceVersionString','BuildClrVersionString','Collation','CollationID')
)
$ErrorActionPreference='Stop'
function Read-Walk([string]$Path){
    $map=@{}
    foreach($line in [IO.File]::ReadLines($Path,[Text.Encoding]::UTF8)){
        if(-not $line){continue}
        $r=$line|ConvertFrom-Json
        $name=$r.name;if($r.kind -eq 'error'){$name=($r.name -replace '^(property|collection|object):','')}
        $map[$r.path+'|'+$name]=$r
    }
    return $map
}
$source=Read-Walk $Source;$target=Read-Walk $Target
$rows=New-Object Collections.Generic.List[object]
foreach($key in $source.Keys){
    $s=$source[$key];$t=$target[$key]
    if($null -eq $t){$rows.Add([pscustomobject]@{Kind='missing';Path=$s.path;Name=$s.name;Source=$s.value;Target=$null;Error=$null});continue}
    if($t.kind -eq 'error' -and $s.kind -ne 'error'){$rows.Add([pscustomobject]@{Kind='seamless-error';Path=$s.path;Name=$s.name;Source=$s.value;Target=$null;Error=$t.error});continue}
    if($s.kind -eq 'error'){continue}
    if($IgnoredNames -contains $s.name){continue}
    if($s.value -cne $t.value -or $s.sha256 -cne $t.sha256){$rows.Add([pscustomobject]@{Kind='different';Path=$s.path;Name=$s.name;Source=$s.value;Target=$t.value;Error=$null})}
}
foreach($key in $target.Keys){if(-not $source.ContainsKey($key)){$t=$target[$key];$rows.Add([pscustomobject]@{Kind='extra';Path=$t.path;Name=$t.name;Source=$null;Target=$t.value;Error=$t.error})}}

New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$rows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
# パスの [...] を外した「オブジェクトの種類」でまとめる。
function Shape([string]$Path){return ($Path -replace '\[[^\]]*\]','')}
$md=New-Object Collections.Generic.List[string]
$md.Add('# SMO巡回の突き合わせ');$md.Add('')
foreach($kind in @('seamless-error','missing','different','extra')){
    $items=@($rows|Where-Object Kind -eq $kind)
    $md.Add("- $kind : $($items.Count)")
}
$md.Add('');$md.Add('## Seamlessだけが失敗した理由');$md.Add('');$md.Add('| 件数 | 理由 | 例 |');$md.Add('|---:|---|---|')
foreach($g in @($rows|Where-Object Kind -eq 'seamless-error'|Group-Object {(($_.Error -split ' \| ')[-1])}|Sort-Object Count -Descending)){
    $example=$g.Group[0];$reason=(($g.Name -replace '\|','\|') -replace '\r?\n',' ');if($reason.Length -gt 160){$reason=$reason.Substring(0,160)}
    $md.Add("| $($g.Count) | $reason | $(Shape $example.Path) $($example.Name) |")
}
$md.Add('');$md.Add('## 届かなかった種類(本物にはある)');$md.Add('');$md.Add('| 件数 | 種類 |');$md.Add('|---:|---|')
foreach($g in @($rows|Where-Object Kind -eq 'missing'|Group-Object {Shape $_.Path}|Sort-Object Count -Descending)){$md.Add("| $($g.Count) | $($g.Name) |")}
$md.Add('');$md.Add('## 値が違うプロパティ');$md.Add('');$md.Add('| 種類 | 名前 | 本物 | Seamless |');$md.Add('|---|---|---|---|')
foreach($r in @($rows|Where-Object Kind -eq 'different'|Sort-Object {Shape $_.Path},Name)){
    $sv=[string]$r.Source;$tv=[string]$r.Target;if($sv.Length -gt 60){$sv=$sv.Substring(0,60)+'…'};if($tv.Length -gt 60){$tv=$tv.Substring(0,60)+'…'}
    $md.Add("| $(Shape $r.Path) | $($r.Name) | $($sv -replace '\|','\|' -replace '\r?\n',' ') | $($tv -replace '\|','\|' -replace '\r?\n',' ') |")
}
$md|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.md') -Encoding UTF8
Write-Output ("seamless-error={0} missing={1} different={2} extra={3} -> {4}" -f @($rows|Where-Object Kind -eq 'seamless-error').Count,@($rows|Where-Object Kind -eq 'missing').Count,@($rows|Where-Object Kind -eq 'different').Count,@($rows|Where-Object Kind -eq 'extra').Count,$OutputDirectory)
