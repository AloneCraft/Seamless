#Requires -Version 5.1
<#
.SYNOPSIS
本物の SQL Server から、システムのビュー・表・ストアド・関数の「形」を読み取り、定義ファイル(TSV)へ書き出す。

.DESCRIPTION
読み取りだけを行い、SQL Server 側は何も変更しない。
写し取る範囲は次の3つ。
  system : sys と INFORMATION_SCHEMA のオブジェクト(どのDBでも同じもの。master で読む)
  master : master の dbo にある出荷時オブジェクト
  msdb   : msdb の dbo にある出荷時オブジェクト
書き出すのは名前・型・列・引数・結果の列だけで、リポジトリへ入れてよい事実情報に限る。
本物の T-SQL 本文(Microsoft が書いたもの)は -SourceDirectory を指定したときだけ、別の場所へ書き出す。
リポジトリの中へは書き出さないこと。
#>
param(
    [string]$Server='localhost',
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$SourceDirectory,
    # 利用者のオブジェクトが載る sys ビューの「いつもの値」を読み取るデータベース。
    [string]$SampleDatabase='AdventureWorks2016'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data

$scopes=@(
    @{Scope='system';Database='master';Schemas="'sys','INFORMATION_SCHEMA'";Types="'V','S','U','P','X','PC','FN','IF','TF','FS','FT','AF'"},
    @{Scope='master';Database='master';Schemas="'dbo'";Types="'U','V','P','X','FN','IF','TF','SN'"},
    @{Scope='msdb';Database='msdb';Schemas="'dbo'";Types="'U','V','P','X','FN','IF','TF','SN'"}
)
$bin='COLLATE Latin1_General_BIN2'

function Invoke-Rows([System.Data.SqlClient.SqlConnection]$connection,[string]$sql){
    $command=$connection.CreateCommand();$command.CommandText=$sql;$command.CommandTimeout=1800
    $reader=$command.ExecuteReader()
    try{
        while($reader.Read()){
            $values=New-Object object[] $reader.FieldCount;[void]$reader.GetValues($values)
            ,$values
        }
    }finally{$reader.Dispose()}
}
function Format-Field($value){
    if($value -is [System.DBNull] -or $null -eq $value){return ''}
    if($value -is [bool]){if($value){return '1'}else{return '0'}}
    $text=[string]$value
    if($text -match "[`t`r`n\\]"){throw "TSVに入れられない文字を含む値: $text"}
    return $text
}
function Write-Tsv([string]$path,[string[]]$header,[System.Collections.Generic.List[string]]$lines){
    $all=New-Object System.Collections.Generic.List[string];$all.Add(($header -join "`t"));$all.AddRange($lines)
    [System.IO.File]::WriteAllText($path,(($all -join "`n")+"`n"),(New-Object System.Text.UTF8Encoding($false)))
}

$objects=New-Object System.Collections.Generic.List[string]
$columns=New-Object System.Collections.Generic.List[string]
$parameters=New-Object System.Collections.Generic.List[string]
$results=New-Object System.Collections.Generic.List[string]
$version=$null
foreach($s in $scopes){
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$Server;$builder['Initial Catalog']=$s.Database;$builder['Integrated Security']=$true
    $builder['Encrypt']=$true;$builder['TrustServerCertificate']=$true;$builder['Application Name']='SeamlessSystemCatalogExport'
    $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
    try{
        if(-not $version){$version=@(Invoke-Rows $connection "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40)),CAST(SERVERPROPERTY('Edition') AS nvarchar(128))")[0]}
        $filter="o.is_ms_shipped=1 AND SCHEMA_NAME(o.schema_id) IN ($($s.Schemas)) AND o.type IN ($($s.Types))"
        $order="SCHEMA_NAME(o.schema_id) $bin,o.name $bin"
        # 結果の列は手続きだけ。読めないもの(一時表や動的SQLを使うもの)は理由を残す。
        $state=@{}
        $resultRows=Invoke-Rows $connection @"
SELECT SCHEMA_NAME(o.schema_id),o.name,r.column_ordinal,r.name,r.system_type_name,r.is_nullable,r.error_number,r.error_type_desc
FROM sys.all_objects AS o OUTER APPLY sys.dm_exec_describe_first_result_set_for_object(o.object_id,0) AS r
WHERE $filter AND o.type IN ('P','X','PC')
ORDER BY $order,r.column_ordinal
"@
        foreach($r in $resultRows){
            $key="$($r[0]).$($r[1])"
            if($r[6] -isnot [System.DBNull]){$state[$key]="error:$($r[7])";continue}
            if($r[2] -is [System.DBNull]){if(-not $state.ContainsKey($key)){$state[$key]='none'};continue}
            $state[$key]='columns'
            $results.Add((@($s.Scope,$r[0],$r[1],$r[2],$r[3],$r[4],$r[5])|ForEach-Object{Format-Field $_}) -join "`t")
        }
        foreach($r in Invoke-Rows $connection "SELECT SCHEMA_NAME(o.schema_id),o.name,RTRIM(o.type),o.object_id FROM sys.all_objects AS o WHERE $filter ORDER BY $order"){
            $key="$($r[0]).$($r[1])";$resultState=if($state.ContainsKey($key)){$state[$key]}else{''}
            $objects.Add((@($s.Scope,$r[0],$r[1],$r[2],$r[3],$resultState)|ForEach-Object{Format-Field $_}) -join "`t")
        }
        foreach($r in Invoke-Rows $connection @"
SELECT SCHEMA_NAME(o.schema_id),o.name,c.column_id,c.name,TYPE_NAME(c.user_type_id),c.max_length,c.precision,c.scale,c.is_nullable,c.collation_name
FROM sys.all_objects AS o INNER JOIN sys.all_columns AS c ON c.object_id=o.object_id
WHERE $filter ORDER BY $order,c.column_id
"@){$columns.Add((@($s.Scope)+$r|ForEach-Object{Format-Field $_}) -join "`t")}
        foreach($r in Invoke-Rows $connection @"
SELECT SCHEMA_NAME(o.schema_id),o.name,p.parameter_id,p.name,TYPE_NAME(p.user_type_id),p.max_length,p.precision,p.scale,p.is_output,p.has_default_value
FROM sys.all_objects AS o INNER JOIN sys.all_parameters AS p ON p.object_id=o.object_id
WHERE $filter ORDER BY $order,p.parameter_id
"@){$parameters.Add((@($s.Scope)+$r|ForEach-Object{Format-Field $_}) -join "`t")}
        if($SourceDirectory){
            $target=Join-Path $SourceDirectory $s.Scope;New-Item -ItemType Directory -Path $target -Force|Out-Null
            foreach($r in Invoke-Rows $connection "SELECT SCHEMA_NAME(o.schema_id),o.name,OBJECT_DEFINITION(o.object_id) FROM sys.all_objects AS o WHERE $filter AND OBJECT_DEFINITION(o.object_id) IS NOT NULL ORDER BY $order"){
                [System.IO.File]::WriteAllText((Join-Path $target "$($r[0]).$($r[1]).sql"),[string]$r[2],(New-Object System.Text.UTF8Encoding($false)))
            }
        }
    }finally{$connection.Dispose()}
}


# 利用者の表・列などが載る sys ビューで、ほとんどの行が同じ値を持つ数値・bit の列を typical_values.tsv に書く。
# Seamless のカタログが値を持たない列は、本物と同じ「いつもの値」(例: is_dropped_ledger_column は 0、graph_type は NULL)にする。
# 行ごとに違う値(名前・番号・日付・件数など)は書かない。基準は、異なる値が3つ以下で、最も多い値が9割以上を占める列。
$typical=New-Object System.Collections.Generic.List[string]
$sampleBuilder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$sampleBuilder['Data Source']=$Server;$sampleBuilder['Initial Catalog']=$SampleDatabase;$sampleBuilder['Integrated Security']=$true
$sampleBuilder['Encrypt']=$true;$sampleBuilder['TrustServerCertificate']=$true;$sampleBuilder['Application Name']='SeamlessSystemCatalogExport'
$sample=New-Object System.Data.SqlClient.SqlConnection($sampleBuilder.ConnectionString);$sample.Open()
try{
    $numeric=@('bit','tinyint','smallint','int','bigint','real','float','decimal','numeric')
    $columnsByView=$columns|ForEach-Object{$f=$_ -split "`t";[pscustomobject]@{Scope=$f[0];Schema=$f[1];Object=$f[2];Name=$f[4];Type=$f[5]}}|Where-Object{$_.Scope -eq 'system' -and $_.Schema -eq 'sys'}|Group-Object Object
    $views=@{};foreach($o in $objects){$f=$o -split "`t";if($f[0] -eq 'system' -and $f[1] -eq 'sys' -and $f[3] -eq 'V'){$views[$f[2]]=1}}
    foreach($group in $columnsByView){
        if(-not $views.ContainsKey($group.Name)){continue}
        $names=@($group.Group|ForEach-Object{$_.Name})
        $filter=if($names -contains 'object_id'){' WHERE object_id IN (SELECT object_id FROM sys.objects WHERE is_ms_shipped=0)'}else{''}
        try{
            $rows=@(Invoke-Rows $sample "SELECT TOP 2000 * FROM sys.[$($group.Name)]$filter")
        }catch{continue}
        if($rows.Count -eq 0){continue}
        for($i=0;$i -lt $group.Group.Count;$i++){
            $column=$group.Group[$i]
            if($numeric -notcontains $column.Type){continue}
            $counts=@{};foreach($row in $rows){$v=if($row[$i] -is [System.DBNull]){'NULL'}else{Format-Field $row[$i]};$counts[$v]=1+[int]$counts[$v]}
            $top=$counts.GetEnumerator()|Sort-Object Value -Descending|Select-Object -First 1
            if($counts.Count -le 3 -and $top.Value -ge 0.9*$rows.Count -and $top.Key -ne 'NULL'){$typical.Add((@('system','sys',$group.Name,$column.Name,$top.Key) -join "`t"))}
        }
    }
}finally{$sample.Dispose()}

New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
Write-Tsv (Join-Path $OutputDirectory 'objects.tsv') @('scope','schema','name','type','object_id','result_set') $objects
Write-Tsv (Join-Path $OutputDirectory 'columns.tsv') @('scope','schema','object','column_id','name','type','max_length','precision','scale','is_nullable','collation') $columns
Write-Tsv (Join-Path $OutputDirectory 'parameters.tsv') @('scope','schema','object','parameter_id','name','type','max_length','precision','scale','is_output','has_default_value') $parameters
Write-Tsv (Join-Path $OutputDirectory 'typical_values.tsv') @('scope','schema','object','column','value') $typical
Write-Tsv (Join-Path $OutputDirectory 'result_columns.tsv') @('scope','schema','object','ordinal','name','type','is_nullable') $results
[System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'source.txt'),"product_version`t$($version[0])`nedition`t$($version[1])`n",(New-Object System.Text.UTF8Encoding($false)))
Write-Output ("objects={0} columns={1} parameters={2} result_columns={3} version={4}" -f $objects.Count,$columns.Count,$parameters.Count,$results.Count,$version[0])
