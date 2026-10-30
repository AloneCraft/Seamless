param([string]$Server='localhost',[Parameter(Mandatory)][string]$FlatDirectory,[Parameter(Mandatory)][string]$ManifestPath)
$ErrorActionPreference='Stop'
function Get-FileHash([Parameter(Mandatory)][string]$LiteralPath,[string]$Algorithm='SHA256') {
    if($Algorithm -ne 'SHA256'){throw 'Only SHA256 is supported.'}
    $stream=[IO.File]::OpenRead($LiteralPath);$hash=[Security.Cryptography.SHA256]::Create()
    try{return [pscustomobject]@{Hash=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}}finally{$hash.Dispose();$stream.Dispose()}
}

Add-Type -AssemblyName System.Data
Add-Type -Path (Join-Path $PSScriptRoot 'TpcEFlatLoader.cs') -ReferencedAssemblies System.Data,System.Xml
function Open-Database([string]$name) {
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$Server; $builder['Initial Catalog']=$name; $builder['Integrated Security']=$true
    $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString); $connection.Open(); return $connection
}
function Exec([System.Data.SqlClient.SqlConnection]$connection,[string]$sql) {
    $command=$connection.CreateCommand();$command.CommandTimeout=0;$command.CommandText=$sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
$tables=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'tpce-tables.json')|ConvertFrom-Json
$sql=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'tpce-schema.sql'))
$statements=[regex]::Split($sql,'\r?\n\s*\r?\n')|Where-Object {$_.Trim()}
if ($tables.Count -ne 33 -or @($statements|Where-Object {$_ -match '^CREATE TABLE'}).Count -ne 33) {throw 'Schema must contain 33 tables.'}
foreach($table in $tables) {
    if($table.name -ne 'TRADE_REQUEST' -and -not (Test-Path -LiteralPath (Join-Path $FlatDirectory $table.file) -PathType Leaf)) {throw "Missing EGen output: $($table.file)"}
}
if(-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {throw 'The verified EGen generation manifest is required.'}
$manifest=Get-Content -Raw -Encoding UTF8 $ManifestPath|ConvertFrom-Json
if ($manifest.customers -ne 1000 -or $manifest.days -ne 10 -or $manifest.scaleFactor -ne 500 -or $manifest.status -notin @('generated','completed')) {throw 'EGen manifest does not describe the required generation.'}
foreach($table in $tables) {
    $path=Join-Path $FlatDirectory $table.file
    if($table.name -eq 'TRADE_REQUEST' -and -not (Test-Path -LiteralPath $path)) {continue}
    $entry=$manifest.outputSha256.PSObject.Properties[$table.file]
    if($null -eq $entry -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value) {throw "EGen output hash mismatch: $($table.file)"}
}
function Get-SchemaFingerprint([System.Data.SqlClient.SqlConnection]$connection) {
    return Exec $connection @"
SELECT CONVERT(varchar(64), HASHBYTES('SHA2_256',CONCAT(
(SELECT s.name AS schema_name,t.name AS table_name,c.column_id,c.name AS column_name,ty.name AS type_name,c.max_length,c.precision,c.scale,c.is_nullable FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id ORDER BY s.name,t.name,c.column_id FOR JSON PATH),
(SELECT t.name AS table_name,k.name AS key_name,ic.key_ordinal,c.name AS column_name FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id ORDER BY t.name,k.name,ic.key_ordinal FOR JSON PATH),
(SELECT t.name AS table_name,f.name AS foreign_key_name,rt.name AS target_table,fc.constraint_column_id,c.name AS column_name,rc.name AS target_column,f.is_disabled,f.is_not_trusted FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.tables rt ON rt.object_id=f.referenced_object_id JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fc.referenced_column_id ORDER BY t.name,f.name,fc.constraint_column_id FOR JSON PATH))),2)
"@
}
$master=Open-Database 'master'
try {
    if ((Exec $master "SELECT COUNT(*) FROM sys.databases WHERE name=N'TPC_E'") -ne 0) {
        if($manifest.status -ne 'completed') {throw 'TPC_E exists without a completed matching manifest; refusing to modify it.'}
        $db=Open-Database 'TPC_E'
        try {
            $marker=Exec $db "SELECT CONVERT(nvarchar(64),value) FROM sys.extended_properties WHERE class=0 AND name=N'TpcEManifestId'"
            if($marker -ne $manifest.manifestId) {throw 'TPC_E identity does not match the manifest.'}
            if((Get-SchemaFingerprint $db) -ne $manifest.schemaFingerprint) {throw 'TPC_E schema differs from the manifest.'}
            if((Exec $db "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')") -ne 33 -or (Exec $db "SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK'") -ne 33 -or (Exec $db 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0') -ne 48) {throw 'TPC_E constraint counts differ.'}
            foreach($table in $tables) {
                $actual=Exec $db "SELECT COUNT_BIG(*) FROM dbo.[$($table.name)]"
                if($actual -ne $manifest.tableCounts.($table.name)) {throw "TPC_E row count mismatch: $($table.name)"}
            }
            Write-Output 'TPC_E validated and reused.'
            return
        } finally {$db.Dispose()}
    }
    if($manifest.status -ne 'generated') {throw 'The manifest is completed but TPC_E is absent; refusing to recreate it.'}
    [void](Exec $master 'CREATE DATABASE [TPC_E]')
    [void](Exec $master 'ALTER DATABASE [TPC_E] SET ALLOW_SNAPSHOT_ISOLATION ON')
    $db=Open-Database 'TPC_E'
    try {
        foreach($s in $statements|Where-Object {$_ -match '^CREATE TABLE'}) {[void](Exec $db $s)}
        $counts=@{}
        foreach($table in $tables) {
            $path=Join-Path $FlatDirectory $table.file
            if($table.name -eq 'TRADE_REQUEST' -and -not (Test-Path -LiteralPath $path)) {$rows=0}
            else {
                $rows=[TpcEFlatLoader]::Load($db,$path,$table.name,[string[]]@($table.columns|ForEach-Object {$_.name}),[string[]]@($table.columns|ForEach-Object {$_.type}),[bool[]]@($table.columns|ForEach-Object {$_.nullable}))
            }
            $actual=Exec $db "SELECT COUNT_BIG(*) FROM dbo.[$($table.name)]"
            if($rows -ne $actual -or $rows -ne $manifest.tableCounts.($table.name)) {throw "Count mismatch: $($table.name)"}
            $counts[$table.name]=$actual;Write-Output "$($table.name): $actual"
        }
        foreach($s in $statements|Where-Object {$_ -match '^ALTER TABLE'}) {[void](Exec $db $s)}
        if((Exec $db 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1') -ne 0) {throw 'Foreign keys are disabled or untrusted.'}
        $manifest.tableCounts=$counts
        $manifestId=[guid]::NewGuid().ToString('N')
        [void](Exec $db "EXEC sys.sp_addextendedproperty @name=N'TpcEManifestId', @value=N'$manifestId'")
        $manifest|Add-Member -NotePropertyName manifestId -NotePropertyValue $manifestId
        $manifest|Add-Member -NotePropertyName schemaFingerprint -NotePropertyValue (Get-SchemaFingerprint $db)
        $manifest.status='completed'
        $temporary=$ManifestPath+'.tmp'
        $manifest|ConvertTo-Json -Depth 10|Set-Content -Encoding UTF8 -LiteralPath $temporary
        [IO.File]::Replace($temporary,$ManifestPath,($ManifestPath+'.previous'))
    } finally {$db.Dispose()}
} finally {$master.Dispose()}
