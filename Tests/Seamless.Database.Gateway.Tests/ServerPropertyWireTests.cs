using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;
public sealed class ServerPropertyWireTests{
    [Fact,Trait("Category","Integration")]
    public async Task EnginePropertyUsesExpressionTypeAndValue(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
            using var command=connection.CreateCommand();command.CommandText="SELECT SERVERPROPERTY('EngineEdition') AS DatabaseEngineType";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(3,reader.GetInt32(0));Assert.Equal("DatabaseEngineType",reader.GetName(0));Assert.False(await reader.ReadAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task UncastPropertyUsesBaseTypeRegardlessOfAlias(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            // Compatibility profile: SERVERPROPERTY normally has sql_variant metadata, which this gateway cannot yet send.
            command.CommandText="SELECT SERVERPROPERTY('EngineEdition'), SERVERPROPERTY('EngineEdition') AS ArbitraryName, CAST(SERVERPROPERTY('EngineEdition') AS int) AS DatabaseEngineType";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());
            for(var i=0;i<3;i++){Assert.Equal("int",reader.GetDataTypeName(i));Assert.Equal(typeof(int),reader.GetFieldType(i));Assert.Equal(3,reader.GetInt32(i));}
            Assert.Equal("",reader.GetName(0));Assert.Equal("ArbitraryName",reader.GetName(1));Assert.Equal("DatabaseEngineType",reader.GetName(2));Assert.False(await reader.ReadAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task CastAliasCaseAndUnknownPropertyHaveConsistentWireTypes(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT CAST(SERVERPROPERTY('EngineEdition') AS nvarchar(8)) AS Renamed, CAST(SERVERPROPERTY('ProductMajorVersion') AS int) AS Different, CASE WHEN SERVERPROPERTY('EngineEdition') = 3 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS Flag, SERVERPROPERTY('UnknownXYZ') AS Unknown, SERVERPROPERTY('ProductMajorVersion') AS IsMisleading, SERVERPROPERTY('IsClustered') AS Edition";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());
            Assert.Equal(typeof(string),reader.GetFieldType(0));Assert.Equal("nvarchar",reader.GetDataTypeName(0));Assert.Equal("3",reader.GetString(0));
            Assert.Equal(typeof(int),reader.GetFieldType(1));Assert.Equal(16,reader.GetInt32(1));
            Assert.Equal(typeof(bool),reader.GetFieldType(2));Assert.True(reader.GetBoolean(2));Assert.True(await reader.IsDBNullAsync(3));
            Assert.Equal(typeof(string),reader.GetFieldType(4));Assert.Equal("16",reader.GetString(4));Assert.Equal(typeof(int),reader.GetFieldType(5));Assert.Equal(0,reader.GetInt32(5));Assert.False(await reader.ReadAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task CaseUsesDeclaredTypesIncludingTypedNull(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT CASE WHEN SERVERPROPERTY('EngineEdition')=0 THEN CAST(NULL AS int) ELSE '1' END AS Value, CASE SERVERPROPERTY('EngineEdition') WHEN 3 THEN 'yes' ELSE 'no' END AS Answer";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(typeof(int),reader.GetFieldType(0));Assert.Equal(1,reader.GetInt32(0));Assert.Equal("varchar",reader.GetDataTypeName(1));Assert.Equal("yes",reader.GetString(1));Assert.False(await reader.ReadAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task InitializationBatchPreservesResultOrder(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT CAST(SERVERPROPERTY('EngineEdition') AS int) AS DatabaseEngineType; SELECT host_platform FROM sys.dm_os_host_info; SELECT CONNECTIONPROPERTY('net_transport') AS ConnectionProtocol;";
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(3,reader.GetInt32(0));Assert.False(await reader.ReadAsync());
            Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal("Windows",reader.GetString(0));Assert.False(await reader.ReadAsync());
            Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal("TCP",reader.GetString(0));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task UnsupportedExpressionFailsInsteadOfReturningFabricatedValue(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT SERVERPROPERTY('EngineEdition')+1 AS Value";var exception=await Assert.ThrowsAsync<SqlException>(async()=>{await using var reader=await command.ExecuteReaderAsync();while(await reader.ReadAsync()){}});
            Assert.Contains("Unsupported server property expression",exception.Message);
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task Ssms22ServerInformationInitializationBatch(){
        const string sql="DECLARE @edition sysname; SET @edition = cast(SERVERPROPERTY(N'EDITION') as sysname); SELECT case when @edition = N'SQL Azure' then 2 else 1 end as DatabaseEngineType, SERVERPROPERTY('EngineEdition') AS DatabaseEngineEdition, SERVERPROPERTY('ProductVersion') AS ProductVersion, @@MICROSOFTVERSION AS MicrosoftVersion, case when serverproperty('EngineEdition') = 12 then 1 when serverproperty('EngineEdition') = 11 and @@version like 'Microsoft Azure SQL Data Warehouse%' then 1 else 0 end as IsFabricServer, convert(sysname, SERVERPROPERTY(N'Collation')) AS Collation; select host_platform from sys.dm_os_host_info; if @edition = N'SQL Azure' select 'TCP' as ConnectionProtocol else exec ('select CONVERT(nvarchar(40),CONNECTIONPROPERTY(''net_transport'')) as ConnectionProtocol')";
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=sql;
            await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());
            Assert.Equal(1,reader.GetInt32(0));Assert.Equal(3,reader.GetInt32(1));Assert.Equal("16.0.1000.6",reader.GetString(2));Assert.Equal(0x100003E8,reader.GetInt32(3));Assert.Equal(0,reader.GetInt32(4));Assert.Equal("Japanese_CI_AS",reader.GetString(5));
            Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal("Windows",reader.GetString(0));
            Assert.True(await reader.NextResultAsync());Assert.True(await reader.ReadAsync());Assert.Equal("TCP",reader.GetString(0));Assert.False(await reader.NextResultAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task DatabaseCatalogCollationMetadataMatchesRows(){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,defaultCollation:"Japanese_CI_AS");
        using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SELECT name,catalog_collation_type,catalog_collation_type_desc,containment,containment_desc FROM sys.databases ORDER BY database_id";
            await using var reader=await command.ExecuteReaderAsync();Assert.Equal(typeof(int),reader.GetFieldType(1));Assert.Equal("nvarchar",reader.GetDataTypeName(2));
            Assert.True(await reader.ReadAsync());Assert.Equal("master",reader.GetString(0));Assert.Equal(0,reader.GetInt32(1));Assert.Equal("DATABASE_DEFAULT",reader.GetString(2));Assert.Equal((byte)0,reader.GetByte(3));Assert.Equal("NONE",reader.GetString(4));
            Assert.True(await reader.ReadAsync());Assert.Equal("fixture",reader.GetString(0));Assert.Equal(0,reader.GetInt32(1));Assert.Equal("DATABASE_DEFAULT",reader.GetString(2));Assert.Equal((byte)0,reader.GetByte(3));Assert.Equal("NONE",reader.GetString(4));Assert.False(await reader.ReadAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ZeroRowCountSettingAllowsFollowingQueryAndConnectionReuse(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
            command.CommandText="SET ROWCOUNT 0; SELECT CAST(17 AS int) AS Value;";
            await using(var reader=await command.ExecuteReaderAsync()){
                Assert.True(await reader.ReadAsync());Assert.Equal("Value",reader.GetName(0));Assert.Equal(17,reader.GetInt32(0));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
            }
            command.CommandText="SELECT CAST(29 AS int) AS Value";Assert.Equal(29,await command.ExecuteScalarAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task UnsupportedRowLimitFailsWithoutPoisoningConnection(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SET ROWCOUNT 1";
            var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());Assert.Equal(40515,error.Number);
            command.CommandText="SELECT CAST(29 AS int) AS Value";Assert.Equal(29,await command.ExecuteScalarAsync());
        }finally{await server.StopAsync();}
    }
    private static string ContainedAuthenticationProbe(string database)=>"USE ["+database.Replace("]","]]")+"]; IF (db_id() = 1) BEGIN -- connected to master\n SELECT 0 END ELSE BEGIN EXEC('select case when authenticating_database_id = 1 then 0 else 1 end from sys.dm_exec_sessions where session_id = @@SPID') END; USE ["+database.Replace("]","]]")+"]; -- resetting the context";
    [Theory,Trait("Category","Integration")]
    [InlineData("fixture")]
    [InlineData("master")]
    public async Task SsmsContainedAuthenticationProbeReturnsServerLoginAndUpdatesContext(string database){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=ContainedAuthenticationProbe(database);
            await using(var reader=await command.ExecuteReaderAsync()){
                Assert.True(await reader.ReadAsync());Assert.Equal(typeof(int),reader.GetFieldType(0));Assert.Equal(0,reader.GetInt32(0));Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
            }
            Assert.Equal(database,connection.Database);command.CommandText="SELECT DB_NAME()";Assert.Equal(database,await command.ExecuteScalarAsync());
        }finally{await server.StopAsync();}
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("condition")]
    [InlineData("dynamic")]
    [InlineData("extra")]
    [InlineData("unrelated")]
    public async Task ContainedAuthenticationCompatibilityDoesNotIgnoreOtherIfStatements(string change){
        var sql=ContainedAuthenticationProbe("fixture");
        sql=change switch{"condition"=>sql.Replace("db_id() = 1","db_id() = 2"),"dynamic"=>sql.Replace("authenticating_database_id = 1","authenticating_database_id = 2"),"extra"=>sql+"\nSELECT 42;",_=>"IF 1=1 SELECT 42"};
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=sql;
            var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteScalarAsync());Assert.Equal(40515,error.Number);
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ContainedAuthenticationProbeDoesNotAcceptUnknownDatabase(){
        var adapter=new Adapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText=ContainedAuthenticationProbe("missing");
            var error=await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteScalarAsync());Assert.Equal(4060,error.Number);Assert.Equal("fixture",connection.Database);
        }finally{await server.StopAsync();}
    }
    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata> GetTables()=>[];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
