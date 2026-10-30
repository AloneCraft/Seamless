use [AdventureWorks2016]
SELECT
udf.name AS [Name],
'Server[@Name=' + quotename(CAST(
        serverproperty(N'Servername')
       AS sysname),'''') + ']' + '/Database[@Name=' + quotename(db_name(),'''') + ']' + '/UserDefinedFunction[@Name=' + quotename(udf.name,'''') + ' and @Schema=' + quotename(SCHEMA_NAME(udf.schema_id),'''') + ']' AS [Urn],
SCHEMA_NAME(udf.schema_id) AS [Schema],
(case when 'FN' = udf.type then 1 when 'FS' = udf.type then 1 when 'IF' = udf.type then 3 when 'TF' = udf.type then 2 when 'FT' = udf.type then 2 else 0 end) AS [FunctionType]
FROM
sys.all_objects AS udf
WHERE
(udf.type in ('TF', 'FN', 'IF', 'FS', 'FT'))and(CAST(
 case 
    when udf.is_ms_shipped = 1 then 1
    when exists (
        select 
            1
        from 
            sys.extended_properties 
        where 
            major_id = udf.object_id and 
            minor_id = 0 and 
            class = 1 and 
            name = N'microsoft_database_tools_support') 
        then 1
    else 0
end          
             AS bit)=@ui_0)