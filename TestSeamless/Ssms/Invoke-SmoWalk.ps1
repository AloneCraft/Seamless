#Requires -Version 5.1
<#
.SYNOPSIS
SSMS と同じ部品(SMO)で、サーバー・データベース・各種オブジェクトの全プロパティとスクリプト生成を一通り実行し、結果を1行JSONで書き出す。

.DESCRIPTION
SSMS で手作業で操作しなくても、SSMS/SMO が送る問い合わせを網羅的に発生させるための巡回スクリプト。
Seamless(要求の記録あり)と本物の SQL Server の両方に流し、失敗した SQL と値の違いをまとめて洗い出す。

読み取りだけを行う。プロパティの読み取り、Refresh、Script() 以外のメソッド(Create/Alter/Drop など)は呼ばない。
各オブジェクト・各プロパティの失敗は記録して先へ進む。

-Target source       : 本物の SQL Server(localhost、Windows 認証、暗号化あり)
-Target <ポート番号> : Seamless(127.0.0.1,<ポート>、sa、パスワードは環境変数 SEAMLESS_TDS_TEST_PASSWORD)

Windows PowerShell 5.1 で動かす(SSMS 22 の SMO は .NET Framework 用)。
#>
param(
    [Parameter(Mandatory)][string]$Target,
    [Parameter(Mandatory)][string]$Output,
    [string[]]$Databases=@('AdventureWorks2016'),
    # 1つのコレクションから巡回する要素の数。SMO が送る SQL の形は種類ごとに同じなので、少数で足りる。
    [int]$ItemsPerCollection=3,
    [int]$MaxDepth=4
)
$ErrorActionPreference='Stop'
$env:TEMP='D:\Seamless\Comparison\Temp';$env:TMP=$env:TEMP
if($Output -notmatch '^F:\\'){throw 'Write the walk output under F:.'}
New-Item -ItemType Directory -Path (Split-Path -Parent $Output) -Force|Out-Null
$base='C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE'
Add-Type -TypeDefinition 'using System; using System.IO; using System.Reflection; public static class SmoWalkResolver { public static void Install(string root) { AppDomain.CurrentDomain.AssemblyResolve += (sender,e) => { var path=Path.Combine(root,new AssemblyName(e.Name).Name+".dll"); if(!File.Exists(path)) path=Path.Combine(root,"PublicAssemblies",new AssemblyName(e.Name).Name+".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; }; } }'
[SmoWalkResolver]::Install($base)
foreach($assembly in @('Microsoft.SqlServer.ConnectionInfo.dll','Microsoft.SqlServer.Management.Sdk.Sfc.dll','Microsoft.SqlServer.Smo.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $base $assembly))}

$writer=New-Object IO.StreamWriter($Output,$false,(New-Object Text.UTF8Encoding($false)))
$visited=New-Object 'System.Collections.Generic.HashSet[string]'
$smoObject=[Microsoft.SqlServer.Management.Smo.SqlSmoObject]
$collectionBase=[Microsoft.SqlServer.Management.Smo.AbstractCollectionBase]
$skipped=@('Parent','ParentCollection','ExecutionManager','ServerVersion','DatabaseEngineType','DatabaseEngineEdition','UserData','Urn','State')

function Write-Record([string]$Path,[string]$Kind,[string]$Name,$Value,$ErrorRecord){
    $text=$null;$hash=$null
    if($null -ne $Value){
        if($Value -is [DateTime]){$text=$Value.ToString('O',[Globalization.CultureInfo]::InvariantCulture)}
        elseif($Value -is [IFormattable]){$text=$Value.ToString($null,[Globalization.CultureInfo]::InvariantCulture)}
        else{$text=[string]$Value}
        if($text.Length -gt 200){
            $sha=[Security.Cryptography.SHA256]::Create()
            try{$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','')}finally{$sha.Dispose()}
            $text=$text.Substring(0,200)
        }
    }
    $message=$null
    if($ErrorRecord){
        $e=$ErrorRecord;if($e -is [Management.Automation.ErrorRecord]){$e=$e.Exception}
        $parts=New-Object Collections.Generic.List[string]
        while($e){$parts.Add($e.Message);$e=$e.InnerException}
        $message=($parts|Select-Object -Unique) -join ' | '
    }
    $writer.WriteLine((ConvertTo-Json -Compress -InputObject ([ordered]@{path=$Path;kind=$Kind;name=$Name;value=$text;sha256=$hash;error=$message})))
}

function Walk($Object,[string]$Path,[int]$Depth){
    $urn=$null;try{$urn=[string]$Object.Urn}catch{}
    if($urn){if(-not $visited.Add($urn)){return}}
    try{
        foreach($property in @($Object.Properties)){
            try{Write-Record $Path 'property' $property.Name $property.Value $null}catch{Write-Record $Path 'error' ('property:'+$property.Name) $null $_}
        }
    }catch{Write-Record $Path 'error' 'properties' $null $_}
    if($Object -is [Microsoft.SqlServer.Management.Smo.IScriptable]){
        try{$lines=@($Object.Script());Write-Record $Path 'script' 'Script' ($lines -join "`n") $null}catch{Write-Record $Path 'error' 'Script' $null $_}
    }
    if($Depth -ge $MaxDepth){return}
    foreach($member in $Object.GetType().GetProperties()){
        if($skipped -contains $member.Name -or $member.GetIndexParameters().Count -gt 0){continue}
        $type=$member.PropertyType
        if($type.IsSubclassOf($collectionBase)){
            $collection=$null
            try{$collection=$member.GetValue($Object,$null);$items=@($collection)}catch{Write-Record $Path 'error' ('collection:'+$member.Name) $null $_;continue}
            Write-Record $Path 'collection' $member.Name $items.Count $null
            if($member.Name -eq 'Databases'){$items=@($items|Where-Object {$Databases -contains $_.Name})}
            else{$items=@($items|Select-Object -First $ItemsPerCollection)}
            foreach($item in $items){
                $name=$null;try{$name=[string]$item.Name}catch{}
                $schema=$null;try{$schema=[string]$item.Schema}catch{}
                if($schema){$name=$schema+'.'+$name}
                Walk $item ($Path+'/'+$member.Name+'['+$name+']') ($Depth+1)
            }
        }elseif($type.IsSubclassOf($smoObject)){
            $child=$null
            try{$child=$member.GetValue($Object,$null)}catch{Write-Record $Path 'error' ('object:'+$member.Name) $null $_;continue}
            if($null -ne $child){Walk $child ($Path+'/'+$member.Name) ($Depth+1)}
        }
    }
}

if($Target -eq 'source'){
    $connection=New-Object Microsoft.SqlServer.Management.Common.ServerConnection('localhost')
    $connection.EncryptConnection=$true
}else{
    if(-not $env:SEAMLESS_TDS_TEST_PASSWORD){throw 'Set SEAMLESS_TDS_TEST_PASSWORD for the Seamless target.'}
    $connection=New-Object Microsoft.SqlServer.Management.Common.ServerConnection(('127.0.0.1,'+$Target),'sa',$env:SEAMLESS_TDS_TEST_PASSWORD)
    $connection.EncryptConnection=$false
}
$connection.TrustServerCertificate=$true;$connection.ConnectTimeout=10;$connection.StatementTimeout=120;$connection.DatabaseName=$Databases[0]
$started=[DateTime]::UtcNow
try{
    $connection.Connect()
    $server=New-Object Microsoft.SqlServer.Management.Smo.Server($connection)
    # SSMS と同じく、一覧の取得時に全フィールドを読む。オブジェクトごとの追加取得が減る。
    $server.SetDefaultInitFields($true)
    Walk $server 'Server' 0
}catch{
    Write-Record 'Server' 'error' 'walk' $null $_
}finally{
    if($connection.IsOpen){$connection.Disconnect()}
    $writer.Dispose()
}
Write-Output ("walk done in {0:n0}s -> {1}" -f ([DateTime]::UtcNow-$started).TotalSeconds,$Output)
