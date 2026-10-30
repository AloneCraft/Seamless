using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsInitializationTests{
    private static string ServerInformation(){using var stream=typeof(SqlExecutionEngine).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility.SsmsServerInformation.sql")??throw new InvalidOperationException("Missing captured SQL resource.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private static SqlConnection Connect(TdsServer server)=>new(new SqlConnectionStringBuilder{DataSource=$"127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5}.ConnectionString);
    [Theory,Trait("Category","Integration")]
    [InlineData("")]
    [InlineData("/* DatabaseEngineType */")]
    public async Task CapturedServerInformationWorksOnReadOnlyDatabase(string comment){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,true);using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=comment+ServerInformation();
            await using(var reader=await command.ExecuteReaderAsync()){
                Assert.Equal(15,reader.FieldCount);Assert.True(await reader.ReadAsync());Assert.Equal("127.0.0.1,14333",reader.GetString(0));Assert.True(reader.IsDBNull(2));Assert.True(reader.GetColumnSchema()[2].AllowDBNull);Assert.Equal(16,reader.GetInt32(5));Assert.Equal("Windows",reader.GetString(13));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
            }
            command.CommandText="DROP TABLE #SVer";await command.ExecuteNonQueryAsync();var duplicate=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(3906,duplicate.Number);
            command.CommandText="CREATE TABLE dbo.forbidden(Id int)";var forbidden=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(3906,forbidden.Number);
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task InitialConnectionProfileDoesNotIgnoreAppendedMutation(){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,true);using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            using var stream=typeof(SqlExecutionEngine).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility.SsmsConnectionInitialization.sql")??throw new InvalidOperationException("Missing captured connection resource.");using var source=new StreamReader(stream);
            await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=source.ReadToEnd()+"; DELETE dbo.forbidden";var unknown=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(40515,unknown.Number);
            command.CommandText="SELECT CAST(SERVERPROPERTY('EngineEdition') AS int) AS DatabaseEngineType; SELECT host_platform FROM sys.dm_os_host_info; DELETE dbo.forbidden";var mutation=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(3906,mutation.Number);
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ModifiedServerInformationDoesNotBypassReadOnlyProtection(){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,true);using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();
            foreach(var sql in new[]{ServerInformation()+"; DELETE dbo.forbidden",ServerInformation().Replace("drop table #SVer","drop table dbo.forbidden"),ServerInformation().Replace("xp_msver","other_procedure")}){command.CommandText=sql;var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(3906,error.Number);}
            command.CommandText="SELECT N'#SVer'";Assert.Equal("#SVer",await command.ExecuteScalarAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task UnlimitedTextSizeDoesNotTruncateUnicodeOrBinary(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();var text=new string('界',5000);var bytes=Enumerable.Range(0,9000).Select(i=>(byte)(i%251)).ToArray();
            command.Parameters.Add("@text",global::System.Data.SqlDbType.NVarChar,-1).Value=text;command.Parameters.Add("@bytes",global::System.Data.SqlDbType.VarBinary,-1).Value=bytes;command.CommandText="SET TEXTSIZE 2147483647; SELECT @text AS TextValue,@bytes AS BinaryValue";
            await using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(text,reader.GetString(0));Assert.Equal(bytes,reader.GetFieldValue<byte[]>(1));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());}
            command.Parameters.Clear();command.CommandText="SELECT @@TEXTSIZE AS [TextSize]";Assert.Equal(int.MaxValue,await command.ExecuteScalarAsync());
            foreach(var value in new[]{"0","4096"}){command.CommandText="SET TEXTSIZE "+value;var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(40515,error.Number);}
            foreach(var value in new[]{"@size","2147483648"}){command.CommandText="SET TEXTSIZE "+value;var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(102,error.Number);}
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task CapturedQueryEditorSettingsAndBasicSelect(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            using var stream=typeof(SqlExecutionEngine).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility.SsmsQuerySettings.sql")??throw new InvalidOperationException("Missing captured settings resource.");using var source=new StreamReader(stream);
            await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=source.ReadToEnd()+"; SELECT @@LOCK_TIMEOUT; SELECT @@TEXTSIZE; SELECT CAST(17 AS int) AS Value";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(-1,reader.GetInt32(0));Assert.False(await reader.ReadAsync());Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal(int.MaxValue,reader.GetInt32(0));Assert.False(await reader.ReadAsync());Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal(17,reader.GetInt32(0));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
        }finally{await server.StopAsync();}
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("SET QUERY_GOVERNOR_COST_LIMIT 1")]
    [InlineData("SET DEADLOCK_PRIORITY LOW")]
    [InlineData("SET DEADLOCK_PRIORITY HIGH")]
    public async Task NonDefaultGovernorAndPriorityAreRejected(string sql){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{await using var connection=Connect(server);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=sql;var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(40515,error.Number);command.CommandText="SELECT CAST(19 AS int) AS Value";Assert.Equal(19,await command.ExecuteScalarAsync());}finally{await server.StopAsync();}
    }
    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata> GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string t,IReadOnlyList<string>?c,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException("No user table read expected.");
        public int ExecuteInsert(string t,IReadOnlyDictionary<string,object?>v,SessionContext s)=>throw new InvalidOperationException("No mutation expected.");
        public int ExecuteUpdate(string t,IReadOnlyDictionary<string,object?>v,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException("No mutation expected.");
        public int ExecuteDelete(string t,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException("No mutation expected.");
        public void BeginTransaction(SessionContext s)=>throw new InvalidOperationException("No transaction expected.");public void CommitTransaction(SessionContext s)=>throw new InvalidOperationException("No transaction expected.");public void RollbackTransaction(SessionContext s)=>throw new InvalidOperationException("No transaction expected.");
    }
}
