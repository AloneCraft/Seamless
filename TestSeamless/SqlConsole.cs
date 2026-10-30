using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TestSeamless;

/// <summary>SSMSと同じSQLファイルをパッケージ利用EXEから実行する。</summary>
public static class SqlConsole{
    public static async Task RunAsync(string route,string database,string file,CancellationToken cancellationToken=default){
        if(route is not ("source" or "seamless")){throw new ArgumentException("SQL route must be source or seamless.");}
        var sql=await File.ReadAllTextAsync(Path.GetFullPath(file),cancellationToken);var parser=new TSql160Parser(true);var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count>0){throw new InvalidDataException($"SQL parse error at {errors[0].Line}: {errors[0].Message}");}if(script is null){throw new InvalidDataException("SQL script is empty.");}
        var connectionString=Program.TargetConnection(database);if(route=="source"){
            var fixture=database.Equals("Seamless_Comparison_Fixture",StringComparison.Ordinal);var guard=new SourceReadOnlyGuard(database,fixture);script.Accept(guard);
            connectionString=new SqlConnectionStringBuilder(Program.SourceConnection()){InitialCatalog=database}.ConnectionString;
        }
        using var connection=new SqlConnection(connectionString);await connection.OpenAsync(cancellationToken);var batchNumber=0;
        foreach(var batch in script.Batches){if(batch.Statements.Count==0){continue;}batchNumber++;using var command=connection.CreateCommand();command.CommandText=sql.Substring(batch.StartOffset,batch.FragmentLength);command.CommandTimeout=0;using var reader=await command.ExecuteReaderAsync(cancellationToken);var resultNumber=0;
            do{resultNumber++;var columns=Enumerable.Range(0,reader.FieldCount).Select(i=>new{Name=reader.GetName(i),Type=reader.GetDataTypeName(i)}).ToArray();Console.WriteLine(JsonSerializer.Serialize(new{Batch=batchNumber,Result=resultNumber,Columns=columns}));long rows=0;
                while(await reader.ReadAsync(cancellationToken)){var values=new object?[reader.FieldCount];for(var i=0;i<values.Length;i++){if(!reader.IsDBNull(i)){values[i]=reader.GetValue(i);}}Console.WriteLine(JsonSerializer.Serialize(new{Row=values}));rows++;}
                Console.WriteLine(JsonSerializer.Serialize(new{Rows=rows,RowsAffected=reader.RecordsAffected}));
            }while(await reader.NextResultAsync(cancellationToken));
        }
    }
    public static void ValidateSourceSql(string database,string sql){
        var parser=new TSql180Parser(true);var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;
        if(errors.Count>0){throw new InvalidDataException($"SQL parse error at {errors[0].Line}: {errors[0].Message}");}if(script is null){throw new InvalidDataException("SQL script is empty.");}
        script.Accept(new SourceReadOnlyGuard(database,database.Equals("Seamless_Comparison_Fixture",StringComparison.Ordinal)));
    }
    private sealed class SourceReadOnlyGuard(string database,bool fixture):TSqlFragmentVisitor{
        public override void Visit(TSqlStatement node){
            if(node is UseStatement use){if(!use.DatabaseName.Value.Equals(database,StringComparison.Ordinal)){throw new InvalidOperationException("The source SQL file must stay in its selected database.");}return;}
            if(node is SelectStatement select){if(select.GetType().GetProperty("Into")?.GetValue(select)is not null){throw new InvalidOperationException("SELECT INTO is not allowed in the source comparison database.");}return;}
            if(node is SetOnOffStatement or PredicateSetStatement or SetTransactionIsolationLevelStatement){return;}
            if(fixture&&node is InsertStatement or UpdateStatement or DeleteStatement or BeginTransactionStatement or CommitTransactionStatement or RollbackTransactionStatement){return;}
            throw new InvalidOperationException($"Source statement '{node.GetType().Name}' is not allowed. Use the dedicated fixture for writes.");
        }
        public override void ExplicitVisit(SchemaObjectName node){
            if(node.ServerIdentifier is not null||node.DatabaseIdentifier is not null&&!node.DatabaseIdentifier.Value.Equals(database,StringComparison.Ordinal)){throw new InvalidOperationException("Source SQL must stay in its selected local database.");}
            base.ExplicitVisit(node);
        }
        public override void ExplicitVisit(OpenQueryTableReference node)=>throw new InvalidOperationException("Remote SQL pass-through is not allowed in the source comparison console.");
        public override void ExplicitVisit(OpenRowsetTableReference node)=>throw new InvalidOperationException("OPENROWSET is not allowed in the source comparison console.");
    }
}
