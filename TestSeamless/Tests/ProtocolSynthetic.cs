using System.Data;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;

namespace TestSeamless.Tests;

public readonly record struct ProtocolSyntheticKey(long SyntheticId);
public readonly record struct ProtocolSyntheticRow(int Value,long SyntheticId);
public sealed class ProtocolSyntheticDatabase(DatabaseContainerOptions options):DatabaseContainer(options){
    public Table<ProtocolSyntheticKey,ProtocolSyntheticRow> Rows{[return:Seamless.Linq.UnorderedEnumerable]get=>this.Table<ProtocolSyntheticKey,ProtocolSyntheticRow>(static row=>new(row.SyntheticId));}
}

[Collection("Protocol serial")]public sealed class ProtocolSynthetic{
    [Fact]public void ExplicitNullCannotBecomeZero(){
        using var db=new ProtocolSyntheticDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var binding=new[]{new GeneratedTableBinding("Rows","dbo","Rows",[new("Value","Value",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("SyntheticId","SyntheticId",new(DatabaseTypeKind.Int64,false),true)])};
        var adapter=new InProcessDatabaseAdapter(db,binding);var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false}.ConnectionString);connection.Open();
        using var command=connection.CreateCommand();command.CommandText="INSERT INTO dbo.Rows (Value) VALUES (NULL)";Assert.Throws<SqlException>(()=>command.ExecuteNonQuery());Assert.Empty(db.Rows);
    }
}
