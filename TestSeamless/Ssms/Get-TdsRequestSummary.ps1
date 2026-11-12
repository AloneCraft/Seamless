#Requires -Version 5.1
<#
.SYNOPSIS
SEAMLESS_TDS_REQUEST_LOG で記録した TDS 要求(1行JSON)を集計し、SSMS互換に足りないものを一覧にする。

.DESCRIPTION
同じ形のSQL(空白・数値・文字列リテラルを畳んだもの)をまとめ、失敗をエラーメッセージ別に数える。
各SQLが参照する sys/INFORMATION_SCHEMA のオブジェクト、関数呼び出し、EXEC/RPC の手続き名も数える。
読み取り専用で、記録ファイルは変更しない。
#>
param(
    [Parameter(Mandatory)][string[]]$Path,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'

function Get-Shape([string]$sql){
    if(-not $sql){return ''}
    $s=[regex]::Replace($sql,"N?'(?:[^']|'')*'","'?'")
    $s=[regex]::Replace($s,'\b\d+(?:\.\d+)?\b','0')
    return ([regex]::Replace($s,'\s+',' ')).Trim()
}
function Add-Count([hashtable]$table,[string]$key){if($key){$table[$key]=1+[int]$table[$key]}}

$records=foreach($file in $Path){Get-Content -LiteralPath $file -Encoding UTF8|Where-Object{$_}|ForEach-Object{$_|ConvertFrom-Json}}
$shapes=@{};$errors=@{};$objects=@{};$failedObjects=@{};$functions=@{};$procedures=@{}
$keywords=@('SELECT','FROM','WHERE','AND','OR','NOT','IN','EXISTS','VALUES','AS','ON','JOIN','APPLY','OVER','INSERT','INTO','EXEC','EXECUTE','IF','WHILE','RETURN','CASE','WHEN','THEN','ELSE','END','BEGIN','UNION','ALL','TABLE','KEY','CHECK','DEFAULT','WITH','OUTPUT','PROCEDURE','FUNCTION','VIEW','INDEX')
foreach($r in $records){
    $text=if($r.sql){$r.sql}else{''}
    $shape=Get-Shape $text
    $key=if($shape){$shape}else{"[$($r.kind)] $($r.procedure)"}
    if(-not $shapes.ContainsKey($key)){$shapes[$key]=[ordered]@{Count=0;Failed=0;Kind=$r.kind;Procedure=$r.procedure;FirstUtc=$r.utc;Error=$null;Sql=$text}}
    $entry=$shapes[$key];$entry.Count++
    if(-not $r.ok){$entry.Failed++;if(-not $entry.Error){$entry.Error=$r.error.message};Add-Count $errors $r.error.message}
    if($r.kind -eq 'rpc' -and $r.procedure -notin @('sp_executesql','sp_prepare','sp_prepexec','sp_execute','sp_unprepare','sp_reset_connection')){Add-Count $procedures $r.procedure}
    $seen=@{}
    foreach($m in [regex]::Matches($text,'(?i)\b(?:\[?(?:master|msdb|tempdb|model)\]?\s*\.\s*)?(?:\[?dbo\]?\s*\.\s*)?\[?(sys|INFORMATION_SCHEMA)\]?\s*\.\s*\[?([A-Za-z_][A-Za-z0-9_$]*)\]?')){
        $name=($m.Groups[1].Value.ToLowerInvariant()+'.'+$m.Groups[2].Value.ToLowerInvariant())
        if(-not $seen.ContainsKey($name)){$seen[$name]=1;Add-Count $objects $name;if(-not $r.ok){Add-Count $failedObjects $name}}
    }
    foreach($m in [regex]::Matches($text,'(?i)(?<![.\w@#])([A-Za-z_][A-Za-z0-9_]*)\s*\(')){
        $name=$m.Groups[1].Value.ToUpperInvariant();if($keywords -notcontains $name){Add-Count $functions $name}
    }
    foreach($m in [regex]::Matches($text,'(?i)\bEXEC(?:UTE)?\s+(?:@\w+\s*=\s*)?([\[\]\w.]+)')){Add-Count $procedures ($m.Groups[1].Value.Replace('[','').Replace(']','').ToLowerInvariant())}
}

New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$sorted=$shapes.GetEnumerator()|Sort-Object {-$_.Value.Failed},{-$_.Value.Count}
$summary=[ordered]@{
    Files=$Path;Requests=@($records).Count;Failed=@($records|Where-Object{-not $_.ok}).Count;DistinctShapes=$shapes.Count
    Errors=$errors.GetEnumerator()|Sort-Object Value -Descending|ForEach-Object{[ordered]@{Message=$_.Key;Count=$_.Value}}
    Objects=$objects.GetEnumerator()|Sort-Object Value -Descending|ForEach-Object{[ordered]@{Name=$_.Key;Requests=$_.Value;FailedRequests=[int]$failedObjects[$_.Key]}}
    Functions=$functions.GetEnumerator()|Sort-Object Value -Descending|ForEach-Object{[ordered]@{Name=$_.Key;Count=$_.Value}}
    Procedures=$procedures.GetEnumerator()|Sort-Object Value -Descending|ForEach-Object{[ordered]@{Name=$_.Key;Count=$_.Value}}
    Shapes=$sorted|ForEach-Object{$_.Value}
}
$summary|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
$md=New-Object System.Collections.Generic.List[string]
$md.Add("# TDS要求の集計");$md.Add('');$md.Add("- 要求数: $($summary.Requests) / 失敗: $($summary.Failed) / SQLの形: $($summary.DistinctShapes)");$md.Add('')
$md.Add('## 失敗の理由');$md.Add('');$md.Add('| 件数 | メッセージ |');$md.Add('|---:|---|')
foreach($e in $summary.Errors){$md.Add("| $($e.Count) | $(($e.Message -replace '\|','\|') -replace '\r?\n',' ') |")}
$md.Add('');$md.Add('## 参照されたシステムオブジェクト');$md.Add('');$md.Add('| 名前 | 要求数 | うち失敗 |');$md.Add('|---|---:|---:|')
foreach($o in $summary.Objects){$md.Add("| $($o.Name) | $($o.Requests) | $($o.FailedRequests) |")}
$md.Add('');$md.Add('## 関数');$md.Add('');$md.Add('| 名前 | 回数 |');$md.Add('|---|---:|')
foreach($f in $summary.Functions){$md.Add("| $($f.Name) | $($f.Count) |")}
$md.Add('');$md.Add('## 手続き(EXEC/RPC)');$md.Add('');$md.Add('| 名前 | 回数 |');$md.Add('|---|---:|')
foreach($p in $summary.Procedures){$md.Add("| $($p.Name) | $($p.Count) |")}
$md|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.md') -Encoding UTF8
Write-Output ("requests={0} failed={1} shapes={2} -> {3}" -f $summary.Requests,$summary.Failed,$summary.DistinctShapes,$OutputDirectory)
