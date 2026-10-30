-- 比較元のローカルSQL Serverで実行する。Seamless側では実行しない。
-- 実行後、同じSSMSから新しいクエリを開いて比較SQLを実行し、「実行した」と連絡する。
-- SQL_BATCHとRPCだけを採取し、LOGIN7の資格情報は採取しない。
-- セッション/リングバッファーは各1MiB、イベント数は1000件まで。
SET NOCOUNT ON;

DECLARE @ClientPid int = (SELECT host_process_id FROM sys.dm_exec_sessions WHERE session_id = @@SPID);
IF @ClientPid IS NULL OR @ClientPid <= 0
    THROW 51000, 'The client process ID is unavailable; no trace was created.', 1;

DECLARE @Prefix sysname = N'Seamless_SsmsManual_' + CONVERT(nvarchar(10), @ClientPid) + N'_';
IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE LEFT(name, LEN(@Prefix)) = @Prefix)
    THROW 51001, 'A trace for this client already exists. Collect it before starting another.', 1;

DECLARE @SessionName sysname = @Prefix + LEFT(REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), 8);
DECLARE @Created bit = 0;
DECLARE @Sql nvarchar(max) = N'CREATE EVENT SESSION ' + QUOTENAME(@SessionName) + N' ON SERVER
ADD EVENT sqlserver.sql_batch_starting(
 ACTION(sqlserver.client_app_name,sqlserver.client_pid,sqlserver.database_name)
 WHERE ([sqlserver].[client_pid]=(' + CONVERT(nvarchar(10), @ClientPid) + N'))),
ADD EVENT sqlserver.rpc_starting(
 ACTION(sqlserver.client_app_name,sqlserver.client_pid,sqlserver.database_name)
 WHERE ([sqlserver].[client_pid]=(' + CONVERT(nvarchar(10), @ClientPid) + N')))
ADD TARGET package0.ring_buffer(SET max_events_limit=(1000),max_memory=(1024))
WITH(MAX_MEMORY=1024KB,MAX_DISPATCH_LATENCY=1 SECONDS,STARTUP_STATE=OFF);';

BEGIN TRY
    EXEC sys.sp_executesql @Sql;
    SET @Created = 1;
    SET @Sql = N'ALTER EVENT SESSION ' + QUOTENAME(@SessionName) + N' ON SERVER STATE=START;';
    EXEC sys.sp_executesql @Sql;
    SELECT @SessionName AS TraceSession, @ClientPid AS ClientProcessId,
           DB_NAME() AS DatabaseName, SYSUTCDATETIME() AS StartedUtc;
END TRY
BEGIN CATCH
    IF @Created = 1
    BEGIN
        SET @Sql = N'DROP EVENT SESSION ' + QUOTENAME(@SessionName) + N' ON SERVER;';
        EXEC sys.sp_executesql @Sql;
    END;
    THROW;
END CATCH;
