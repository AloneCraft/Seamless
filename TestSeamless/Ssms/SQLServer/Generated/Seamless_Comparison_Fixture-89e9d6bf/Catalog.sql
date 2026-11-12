-- Generated from the rebuild manifest. Do not edit this generated file.
USE [Seamless_Comparison_Fixture];
GO
SELECT s.name AS schema_name,o.name,o.type,o.type_desc FROM sys.objects AS o JOIN sys.schemas AS s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0 ORDER BY s.name,o.name;
GO
SELECT s.name AS schema_name,o.name,o.type,m.definition FROM sys.objects AS o JOIN sys.schemas AS s ON s.schema_id=o.schema_id JOIN sys.sql_modules AS m ON m.object_id=o.object_id WHERE o.is_ms_shipped=0 ORDER BY s.name,o.name;
GO
