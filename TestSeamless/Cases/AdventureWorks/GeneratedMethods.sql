-- SeamlessDB の生成 C# メソッドと元 SQL オブジェクトの対応を表示する。
-- is_stub = 1 は生成 C# が未実装であることを示す。
USE [AdventureWorks2016];
GO

SELECT source_database_name, source_schema_name, source_name, routine_kind,
       declaring_type_name, method_name, source_file, is_stub, csharp_source
FROM seamless.generated_methods
ORDER BY source_schema_name, source_name;
GO
