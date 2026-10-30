using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

public static class SystemCatalogEmulator{
    public static bool TryEmulate(string sql,IDatabaseAdapter adapter,out QueryResult result){
        var normalized=NormalizeSql(sql);

        if(normalized.StartsWith("SET ",StringComparison.OrdinalIgnoreCase)){
            result=QueryResult.NonQuery(0);
            return true;
        }else{
        }

        if(normalized.Contains("@@VERSION",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)};
            var rows=new[]{new object?[]{"Microsoft SQL Server 2022 (RTM) - 16.0.1000.6 (X64) - SeamlessDB Server 1.0"}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("SERVERPROPERTY",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(string),"nvarchar",false,false,0)};
            var rows=new[]{new object?[]{"Enterprise Edition (64-bit)"}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("@@MAX_PRECISION",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(byte),"tinyint",false,false,0)};
            var rows=new[]{new object?[]{(byte)38}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("@@OPTIONS",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{new ColumnMetadata("",typeof(int),"int",false,false,0)};
            var rows=new[]{new object?[]{5496}};
            result=new QueryResult(cols,rows,1);
            return true;
        }else{
        }

        if(normalized.Contains("sys.databases",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("database_id",typeof(int),"int",false,false,1),
                new ColumnMetadata("collation_name",typeof(string),"nvarchar",true,false,2)
            };
            var rows=new List<object?[]>{
                new object?[]{"master",1,"Japanese_CI_AS"},
                new object?[]{"tempdb",2,"Japanese_CI_AS"},
                new object?[]{adapter.DatabaseName,5,"Japanese_CI_AS"}
            };
            result=new QueryResult(cols,rows,rows.Count);
            return true;
        }else{
        }

        if(normalized.Contains("sys.schemas",StringComparison.OrdinalIgnoreCase)){
            var cols=new[]{
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("schema_id",typeof(int),"int",false,false,1)
            };
            var rows=new[]{
                new object?[]{"dbo",1},
                new object?[]{"sys",4}
            };
            result=new QueryResult(cols,rows,rows.Length);
            return true;
        }else{
        }

        if(normalized.Contains("sys.tables",StringComparison.OrdinalIgnoreCase)||normalized.Contains("INFORMATION_SCHEMA.TABLES",StringComparison.OrdinalIgnoreCase)){
            var tables=adapter.GetTables();
            var cols=new[]{
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,0),
                new ColumnMetadata("object_id",typeof(int),"int",false,false,1),
                new ColumnMetadata("schema_id",typeof(int),"int",false,false,2),
                new ColumnMetadata("type",typeof(string),"char",false,false,3),
                new ColumnMetadata("type_desc",typeof(string),"nvarchar",false,false,4)
            };
            var rows=new List<object?[]>();
            var id=100;
            foreach(var t in tables){
                rows.Add(new object?[]{t.TableName,id++,1,"U ","USER_TABLE"});
            }
            result=new QueryResult(cols,rows,rows.Count);
            return true;
        }else{
        }

        if(normalized.Contains("sys.columns",StringComparison.OrdinalIgnoreCase)||normalized.Contains("INFORMATION_SCHEMA.COLUMNS",StringComparison.OrdinalIgnoreCase)){
            var tables=adapter.GetTables();
            var cols=new[]{
                new ColumnMetadata("object_id",typeof(int),"int",false,false,0),
                new ColumnMetadata("name",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("column_id",typeof(int),"int",false,false,2),
                new ColumnMetadata("system_type_id",typeof(byte),"tinyint",false,false,3),
                new ColumnMetadata("max_length",typeof(short),"smallint",false,false,4),
                new ColumnMetadata("precision",typeof(byte),"tinyint",false,false,5),
                new ColumnMetadata("scale",typeof(byte),"tinyint",false,false,6),
                new ColumnMetadata("is_nullable",typeof(bool),"bit",false,false,7)
            };
            var rows=new List<object?[]>();
            var tableId=100;
            foreach(var t in tables){
                foreach(var c in t.Columns){
                    rows.Add(new object?[]{tableId,c.Name,c.Ordinal+1,(byte)56,(short)8,(byte)0,(byte)0,c.IsNullable});
                }
                tableId++;
            }
            result=new QueryResult(cols,rows,rows.Count);
            return true;
        }else{
        }

        result=QueryResult.Empty;
        return false;
    }

    private static string NormalizeSql(string sql){
        if(string.IsNullOrWhiteSpace(sql)){
            return string.Empty;
        }else{
            return sql.Trim();
        }
    }
}
