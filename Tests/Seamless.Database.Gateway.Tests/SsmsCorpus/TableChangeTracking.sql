
            create table #change_tracking_tables
            (object_id int not null primary key, is_track_columns_updated_on bit null)
            insert into #change_tracking_tables
            select t.object_id, c.is_track_columns_updated_on
            from sys.tables t
            inner join sys.change_tracking_tables as c on c.object_id = t.object_id
        


            CREATE TABLE #file_tables
            (object_id int not null primary key, is_enabled bit null, directory_name nvarchar(256) null, filename_collation_name nvarchar(129) null)
            insert into #file_tables
            select t.object_id, f.is_enabled, f.directory_name, f.filename_collation_name
            from sys.tables as t
            inner join sys.filetables as f on t.object_id = f.object_id
        


SELECT
tbl.name AS [Name],
tbl.object_id AS [ID],
tbl.create_date AS [CreateDate],
tbl.modify_date AS [DateLastModified],
ISNULL(stbl.name, N'') AS [Owner],
CAST(case when tbl.principal_id is null then 1 else 0 end AS bit) AS [IsSchemaOwned],
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
CAST(OBJECTPROPERTY(tbl.object_id, N'HasAfterTrigger') AS bit) AS [HasAfterTrigger],
CAST(OBJECTPROPERTY(tbl.object_id, N'HasInsertTrigger') AS bit) AS [HasInsertTrigger],
CAST(OBJECTPROPERTY(tbl.object_id, N'HasDeleteTrigger') AS bit) AS [HasDeleteTrigger],
CAST(OBJECTPROPERTY(tbl.object_id, N'HasInsteadOfTrigger') AS bit) AS [HasInsteadOfTrigger],
CAST(OBJECTPROPERTY(tbl.object_id, N'HasUpdateTrigger') AS bit) AS [HasUpdateTrigger],
CAST(OBJECTPROPERTY(tbl.object_id, N'IsIndexed') AS bit) AS [HasIndex],
CAST(OBJECTPROPERTY(tbl.object_id, N'IsIndexable') AS bit) AS [IsIndexable],
CAST(CASE idx.index_id WHEN 1 THEN 1 ELSE 0 END AS bit) AS [HasClusteredIndex],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type > 1 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasNonClusteredIndex],
CAST(case idx.index_id when 1 then case when (idx.is_primary_key + 2*idx.is_unique_constraint = 1) then 1 else 0 end else 0 end AS bit) AS [HasPrimaryClusteredIndex],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type = 6 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasNonClusteredColumnStoreIndex],
CAST(ISNULL((select top 1 1 from sys.indexes ind where ind.object_id = tbl.object_id and ind.type = 3 and ind.is_hypothetical = 0 ), 0) AS bit) AS [HasXmlIndex],
CAST(CASE idx.type WHEN 0 THEN 1 ELSE 0 END AS bit) AS [HasHeapIndex],
CAST(ISNULL((select top 1 1 from sys.all_columns as clmns join sys.types as usrt on usrt.user_type_id = clmns.user_type_id where clmns.object_id = tbl.object_id and usrt.name = N'xml'), 0) AS bit) AS [HasXmlData],
CAST(ISNULL((select top 1 1 from sys.all_columns as clmns join sys.types as usrt on usrt.user_type_id = clmns.user_type_id where clmns.object_id = tbl.object_id and usrt.name in (N'geometry', N'geography')), 0) AS bit) AS [HasSpatialData],
tbl.uses_ansi_nulls AS [AnsiNullsStatus],
CAST(ISNULL(OBJECTPROPERTY(tbl.object_id,N'IsQuotedIdentOn'),1) AS bit) AS [QuotedIdentifierStatus],
CAST(0 AS bit) AS [FakeSystemTable],
ISNULL(dstext.name,N'') AS [TextFileGroup],
CAST(tbl.is_memory_optimized AS bit) AS [IsMemoryOptimized],
case when (tbl.durability=1) then 0 else 1 end AS [Durability],
tbl.is_replicated AS [Replicated],
tbl.lock_escalation AS [LockEscalation],
CAST(case when ctt.object_id is null then 0 else 1  end AS bit) AS [ChangeTrackingEnabled],
CAST(ISNULL(ctt.is_track_columns_updated_on,0) AS bit) AS [TrackColumnsUpdatedEnabled],
CAST(CASE tbl.ledger_type WHEN 2 THEN 1 WHEN 3 THEN 1 ELSE 0 END AS bit) AS [IsLedger],
CAST(ISNULL(tbl.ledger_type, 0) AS int) AS [LedgerType],
CAST(ISNULL(tbl.is_dropped_ledger_table, 0) AS bit) AS [IsDroppedLedgerTable],
CAST(CASE WHEN (tbl.temporal_type = 2) OR (tbl.ledger_type = 2) THEN 1 ELSE 0 END AS bit) AS [IsSystemVersioned],
tbl.is_filetable AS [IsFileTable],
ISNULL(ft.directory_name,N'') AS [FileTableDirectoryName],
ISNULL(ft.filename_collation_name,N'') AS [FileTableNameColumnCollation],
CAST(ISNULL(ft.is_enabled,0) AS bit) AS [FileTableNamespaceEnabled],
CASE WHEN 'PS'=dsidx.type THEN dsidx.name ELSE N'' END AS [PartitionScheme],
CAST(CASE WHEN 'PS'=dsidx.type THEN 1 ELSE 0 END AS bit) AS [IsPartitioned],
CASE WHEN 'FD'=dstbl.type THEN dstbl.name ELSE N'' END AS [FileStreamFileGroup],
CASE WHEN 'PS'=dstbl.type THEN dstbl.name ELSE N'' END AS [FileStreamPartitionScheme],
CAST(CASE idx.type WHEN 5 THEN 1 ELSE 0 END AS bit) AS [HasClusteredColumnStoreIndex],
CAST(ISNULL(historyTable.name, N'') AS sysname) AS [HistoryTableName],
CAST(ISNULL(SCHEMA_NAME(historyTable.schema_id), N'') AS sysname) AS [HistoryTableSchema],
CAST(ISNULL(historyTable.object_id, 0) AS int) AS [HistoryTableID],
CAST(CASE WHEN periods.start_column_id IS NULL THEN 0 ELSE 1 END AS bit) AS [HasSystemTimePeriod],
CAST(
        ISNULL((SELECT cols.name
        FROM sys.columns cols
        WHERE periods.object_id = tbl.object_id
        AND cols.object_id = tbl.object_id
        AND cols.column_id = periods.start_column_id), N'')
       AS sysname) AS [SystemTimePeriodStartColumn],
CAST(
        ISNULL((SELECT cols.name
        FROM sys.columns cols
        WHERE periods.object_id = tbl.object_id
        AND cols.object_id = tbl.object_id
        AND cols.column_id = periods.end_column_id), N'')
       AS sysname) AS [SystemTimePeriodEndColumn],
tbl.temporal_type AS [TemporalType],
CAST(0 AS bit) AS [RemoteDataArchiveEnabled],
CAST(0 AS tinyint) AS [RemoteDataArchiveDataMigrationState],
CAST('' AS varchar(4000)) AS [RemoteDataArchiveFilterPredicate],
CAST(N'' AS sysname) AS [RemoteTableName],
CAST(0 AS bit) AS [RemoteTableProvisioned],
CAST(tbl.is_external AS bit) AS [IsExternal],
CAST(tbl.is_node AS bit) AS [IsNode],
CAST(tbl.is_edge AS bit) AS [IsEdge]
FROM
sys.tables AS tbl
LEFT OUTER JOIN sys.database_principals AS stbl ON stbl.principal_id = ISNULL(tbl.principal_id, (OBJECTPROPERTY(tbl.object_id, 'OwnerId')))
INNER JOIN sys.indexes AS idx ON 
        idx.object_id = tbl.object_id and (idx.index_id < @_msparam_0  or (tbl.is_memory_optimized = 1 and idx.index_id = (select min(index_id) from sys.indexes where object_id = tbl.object_id)))
      
LEFT OUTER JOIN sys.data_spaces AS dstext  ON tbl.lob_data_space_id = dstext.data_space_id
LEFT OUTER JOIN #change_tracking_tables AS ctt ON ctt.object_id = tbl.object_id 
LEFT OUTER JOIN #file_tables AS ft ON ft.object_id = tbl.object_id
LEFT OUTER JOIN sys.data_spaces AS dsidx ON dsidx.data_space_id = idx.data_space_id
LEFT OUTER JOIN sys.tables AS t ON t.object_id = idx.object_id
LEFT OUTER JOIN sys.data_spaces AS dstbl ON dstbl.data_space_id = t.Filestream_data_space_id and (idx.index_id < 2 or (idx.type = 7 and idx.index_id < 3))
LEFT OUTER JOIN sys.tables as historyTable ON historyTable.object_id = tbl.history_table_id
LEFT OUTER JOIN sys.periods as periods ON periods.object_id = tbl.object_id
WHERE
(tbl.name=@_msparam_1 and SCHEMA_NAME(tbl.schema_id)=@_msparam_2)

            DROP TABLE #change_tracking_tables
        


            DROP TABLE #file_tables
        
