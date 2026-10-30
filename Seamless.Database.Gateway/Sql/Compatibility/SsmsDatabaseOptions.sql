


          create table #temp_data_retention_property2 (db_id int not null, is_data_retention_enabled bit)

          if serverproperty('engineedition') = 9
          exec sp_executesql N'insert into #temp_data_retention_property2
          select dr_db.database_id, dr_db.is_data_retention_enabled
          from sys.databases dr_db'



SELECT
CAST(ISNULL((select is_data_retention_enabled from #temp_data_retention_property2 as tdrp where tdrp.db_id = dtb.database_id),0) AS bit) AS [DataRetentionEnabled],
dtb.is_auto_create_stats_incremental_on AS [AutoCreateStatisticsIncremental],
dtb.is_ansi_null_default_on AS [AnsiNullDefault],
dtb.is_ansi_nulls_on AS [AnsiNullsEnabled],
dtb.is_ansi_padding_on AS [AnsiPaddingEnabled],
dtb.is_ansi_warnings_on AS [AnsiWarningsEnabled],
dtb.is_arithabort_on AS [ArithmeticAbortEnabled],
dtb.is_auto_create_stats_on AS [AutoCreateStatistics],
dtb.is_auto_shrink_on AS [AutoShrink],
dtb.is_auto_update_stats_on AS [AutoUpdateStatistics],
dtb.is_cursor_close_on_commit_on AS [CloseCursorsOnCommitEnabled],
dtb.is_concat_null_yields_null_on AS [ConcatenateNullYieldsNull],
dtb.is_numeric_roundabort_on AS [NumericRoundAbortEnabled],
dtb.is_quoted_identifier_on AS [QuotedIdentifiersEnabled],
dtb.is_read_only AS [ReadOnly],
dtb.is_recursive_triggers_on AS [RecursiveTriggersEnabled],
dtb.is_local_cursor_default AS [LocalCursorsDefault],
dtb.page_verify_option AS [PageVerify],
dtb.recovery_model AS [RecoveryModel],
dtb.user_access AS [UserAccess],
dtb.snapshot_isolation_state AS [SnapshotIsolationState],
dtb.is_db_chaining_on AS [DatabaseOwnershipChaining],
dtb.is_auto_update_stats_async_on AS [AutoUpdateStatisticsAsync],
dtb.is_date_correlation_on AS [DateCorrelationOptimization],
dtb.is_broker_enabled AS [BrokerEnabled],
dtb.is_trustworthy_on AS [Trustworthy],
dtb.is_parameterization_forced AS [IsParameterizationForced],
dtb.is_auto_close_on AS [AutoClose],
ISNULL((case dmi.mirroring_redo_queue_type when N'UNLIMITED' then 0 else dmi.mirroring_redo_queue end),0) AS [MirroringRedoQueueMaxSize],
ISNULL(dmi.mirroring_connection_timeout,0) AS [MirroringTimeout]
FROM
master.sys.databases AS dtb
LEFT OUTER JOIN sys.database_mirroring AS dmi ON dmi.database_id = dtb.database_id
WHERE
(CAST(case when dtb.name in ('master','model','msdb','tempdb') then 1 else dtb.is_distributor end AS bit)=@_msparam_0 and CAST(isnull(dtb.source_database_id, 0) AS bit)=@_msparam_0)

          drop table #temp_data_retention_property2
