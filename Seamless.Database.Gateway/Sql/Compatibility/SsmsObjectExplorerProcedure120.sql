use [AdventureWorks2016]
SELECT
sp.name AS [Name],
'Server[@Name=' + quotename(CAST(
        serverproperty(N'Servername')
       AS sysname),'''') + ']' + '/Database[@Name=' + quotename(db_name(),'''') + ']' + '/StoredProcedure[@Name=' + quotename(sp.name,'''') + ' and @Schema=' + quotename(SCHEMA_NAME(sp.schema_id),'''') + ']' AS [Urn],
SCHEMA_NAME(sp.schema_id) AS [Schema],
CAST(CASE WHEN ISNULL(smsp.definition, ssmsp.definition) IS NULL THEN 1 ELSE 0 END AS bit) AS [IsEncrypted],
CASE WHEN sp.type = N'P' THEN 1 WHEN sp.type = N'PC' THEN 2 ELSE 1 END AS [ImplementationType],
ISNULL(sm.uses_native_compilation,0) AS [IsNativelyCompiled]
FROM
sys.all_objects AS sp
LEFT OUTER JOIN sys.sql_modules AS smsp ON smsp.object_id = sp.object_id
LEFT OUTER JOIN sys.system_sql_modules AS ssmsp ON ssmsp.object_id = sp.object_id
LEFT OUTER JOIN sys.all_sql_modules AS sm ON sm.object_id = sp.object_id
WHERE
(sp.type = @ui_P OR sp.type = @ui_RF OR sp.type=@ui_PC)and(CAST(
 case 
    when sp.is_ms_shipped = 1 then 1
    when exists (
        select 
            1
        from 
            sys.extended_properties 
        where 
            major_id = sp.object_id and 
            minor_id = 0 and 
            class = 1 and 
            name = N'microsoft_database_tools_support') 
        then 1
    else 0
end          
             AS bit)=@ui_0 and ISNULL(sm.uses_native_compilation,0)=@ui_0)