using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsInitializationTests{
    [Theory,Trait("Category","Integration")]
    [InlineData("fixture")]
    [InlineData("master")]
    public async Task DatabaseEnumerationClassifiesRegisteredUserDatabases(string initialDatabase){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,true);registry.Register("scratch",new Adapter());using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            var settings=new SqlConnectionStringBuilder(Connect(server).ConnectionString){InitialCatalog=initialDatabase};await using var connection=new SqlConnection(settings.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT dtb.name AS [Name],CAST(0 AS bit) AS [IsFabricDatabase],dtb.catalog_collation_type AS [CatalogCollation],dtb.collation_name AS [Collation],dtb.database_id AS [ID],CAST(CASE WHEN dtb.name IN ('master','model','msdb','tempdb') THEN 1 ELSE dtb.is_distributor END AS bit) AS [IsSystemObject],CAST(has_dbaccess(dtb.name) AS bit) AS [IsAccessible],dtb.name AS [DatabaseName2] FROM master.sys.databases AS dtb ORDER BY [Name] ASC";
            await using(var reader=await command.ExecuteReaderAsync()){
                var names=new List<string>();var ids=new HashSet<int>();
                while(await reader.ReadAsync()){
                    var name=reader.GetString(0);names.Add(name);Assert.False(reader.GetBoolean(1));Assert.Equal(typeof(bool),reader.GetFieldType(5));Assert.Equal(name=="master",reader.GetBoolean(5));Assert.True(reader.GetBoolean(6));Assert.Equal(name,reader.GetString(7));Assert.True(ids.Add(reader.GetInt32(4)));if(name!="master"){Assert.True(reader.GetInt32(4)>4);}
                }
                Assert.Equal(new[]{"fixture","master","scratch"},names);Assert.False(await reader.NextResultAsync());
            }
            command.CommandText="SELECT name,is_distributor,source_database_id,CAST(CASE WHEN source_database_id IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsDatabaseSnapshot FROM sys.databases WHERE name NOT IN ('master','model','msdb','tempdb') AND source_database_id IS NULL ORDER BY name";
            await using(var reader=await command.ExecuteReaderAsync()){Assert.False(reader.GetColumnSchema()[1].AllowDBNull);Assert.True(reader.GetColumnSchema()[2].AllowDBNull);Assert.True(await reader.ReadAsync());Assert.Equal("fixture",reader.GetString(0));Assert.False(reader.GetBoolean(1));Assert.True(reader.IsDBNull(2));Assert.False(reader.GetBoolean(3));Assert.True(await reader.ReadAsync());Assert.Equal("scratch",reader.GetString(0));Assert.False(reader.GetBoolean(1));Assert.True(reader.IsDBNull(2));Assert.False(reader.GetBoolean(3));Assert.False(await reader.ReadAsync());}
            command.CommandText="SELECT name FROM sys.databases WHERE is_read_only=1 AND is_distributor=0 AND database_id>4 ORDER BY name";Assert.Equal("fixture",await command.ExecuteScalarAsync());
            command.CommandText="SELECT HAS_DBACCESS(N'FIXTURE') AS KnownDatabase,HAS_DBACCESS(N'master') AS MasterDatabase,HAS_DBACCESS(N'not-registered') AS MissingDatabase,HAS_DBACCESS(NULL) AS NullDatabase";
            await using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(1,reader.GetInt32(0));Assert.Equal(1,reader.GetInt32(1));Assert.True(reader.IsDBNull(2));Assert.True(reader.IsDBNull(3));Assert.False(await reader.ReadAsync());}
            command.CommandText="SELECT HAS_DBACCESS(N'late')";Assert.IsType<DBNull>(await command.ExecuteScalarAsync());registry.Register("late",new Adapter());Assert.Equal(1,await command.ExecuteScalarAsync());
            command.CommandText="select has_dbaccess('msdb')";Assert.Equal(0,Convert.ToInt32(await command.ExecuteScalarAsync(),global::System.Globalization.CultureInfo.InvariantCulture));
            registry.Register("msdb",new Adapter());Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync(),global::System.Globalization.CultureInfo.InvariantCulture));
        }finally{await server.StopAsync();}
    }
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
