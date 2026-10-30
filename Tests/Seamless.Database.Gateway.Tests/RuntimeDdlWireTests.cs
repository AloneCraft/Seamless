using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class RuntimeDdlWireTests{
    [Fact]
    public void TransactionRemainsActiveUntilCommitDispose(){
        using var database=new RuntimeSqlGeneratedRoot(Options());var api=database.RuntimeSql;Assert.False(api.HasActiveTransaction);
        using(var tx=api.BeginTransaction()){Assert.True(api.HasActiveTransaction);tx.Commit();Assert.True(api.HasActiveTransaction);}
        Assert.False(api.HasActiveTransaction);
    }
    [Fact]
    public void ParserRejectsUnsupportedBeforeMutation(){
        var create=Assert.IsType<SqlCreateTableCommand>(SqlDdlParser.Parse("CREATE TABLE dbo.t (id int NOT NULL PRIMARY KEY, name nvarchar(20) NULL, n decimal(12,2), data varbinary(max), flag bit)"));
        Assert.Equal(5,create.Columns.Count);Assert.True(create.Columns[0].IsPrimaryKey);Assert.Equal(20,create.Columns[1].Length);Assert.Equal(-1,create.Columns[3].Length);
        var tableKey=Assert.IsType<SqlCreateTableCommand>(SqlDdlParser.Parse("CREATE TABLE dbo.k (id int, CONSTRAINT pk PRIMARY KEY (id))"));Assert.True(tableKey.Columns[0].IsPrimaryKey);Assert.False(tableKey.Columns[0].IsNullable);
        Assert.Throws<SqlQueryException>(()=>SqlDdlParser.Parse("CREATE TABLE dbo.k (id int NULL, CONSTRAINT pk PRIMARY KEY (id))"));
        Assert.IsType<SqlAddColumnCommand>(SqlDdlParser.Parse("ALTER TABLE dbo.t ADD extra int NULL"));Assert.IsType<SqlDropTableCommand>(SqlDdlParser.Parse("DROP TABLE dbo.t"));
        foreach(var sql in new[]{"CREATE TABLE dbo.t(id int IDENTITY(1,1))","CREATE TABLE dbo.t(id int DEFAULT 1)","CREATE TABLE dbo.t(id int CHECK(id>0))","ALTER TABLE dbo.t ADD c int NOT NULL","DROP TABLE dbo.t,dbo.u"}){Assert.Throws<SqlQueryException>(()=>SqlDdlParser.Parse(sql));}
    }
    private static DatabaseContainerOptions Options(){var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root.");var path=Path.Combine(root,"runtime-ddl-wire-"+Guid.NewGuid().ToString("N"));return DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};}
    [Fact,Trait("Category","Integration")]
    public async Task WireStorageSurvivesRestart(){
        var options=Options();int objectId;using(var database=new RuntimeSqlGeneratedRoot(options)){
            var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("fixture",adapter);using var server=new TdsServer(35481,adapter,new SqlExecutionEngine(registry));server.Start();
            var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,35481",InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="CREATE TABLE dbo.persisted(id bigint NOT NULL PRIMARY KEY,flag bit NULL, data varbinary(4) NULL, text nvarchar(10) NULL)";await command.ExecuteNonQueryAsync();
            command.CommandText="INSERT dbo.persisted(id,flag,data,text) VALUES(1,1,0xABCD,N'saved')";await command.ExecuteNonQueryAsync();
            command.CommandText="SELECT object_id FROM sys.tables WHERE name=N'persisted'";objectId=Convert.ToInt32(await command.ExecuteScalarAsync());Assert.Equal(1_000_000_000L+database.RuntimeSql.FindTable("dbo","persisted")!.ObjectId,objectId);
            command.CommandText="SELECT object_id FROM sys.columns WHERE object_id="+objectId;using(var reader=await command.ExecuteReaderAsync()){var count=0;while(await reader.ReadAsync()){Assert.Equal(objectId,reader.GetInt32(0));count++;}Assert.Equal(4,count);}

        }
        using(var reopened=new RuntimeSqlGeneratedRoot(options)){var api=reopened.RuntimeSql;var table=api.FindTable("dbo","persisted");Assert.NotNull(table);var row=Assert.Single(api.ReadRows("dbo","persisted",api.SchemaRevision));Assert.Equal(1L,row.Values[0]);Assert.Equal(true,row.Values[1]);Assert.Equal(new byte[]{0xAB,0xCD},row.Values[2]);Assert.Equal("saved",row.Values[3]);
            var adapter=new InProcessDatabaseAdapter(reopened);var registry=new DatabaseRegistry();registry.Register("fixture",adapter);using var server=new TdsServer(35481,adapter,new SqlExecutionEngine(registry));server.Start();var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,35481",InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT object_id FROM sys.tables WHERE name=N'persisted'";Assert.Equal(objectId,Convert.ToInt32(await command.ExecuteScalarAsync()));}
    }
    [Fact,Trait("Category","Integration")]
    public async Task WireRollbackOnlyAndValidation(){
        var options=Options();using var database=new RuntimeSqlGeneratedRoot(options);var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("fixture",adapter);registry.Register("ro",adapter,true);using var server=new TdsServer(35481,adapter,new SqlExecutionEngine(registry));server.Start();
        var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,35481",InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
        async Task Exec(string sql){command.CommandText=sql;await command.ExecuteNonQueryAsync();}
        await Exec("CREATE TABLE dbo.test_rollback(id int NOT NULL PRIMARY KEY,n int NULL,amount decimal(5,2) NULL,text nvarchar(3) NULL)");
        var metadata=adapter.FindTable("dbo.test_rollback");Assert.NotNull(metadata);Assert.Equal(1_000_000_000L+database.RuntimeSql.FindTable("dbo","test_rollback")!.ObjectId,metadata.StableObjectId);
        await Exec("INSERT dbo.test_rollback(id,n,amount,text) VALUES(1,2,1.20,N'abc')");await Exec("ALTER TABLE dbo.test_rollback ADD more int NULL");await Exec("UPDATE dbo.test_rollback SET n=n+3 WHERE id=1");
        await Exec("BEGIN TRANSACTION");await Assert.ThrowsAsync<SqlException>(()=>Exec("INSERT dbo.test_rollback(id,n) VALUES(3,1),(1,9)"));var failedCommit=await Assert.ThrowsAsync<SqlException>(()=>Exec("COMMIT TRANSACTION"));Assert.Equal(3930,failedCommit.Number);await Exec("ROLLBACK TRANSACTION");
        await Assert.ThrowsAsync<SqlException>(()=>Exec("INSERT dbo.test_rollback(id,text) VALUES(4,N'abcd')"));await Assert.ThrowsAsync<SqlException>(()=>Exec("INSERT dbo.test_rollback(id,amount) VALUES(4,1.234)"));await Assert.ThrowsAsync<SqlException>(()=>Exec("INSERT dbo.test_rollback(id,n) VALUES(4,1.5)"));
        await Exec("INSERT dbo.test_rollback(id,n) VALUES(4,NULL)");await Exec("DELETE dbo.test_rollback WHERE id=4");
        var typedAlter=await Assert.ThrowsAsync<SqlException>(()=>Exec("ALTER TABLE dbo.TypedOrders ADD c int NULL"));Assert.Equal(40515,typedAlter.Number);var typedDrop=await Assert.ThrowsAsync<SqlException>(()=>Exec("DROP TABLE dbo.TypedOrders"));Assert.Equal(40515,typedDrop.Number);await Assert.ThrowsAsync<SqlException>(()=>Exec("CREATE TABLE dbo.invalid_type(id int DEFAULT 1)"));
        await Exec("USE ro");var readOnly=await Assert.ThrowsAsync<SqlException>(()=>Exec("CREATE TABLE dbo.readonly_attempt(id int)"));Assert.Equal(3906,readOnly.Number);await Exec("USE fixture");
        var row=Assert.Single(database.RuntimeSql.ReadRows("dbo","test_rollback",database.RuntimeSql.SchemaRevision));Assert.Equal(new object?[]{1,5,1.20m,"abc",null},row.Values);
        await Exec("DROP TABLE dbo.test_rollback");Assert.Null(database.RuntimeSql.FindTable("dbo","test_rollback"));
    }
    [Fact,Trait("Category","Integration")]
    public async Task CreateAlterDmlRollbackAndReopen(){
        var options=Options();
        using(var database=new RuntimeSqlGeneratedRoot(options)){
            var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("fixture",adapter);registry.Register("ro",adapter,true);using var server=new TdsServer(35481,adapter,new SqlExecutionEngine(registry));server.Start();
            var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,35481",InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
            using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            async Task Exec(string sql){command.CommandText=sql;await command.ExecuteNonQueryAsync();}
            async Task<int> Scalar(string sql){command.CommandText=sql;return Convert.ToInt32(await command.ExecuteScalarAsync());}
            await Exec("CREATE TABLE dbo.dynamic_test(id int NOT NULL PRIMARY KEY, n int NULL)");await Exec("INSERT dbo.dynamic_test(id,n) VALUES(1,2)");Assert.Equal(2,await Scalar("SELECT n FROM dbo.dynamic_test WHERE id=1"));
            await Exec("ALTER TABLE dbo.dynamic_test ADD extra int NULL");await Exec("UPDATE dbo.dynamic_test SET n=n+3 WHERE id=1");Assert.Equal(5,await Scalar("SELECT n FROM dbo.dynamic_test WHERE id=1"));
            await Exec("BEGIN TRANSACTION");await Exec("INSERT dbo.dynamic_test(id,n) VALUES(2,7)");await Exec("ROLLBACK TRANSACTION");Assert.Equal(1,await Scalar("SELECT COUNT(*) FROM dbo.dynamic_test"));
            await Exec("BEGIN TRANSACTION");await Exec("INSERT dbo.dynamic_test(id,n) VALUES(2,7)");await Exec("COMMIT TRANSACTION");Assert.Equal(2,await Scalar("SELECT COUNT(*) FROM dbo.dynamic_test"));
            await Exec("BEGIN TRANSACTION");await Assert.ThrowsAsync<SqlException>(()=>Exec("INSERT dbo.dynamic_test(id,n) VALUES(3,1),(2,9)"));var commit=await Assert.ThrowsAsync<SqlException>(()=>Exec("COMMIT TRANSACTION"));Assert.Equal(3930,commit.Number);await Exec("ROLLBACK TRANSACTION");Assert.Equal(2,await Scalar("SELECT COUNT(*) FROM dbo.dynamic_test"));
            await Exec("INSERT dbo.dynamic_test(id,n) VALUES(4,NULL)");command.CommandText="SELECT n FROM dbo.dynamic_test WHERE id=4";Assert.Equal(DBNull.Value,await command.ExecuteScalarAsync());await Exec("DELETE dbo.dynamic_test WHERE id=4");
            await Exec("USE ro");var readOnly=await Assert.ThrowsAsync<SqlException>(()=>Exec("CREATE TABLE dbo.readonly_attempt(id int)"));Assert.Equal(3906,readOnly.Number);await Exec("USE fixture");
            await Assert.ThrowsAsync<SqlException>(()=>Exec("ALTER TABLE dbo.TypedOrders ADD c int NULL"));await Assert.ThrowsAsync<SqlException>(()=>Exec("CREATE TABLE dbo.bad(id int IDENTITY(1,1))"));
        }
        using(var reopened=new RuntimeSqlGeneratedRoot(options)){var api=reopened.RuntimeSql;Assert.Equal(2,api.ReadRows("dbo","dynamic_test",api.SchemaRevision).Count());var engine=new SqlExecutionEngine(new InProcessDatabaseAdapter(reopened));using var session=new SessionContext(2,reopened.GetType().Name);engine.Execute("DROP TABLE dbo.dynamic_test",session);Assert.Null(api.FindTable("dbo","dynamic_test"));}
    }
}
