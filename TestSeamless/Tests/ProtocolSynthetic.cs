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
    [Fact]public void PrimaryKeyUpdateMovesRowAndRejectsCollision(){
        using var db=new ProtocolSyntheticDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        Assert.True(db.Rows.Add(new(7,1)));Assert.True(db.Rows.Add(new(8,2)));
        var binding=new[]{new GeneratedTableBinding("Rows","dbo","Rows",[new("Value","Value",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("SyntheticId","SyntheticId",new(DatabaseTypeKind.Int64,false,SqlTypeName:"bigint"))])};
        var adapter=new InProcessDatabaseAdapter(db,binding);var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false}.ConnectionString);connection.Open();
        using var command=connection.CreateCommand();command.CommandText="UPDATE dbo.Rows SET SyntheticId=3 WHERE SyntheticId=1";command.ExecuteNonQuery();
        Assert.Equal(2,db.Rows.Count);Assert.False(db.Rows.ContainsKey(new(1)));Assert.True(db.Rows.ContainsKey(new(3)));
        command.CommandText="UPDATE dbo.Rows SET SyntheticId=2 WHERE SyntheticId=3";Assert.Throws<SqlException>(()=>command.ExecuteNonQuery());
        Assert.Equal(2,db.Rows.Count);Assert.True(db.Rows.ContainsKey(new(2)));Assert.True(db.Rows.ContainsKey(new(3)));Assert.Equal(8,db.Rows[new(2)].Value);Assert.Equal(7,db.Rows[new(3)].Value);
    }
    [Fact]public void ExplicitNullCannotBecomeZero(){
        using var db=new ProtocolSyntheticDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
        var binding=new[]{new GeneratedTableBinding("Rows","dbo","Rows",[new("Value","Value",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("SyntheticId","SyntheticId",new(DatabaseTypeKind.Int64,false),true)])};
        var adapter=new InProcessDatabaseAdapter(db,binding);var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false}.ConnectionString);connection.Open();
        using var command=connection.CreateCommand();command.CommandText="INSERT INTO dbo.Rows (Value) VALUES (NULL)";Assert.Throws<SqlException>(()=>command.ExecuteNonQuery());Assert.Empty(db.Rows);
    }
    [Fact]public void DuplicateRowsSurviveRollbackAndRestart(){
        var directory=Path.Combine(Path.GetTempPath(),"SeamlessProtocolSynthetic-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(directory) with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        var binding=new[]{new GeneratedTableBinding("Rows","dbo","Rows",[new("Value","Value",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("SyntheticId","SyntheticId",new(DatabaseTypeKind.Int64,false),true)])};
        void Check(bool first){
            using var db=new ProtocolSyntheticDatabase(options);if(first){Assert.True(db.Rows.Add(new(7,1)));}
            var adapter=new InProcessDatabaseAdapter(db,binding);var metadata=Assert.Single(adapter.GetTables());Assert.Single(metadata.Columns);Assert.False(metadata.Columns[0].IsPrimaryKey);
            var registry=new DatabaseRegistry();registry.Register("first",adapter);using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
            using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false}.ConnectionString);connection.Open();
            void Run(string sql){using var command=connection.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();}
            int Count(){using var command=connection.CreateCommand();command.CommandText="SELECT COUNT(*) FROM dbo.Rows WHERE Value=7";return (int)command.ExecuteScalar()!;}
            if(first){Run("INSERT INTO dbo.Rows (Value) VALUES (7)");Run("INSERT INTO dbo.Rows (Value) VALUES (7)");Assert.Equal(3,Count());Run("BEGIN TRANSACTION");Run("INSERT INTO dbo.Rows (Value) VALUES (7)");Run("ROLLBACK TRANSACTION");Assert.Equal(3,Count());}
            else{Assert.Equal(3,Count());Run("INSERT INTO dbo.Rows (Value) VALUES (7)");Assert.Equal(4,Count());}
        }
        Check(true);Check(false);
    }
}
