using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;
using Seamless.Database.Query;
using Seamless.Execution;

namespace Seamless.Database.Gateway.Sql;

/// <summary>移行と Gateway の文確定で同じ SQL 制約を検証する。UNKNOWN は CHECK 違反にしない。</summary>
internal static class GatewayConstraintValidator{
    internal static void Validate(GatewaySchemaCatalog schema,string database,DatabaseRegistry registry,ExecutionOptions options,Func<string,string,bool>? nativePrimaryKey=null){
        var queries=new SqlQueryEngine(registry,0);
        bool Exists(string sql)=>queries.Prepare(sql,database).Execute(options:options).Rows.Any();
        foreach(var owner in schema.Definition.Schemas){foreach(var table in owner.Tables){
            var name=Quote(owner.Name)+"."+Quote(table.Name);
            void Unique(string constraint,IReadOnlyList<KeyColumnCatalog> columns){var keys=string.Join(",",columns.Select(static column=>Quote(column.Name)));if(Exists("SELECT TOP (1) 1 FROM "+name+" GROUP BY "+keys+" HAVING COUNT(*)>1")){throw new SqlQueryException("Violation of constraint '"+constraint+"': duplicate key.",2627);}}
            // 整数の非null主キーは型付き保存先の点操作とOCCが一意性を守る。全表走査を加えて別行writerを競合させない。
            if(table.PrimaryKey is{} primary&&nativePrimaryKey?.Invoke(owner.Name,table.Name)!=true){Unique(primary.Name,primary.Columns);}foreach(var unique in table.UniqueKeys){Unique(unique.Name,unique.Columns);}
            foreach(var check in table.CheckConstraints){if(!check.IsDisabled&&Exists("SELECT TOP (1) 1 FROM "+name+" WHERE NOT ("+check.Definition+")")){throw new SqlQueryException("The statement conflicted with CHECK constraint '"+check.Name+"'.",547);}}
            foreach(var foreign in table.ForeignKeys){
                var parent=schema.Definition.Schemas.SingleOrDefault(item=>item.Name.Equals(foreign.ReferencedSchema,StringComparison.OrdinalIgnoreCase))?.Tables.SingleOrDefault(item=>item.Name.Equals(foreign.ReferencedTable,StringComparison.OrdinalIgnoreCase))??throw new SqlQueryException("Referenced table does not exist for '"+foreign.Name+"'.",1767);
                if(foreign.Columns.Count==0||foreign.Columns.Any(pair=>!table.Columns.Any(column=>column.Name.Equals(pair.Column,StringComparison.OrdinalIgnoreCase))||!parent.Columns.Any(column=>column.Name.Equals(pair.ReferencedColumn,StringComparison.OrdinalIgnoreCase)))){throw new SqlQueryException("Foreign key column does not exist.",1769);}
                var parentColumns=foreign.Columns.Select(static pair=>pair.ReferencedColumn).ToArray();var keys=parent.UniqueKeys.Select(static unique=>unique.Columns).ToList();if(parent.PrimaryKey is{} parentKey){keys.Add(parentKey.Columns);}if(!keys.Any(key=>key.Count==parentColumns.Length&&key.Select(static column=>column.Name).SequenceEqual(parentColumns,StringComparer.OrdinalIgnoreCase))){throw new SqlQueryException("The referenced columns do not form a primary or unique key.",1776);}
                var required=string.Join(" AND ",foreign.Columns.Select(static pair=>"c."+Quote(pair.Column)+" IS NOT NULL"));var matching=string.Join(" AND ",foreign.Columns.Select(static pair=>"p."+Quote(pair.ReferencedColumn)+"=c."+Quote(pair.Column)));
                if(Exists("SELECT TOP (1) 1 FROM "+name+" AS c WHERE "+required+" AND NOT EXISTS(SELECT 1 FROM "+Quote(foreign.ReferencedSchema)+"."+Quote(foreign.ReferencedTable)+" AS p WHERE "+matching+")")){throw new SqlQueryException("The statement conflicted with FOREIGN KEY constraint '"+foreign.Name+"'.",547);}
            }
        }}
    }
    private static string Quote(string name)=>"["+name.Replace("]","]]",StringComparison.Ordinal)+"]";
}
