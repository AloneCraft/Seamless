param([string]$Server='localhost')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
function Open-Database([string]$database) {
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']=$Server; $builder['Initial Catalog']=$database; $builder['Integrated Security']=$true
    $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    $connection.Open()
    return $connection
}
function Execute([System.Data.SqlClient.SqlConnection]$connection,[string]$sql) {
    $command=$connection.CreateCommand(); $command.CommandText=$sql; $command.CommandTimeout=120
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
$master=Open-Database 'master'
try {
    $exists=Execute $master "SELECT COUNT(*) FROM sys.databases WHERE name=N'Seamless_Comparison_Fixture'"
    if ($exists -eq 0) {
        [void](Execute $master 'CREATE DATABASE [Seamless_Comparison_Fixture]')
        $db=Open-Database 'Seamless_Comparison_Fixture'
        try {
            [void](Execute $db @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
CREATE TABLE dbo.FixtureParent (Id int NOT NULL CONSTRAINT PK_FixtureParent PRIMARY KEY, Name nvarchar(100) NOT NULL);
CREATE TABLE dbo.FixtureItem (Id int NOT NULL CONSTRAINT PK_FixtureItem PRIMARY KEY, ParentId int NOT NULL, Amount decimal(18,4) NULL, Note nvarchar(100) NULL, CONSTRAINT FK_FixtureItem_Parent FOREIGN KEY (ParentId) REFERENCES dbo.FixtureParent(Id));
CREATE TABLE dbo.FixtureDuplicates (Value nvarchar(100) NULL);
INSERT dbo.FixtureParent VALUES (1,N'Alpha'),(2,N'Beta');
INSERT dbo.FixtureItem VALUES (1,1,12.3400,N'first'),(2,1,NULL,NULL),(3,2,-2.5000,N'second');
INSERT dbo.FixtureDuplicates VALUES (N'duplicate'),(N'duplicate'),(NULL);
EXEC sys.sp_addextendedproperty @name=N'SeamlessFixtureVersion', @value=N'1';
COMMIT;
"@)
        } finally { $db.Dispose() }
    }
    $db=Open-Database 'Seamless_Comparison_Fixture'
    try {
        $valid=Execute $db @"
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SeamlessFixtureVersion' AND CONVERT(nvarchar(20),value)=N'1') THROW 51000,'Fixture version is absent or mismatched.',1;
IF (SELECT COUNT(*) FROM sys.tables)<>3 THROW 51000,'Fixture table count is mismatched.',1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK')<>2 OR (SELECT COUNT(*) FROM sys.foreign_keys)<>1 THROW 51000,'Fixture key counts differ.',1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK' AND name IN (N'PK_FixtureParent',N'PK_FixtureItem'))<>2 THROW 51000,'Fixture primary keys are missing.',1;
IF (SELECT COUNT(*) FROM sys.key_constraints k JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE k.type='PK' AND ((k.name=N'PK_FixtureParent' AND k.parent_object_id=OBJECT_ID(N'dbo.FixtureParent')) OR (k.name=N'PK_FixtureItem' AND k.parent_object_id=OBJECT_ID(N'dbo.FixtureItem'))) AND ic.key_ordinal=1 AND c.name=N'Id')<>2 OR EXISTS (SELECT 1 FROM sys.key_constraints k JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id WHERE k.name IN (N'PK_FixtureParent',N'PK_FixtureItem') AND ic.key_ordinal>1) THROW 51000,'Fixture primary key columns differ.',1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id WHERE f.name=N'FK_FixtureItem_Parent' AND f.parent_object_id=OBJECT_ID(N'dbo.FixtureItem') AND f.referenced_object_id=OBJECT_ID(N'dbo.FixtureParent') AND pc.name=N'ParentId' AND rc.name=N'Id' AND fc.constraint_column_id=1) OR (SELECT COUNT(*) FROM sys.foreign_key_columns WHERE constraint_object_id=OBJECT_ID(N'dbo.FK_FixtureItem_Parent'))<>1 THROW 51000,'Fixture foreign key columns differ.',1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_FixtureItem_Parent' AND is_disabled=0 AND is_not_trusted=0) THROW 51000,'Fixture foreign key is missing or untrusted.',1;
IF (SELECT COUNT(*) FROM dbo.FixtureParent)<>2 OR (SELECT COUNT(*) FROM dbo.FixtureItem)<>3 OR (SELECT COUNT(*) FROM dbo.FixtureDuplicates)<>3 THROW 51000,'Fixture data differs from baseline.',1;
IF (SELECT COUNT(*) FROM dbo.FixtureDuplicates WHERE Value=N'duplicate')<>2 OR (SELECT COUNT(*) FROM dbo.FixtureDuplicates WHERE Value IS NULL)<>1 THROW 51000,'Fixture duplicate/NULL baseline differs.',1;
DECLARE @expected TABLE (tableName sysname, columnName sysname, typeName sysname, maxLength smallint, precisionValue tinyint, scaleValue tinyint, nullable bit);
INSERT @expected VALUES
(N'FixtureParent',N'Id',N'int',4,10,0,0),(N'FixtureParent',N'Name',N'nvarchar',200,0,0,0),
(N'FixtureItem',N'Id',N'int',4,10,0,0),(N'FixtureItem',N'ParentId',N'int',4,10,0,0),
(N'FixtureItem',N'Amount',N'decimal',9,18,4,1),(N'FixtureItem',N'Note',N'nvarchar',200,0,0,1),
(N'FixtureDuplicates',N'Value',N'nvarchar',200,0,0,1);
IF EXISTS (SELECT tableName,columnName,typeName,maxLength,precisionValue,scaleValue,nullable FROM @expected EXCEPT SELECT t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.schema_id=SCHEMA_ID('dbo'))
OR EXISTS (SELECT t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.schema_id=SCHEMA_ID('dbo') EXCEPT SELECT tableName,columnName,typeName,maxLength,precisionValue,scaleValue,nullable FROM @expected) THROW 51000,'Fixture column definitions differ.',1;
IF EXISTS (SELECT Id,Name FROM dbo.FixtureParent EXCEPT SELECT Id,Name FROM (VALUES (1,N'Alpha'),(2,N'Beta')) v(Id,Name)) THROW 51000,'Fixture parent rows differ.',1;
IF EXISTS (SELECT Id,ParentId,Amount,Note FROM dbo.FixtureItem EXCEPT SELECT Id,ParentId,Amount,Note FROM (VALUES (1,1,CAST(12.3400 AS decimal(18,4)),N'first'),(2,1,CAST(NULL AS decimal(18,4)),CAST(NULL AS nvarchar(100))),(3,2,CAST(-2.5000 AS decimal(18,4)),N'second')) v(Id,ParentId,Amount,Note)) THROW 51000,'Fixture item rows differ.',1;
SELECT 1;
"@
        if ($valid -ne 1) { throw 'Fixture verification failed.' }
        Write-Output 'Fixture verified: 2 parents, 3 items, 3 duplicate rows.'
    } finally { $db.Dispose() }
} finally { $master.Dispose() }
