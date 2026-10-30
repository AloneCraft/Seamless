SELECT
dtb.name AS [Name],
dtb.database_id AS [ID],
CAST(has_dbaccess(dtb.name) AS bit) AS [IsAccessible],
CAST(case when dtb.name in ('master','model','msdb','tempdb') then 1 else dtb.is_distributor end AS bit) AS [IsSystemObject],
CAST(isnull(dtb.source_database_id, 0) AS bit) AS [IsDatabaseSnapshot],

        case
        when dtb.collation_name is null then 0x200
        else 0
        end |
        case
        when 1 = dtb.is_in_standby then 0x40
        else 0
        end |
        case dtb.state
        when 1 then 0x2
        when 2 then 0x8
        when 3 then 0x4
        when 4 then 0x10
        when 5 then 0x100
        when 6 then 0x20
        else 1
        end
       AS [Status],
dtb.collation_name AS [Collation],
dtb.is_read_only AS [ReadOnly],
dtb.compatibility_level AS [CompatibilityLevel],
dtb.recovery_model AS [RecoveryModel],
dtb.name AS [DatabaseName2]
FROM
master.sys.databases AS dtb
WHERE
(CAST(case when dtb.name in ('master','model','msdb','tempdb') then 1 else dtb.is_distributor end AS bit)=@_msparam_0 and CAST(isnull(dtb.source_database_id, 0) AS bit)=@_msparam_0)
