using System.Data;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Rdbms;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayIdentityFunctions{
    [Fact,Trait("Category","Integration")]
    public async Task NativeExplicitIdentityValuesAdvanceTheDurableWatermarkAndCannotBeUpdated(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"native-identity-"+Guid.NewGuid().ToString("N"));var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(945,"a");foreach(var row in engine.Execute("CREATE TABLE dbo.Ids(Id int IDENTITY(10,2) NOT NULL PRIMARY KEY,N int NOT NULL)",session).Rows){}
            var database=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;var types=database.GetType().GetProperty("dbo")!.GetValue(database)!.GetType().GetProperty("Ids")!.PropertyType.GetGenericArguments();
            var code="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+database.GetType().FullName+")context.Database;var value=MemoryPackSerializer.Deserialize<int>(args.Span);if(id==\"insert\"){db.dbo.Ids.Add(new global::"+types[1].FullName!.Replace('+','.')+"(value,1));}else{db.dbo.Ids.Replace(new global::"+types[0].FullName!.Replace('+','.')+"(value),new global::"+types[1].FullName!.Replace('+','.')+"(value+1,1));}yield return MemoryPackSerializer.Serialize(value);}}";
            await host.Transactions.InstallNativeQueryAsync(new("identity","Query",[new("Query.cs",code)],[new("insert","a","","System.Int32","System.Int32",false),new("move","a","","System.Int32","System.Int32",false)],["a"]));
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var fingerprint=host.Databases.Resolve("a").SchemaFingerprint!;
            await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){await foreach(var value in transaction.QueryAsync<int,int>("identity","insert",fingerprint,40,timeout.Token)){}await transaction.RollbackAsync(timeout.Token);}
            Assert.Equal(40m,Assert.Single(engine.Execute("SELECT IDENT_CURRENT(N'dbo.Ids')",session).Rows)[0]);
            foreach(var row in engine.Execute("INSERT dbo.Ids(N) VALUES(1)",session).Rows){}Assert.Equal(42,Assert.Single(engine.Execute("SELECT Id FROM dbo.Ids",session).Rows)[0]);
            await foreach(var value in client.QueryAsync<int,int>("identity","insert","a",fingerprint,100,timeout.Token)){}
            await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var value in client.QueryAsync<int,int>("identity","move","a",fingerprint,100,timeout.Token)){}});
            Assert.Equal(new object?[]{42,100},engine.Execute("SELECT Id FROM dbo.Ids ORDER BY Id",session).Rows.Select(static row=>row[0]));
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(946,"a");foreach(var row in engine.Execute("INSERT dbo.Ids(N) VALUES(1)",session).Rows){}Assert.Equal(new object?[]{42,100,102},engine.Execute("SELECT Id FROM dbo.Ids ORDER BY Id",session).Rows.Select(static row=>row[0]));}
    }
    [Fact,Trait("Category","Integration")]
    public async Task IdentityStateTracksExplicitValuesRollbackSessionsAndRestart(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var root=Path.Combine(data,"identity-functions-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var server=new TdsServer(0,host.Databases.Resolve("a").Adapter,engine);server.Start();
            var setting=new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="a",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10}.ConnectionString;
            using var connection=new SqlConnection(setting);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandTimeout=120;
            const string state="SELECT @@IDENTITY I,SCOPE_IDENTITY() S,IDENT_CURRENT(N'dbo.Ids') C";
            command.CommandText="CREATE TABLE dbo.Ids(Id int IDENTITY(10,2) NOT NULL PRIMARY KEY,N int CHECK(N>0)); "+state+"; INSERT dbo.Ids(N) VALUES(1); "+state+"; BEGIN TRANSACTION; INSERT dbo.Ids(N) VALUES(2); ROLLBACK TRANSACTION; "+state+"; SET IDENTITY_INSERT dbo.Ids ON; INSERT dbo.Ids(Id,N) VALUES(99,3); "+state+"; INSERT dbo.Ids(Id,N) VALUES(20,4); "+state+"; SET IDENTITY_INSERT dbo.Ids OFF";
            using(var reader=await command.ExecuteReaderAsync()){
                foreach(var expected in new decimal?[][]{[null,null,10],[10,10,10],[12,12,12],[99,99,99],[20,20,99]}){
                    Assert.True(await reader.ReadAsync());for(var index=0;index<3;index++){Assert.Equal(SqlDbType.Decimal.ToString().ToLowerInvariant(),reader.GetDataTypeName(index));Assert.Equal((short)38,reader.GetSchemaTable()!.Rows[index]["NumericPrecision"]);Assert.Equal(expected[index],reader.IsDBNull(index)?null:reader.GetDecimal(index));}Assert.False(await reader.ReadAsync());if(expected[0]!=20){Assert.True(await reader.NextResultAsync());}
                }
            }
            command.CommandText=state;using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(20m,reader.GetDecimal(0));Assert.Equal(20m,reader.GetDecimal(1));Assert.Equal(99m,reader.GetDecimal(2));}
            using var second=new SqlConnection(setting);await second.OpenAsync();using var other=second.CreateCommand();other.CommandText=state;using(var reader=await other.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.True(reader.IsDBNull(0));Assert.True(reader.IsDBNull(1));Assert.Equal(99m,reader.GetDecimal(2));}
            command.CommandText="INSERT dbo.Ids(N) VALUES(0)";Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync())).Number);
            command.CommandText=state;using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(101m,reader.GetDecimal(0));Assert.Equal(101m,reader.GetDecimal(2));}
            command.CommandText="INSERT dbo.Ids(N) VALUES(@n); "+state;command.Parameters.Add("@n",SqlDbType.Int).Value=5;
            using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(103m,reader.GetDecimal(0));Assert.Equal(103m,reader.GetDecimal(1));Assert.Equal(103m,reader.GetDecimal(2));}
            command.Prepare();using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(105m,reader.GetDecimal(1));}
            command.Parameters.Clear();command.CommandText=state;using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(105m,reader.GetDecimal(0));Assert.Equal(101m,reader.GetDecimal(1));Assert.Equal(105m,reader.GetDecimal(2));}
            command.CommandText="SELECT seed_value,increment_value,last_value FROM sys.identity_columns WHERE object_id=OBJECT_ID(N'dbo.Ids')";using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(10,Assert.IsType<int>(reader.GetValue(0)));Assert.Equal(2,Assert.IsType<int>(reader.GetValue(1)));Assert.Equal(105,Assert.IsType<int>(reader.GetValue(2)));}
            command.CommandText="SELECT IDENT_CURRENT(@table),IDENT_CURRENT(NULL)";command.Parameters.Add("@table",SqlDbType.NVarChar,128).Value="dbo.Ids";using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(105m,reader.GetDecimal(0));Assert.True(reader.IsDBNull(1));}command.Parameters.Clear();
            command.CommandText="SELECT IDENT_CURRENT(N'dbo.Missing'),IDENT_CURRENT(N'bad..name.invalid')";using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.True(reader.IsDBNull(0));Assert.True(reader.IsDBNull(1));}
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(940,"a");
            var row=Assert.Single(engine.Execute("SELECT @@IDENTITY,SCOPE_IDENTITY(),IDENT_CURRENT(N'dbo.Ids')",session).Rows);Assert.Equal(new object?[]{null,null,105m},row);
        }
    }
}
