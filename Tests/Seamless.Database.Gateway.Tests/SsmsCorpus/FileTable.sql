use [AdventureWorks2016]

            CREATE TABLE #file_tables
            (object_id int not null primary key, is_enabled bit null, directory_name nvarchar(256) null, filename_collation_name nvarchar(129) null)
            insert into #file_tables
            select t.object_id, f.is_enabled, f.directory_name, f.filename_collation_name
            from sys.tables as t
            inner join sys.filetables as f on t.object_id = f.object_id
        


SELECT
'Server[@Name=' + quotename(CAST(
        serverproperty(N'Servername')
       AS sysname),'''') + ']' + '/Database[@Name=' + quotename(db_name(),'''') + ']' + '/Table[@Name=' + quotename(tbl.name,'''') + ' and @Schema=' + quotename(SCHEMA_NAME(tbl.schema_id),'''') + ']' AS [Urn],
tbl.name AS [Name],
SCHEMA_NAME(tbl.schema_id) AS [Schema],
CAST(
 case 
    when tbl.is_ms_shipped = 1 then 1
    when exists (
        select 
            1
        from 
            sys.extended_properties 
        where 
            major_id = tbl.object_id and 
            minor_id = 0 and 
            class = 1 and 
            name = N'microsoft_database_tools_support') 
        then 1
    else 0
end          
             AS bit) AS [IsSystemObject],
tbl.is_filetable AS [IsFileTable],
CAST(ISNULL(ft.is_enabled,0) AS bit) AS [FileTableNamespaceEnabled],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type > 1 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasNonClusteredIndex],
CAST(CASE idx.index_id WHEN 1 THEN 1 ELSE 0 END AS bit) AS [HasClusteredIndex],
CAST(case idx.index_id when 1 then case when (idx.is_primary_key + 2*idx.is_unique_constraint = 1) then 1 else 0 end else 0 end AS bit) AS [HasPrimaryClusteredIndex],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type = 3 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasXmlIndex],
CAST(ISNULL((select top 1 1 from sys.all_columns as clmns join sys.types as usrt on usrt.user_type_id = clmns.user_type_id where clmns.object_id = tbl.object_id and usrt.name = N'xml'), 0) AS bit) AS [HasXmlData],
CAST(ISNULL((select top 1 1 from sys.all_columns as clmns join sys.types as usrt on usrt.user_type_id = clmns.user_type_id where clmns.object_id = tbl.object_id and usrt.name in (N'geometry', N'geography')), 0) AS bit) AS [HasSpatialData],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type = 6 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasNonClusteredColumnStoreIndex],
CAST(tbl.is_memory_optimized AS bit) AS [IsMemoryOptimized]
FROM
sys.tables AS tbl
LEFT OUTER JOIN #file_tables AS ft ON ft.object_id = tbl.object_id
INNER JOIN sys.indexes AS idx ON 
        idx.object_id = tbl.object_id and (idx.index_id < <msparam>2</msparam>  or (tbl.is_memory_optimized = 1 and idx.index_id = (select min(index_id) from sys.indexes where object_id = tbl.object_id)))
      
WHERE
(CAST(
 case 
    when tbl.is_ms_shipped = 1 then 1
    when exists (
        select 
            1
        from 
            sys.extended_properties 
        where 
            major_id = tbl.object_id and 
            minor_id = 0 and 
            class = 1 and 
            name = N'microsoft_database_tools_support') 
        then 1
    else 0
end          
             AS bit)=<msparam>0</msparam> and tbl.is_filetable=<msparam>1</msparam>)

            DROP TABLE #file_tables
        
