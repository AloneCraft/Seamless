use [AdventureWorks2016]
SELECT
'Server[@Name=' + quotename(CAST(
        serverproperty(N'Servername')
       AS sysname),'''') + ']' + '/Database[@Name=' + quotename(db_name(),'''') + ']' + '/View[@Name=' + quotename(v.name,'''') + ' and @Schema=' + quotename(SCHEMA_NAME(v.schema_id),'''') + ']' AS [Urn],
v.name AS [Name],
SCHEMA_NAME(v.schema_id) AS [Schema],
CAST(CASE WHEN ISNULL(smv.definition, ssmv.definition) IS NULL THEN 1 ELSE 0 END AS bit) AS [IsEncrypted],
CAST(ISNULL(OBJECTPROPERTYEX(v.object_id, N'IsSchemaBound'),0) AS bit) AS [IsSchemaBound],
CAST(CASE idx.index_id WHEN 1 THEN 1 ELSE 0 END AS bit) AS [HasClusteredIndex]
FROM
sys.all_views AS v
LEFT OUTER JOIN sys.sql_modules AS smv ON smv.object_id = v.object_id
LEFT OUTER JOIN sys.system_sql_modules AS ssmv ON ssmv.object_id = v.object_id
LEFT OUTER JOIN sys.indexes AS idx ON idx.object_id = v.object_id and (idx.index_id < @ui_2)
WHERE
(v.type = @ui_V)and(CAST(
 case
    when v.is_ms_shipped = 1 then 1
    when exists (
        select
            1
        from
            sys.extended_properties
        where
            major_id = v.object_id and
            minor_id = 0 and
            class = 1 and
            name = N'microsoft_database_tools_support')
        then 1
    else 0
end
             AS bit)=@ui_0 and CAST(ISNULL(v.ledger_view_type, 0) AS tinyint)=@ui_0)
