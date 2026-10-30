using System.Data;
using System.Reflection;
using MemoryPack;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace TestSeamless.Tests;

public readonly record struct ProtocolRow(int Id,int Value);
public sealed class ProtocolDatabase:DatabaseContainer{
    public ProtocolDatabase():base(DatabaseContainerOptions.InMemory with{IndexSize=1L<<12,MemorySize=1L<<20}){}
    public Table<ProtocolRow> Rows{[return:Seamless.Linq.UnorderedEnumerable]get=>this.Table<ProtocolRow>();}
}

public sealed class ProtocolModule:INativeQueryModule{
    public TaskCompletionSource<bool> Blocked{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Exited{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
        var database=(ProtocolDatabase)context.Database;
        if(queryId=="rows"){
            foreach(var row in database.Rows){context.CancellationToken.ThrowIfCancellationRequested();yield return MemoryPackSerializer.Serialize(row.Value);}
        }else if(queryId=="stream"){
            try{
                yield return MemoryPackSerializer.Serialize(123);
                this.Blocked.TrySetResult(true);
                context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return MemoryPackSerializer.Serialize(456);
            }finally{this.Exited.TrySetResult(true);}
        }else if(queryId=="insert"){
            var value=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            database.Rows.Add(new(value,value));
            yield return MemoryPackSerializer.Serialize(value);
        }else{throw new InvalidOperationException("Unknown query");}
    }
}

[Collection("Protocol serial")]public sealed class ProtocolTests{
    private const string Fingerprint="protocol-fingerprint";
    private static SqlConnection Connect(string database="first",bool pooling=false){
        var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog=database,UserID="sa",Password="test",Encrypt=false,TrustServerCertificate=true,Pooling=pooling,ConnectTimeout=5};
        var connection=new SqlConnection(builder.ConnectionString);connection.Open();return connection;
    }
    private static object? Scalar(SqlConnection connection,string sql){using var command=connection.CreateCommand();command.CommandText=sql;return command.ExecuteScalar();}
    private static void Execute(SqlConnection connection,string sql,SqlTransaction? transaction=null){using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=sql;command.ExecuteNonQuery();}

    [Fact]public void TdsClient(){
        using var first=new ProtocolDatabase();using var second=new ProtocolDatabase();first.Rows.Add(new(1,10));first.Rows.Add(new(2,20));second.Rows.Add(new(3,30));
        var adapter=new InProcessDatabaseAdapter(first);var registry=new DatabaseRegistry();registry.Register("first",adapter);registry.Register("second",new InProcessDatabaseAdapter(second));
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        using var connection=Connect("second");Assert.Equal("second",Scalar(connection,"SELECT DB_NAME()"));Assert.Equal(30,Scalar(connection,"SELECT Value FROM dbo.Rows"));
        Execute(connection,"USE first");Assert.Equal("first",Scalar(connection,"SELECT DB_NAME()"));
        using(var command=connection.CreateCommand()){
            command.CommandText="SELECT Value FROM dbo.Rows WHERE Id=1; SELECT Value FROM dbo.Rows WHERE Id=2";
            using var reader=command.ExecuteReader();Assert.True(reader.Read());Assert.Equal(10,reader.GetInt32(0));Assert.False(reader.Read());Assert.True(reader.NextResult());Assert.True(reader.Read());Assert.Equal(20,reader.GetInt32(0));
        }
        using(var command=connection.CreateCommand()){
            command.CommandText="SELECT name FROM sys.databases";using var reader=command.ExecuteReader();var names=new List<string>();while(reader.Read()){names.Add(reader.GetString(0));}Assert.Contains("first",names);Assert.Contains("second",names);Assert.Contains("master",names);
        }
    }

    [Fact]public void TdsTransactions(){
        using var database=new ProtocolDatabase();var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        Execute(connection,"BEGIN TRANSACTION");Execute(connection,"INSERT INTO dbo.Rows (Id,Value) VALUES (4,40)");Execute(connection,"ROLLBACK TRANSACTION");Assert.Null(Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=4"));
        using(var transaction=connection.BeginTransaction()){
            Execute(connection,"INSERT INTO dbo.Rows (Id,Value) VALUES (5,50)",transaction);transaction.Commit();
        }
        Assert.Equal(50,Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=5"));
        using(var transaction=connection.BeginTransaction()){
            Execute(connection,"INSERT INTO dbo.Rows (Id,Value) VALUES (6,60)",transaction);transaction.Rollback();
        }
        Assert.Null(Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=6"));
        using(var temporary=Connect()){Execute(temporary,"BEGIN TRANSACTION");Execute(temporary,"INSERT INTO dbo.Rows (Id,Value) VALUES (7,70)");}
        Assert.Null(Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=7"));
    }

    [Fact]public void TdsParameters(){
        using var database=new ProtocolDatabase();database.Rows.Add(new(1,10));database.Rows.Add(new(2,20));
        var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using(var command=connection.CreateCommand()){
            command.CommandText="SELECT Value FROM dbo.Rows WHERE Id=@id";command.Parameters.Add("@id",SqlDbType.Int).Value=1;Assert.Equal(10,command.ExecuteScalar());command.Prepare();Assert.Equal(10,command.ExecuteScalar());command.Parameters[0].Value=2;Assert.Equal(20,command.ExecuteScalar());
        }
    }

    [Fact]public void TdsValues(){
        using var database=new ProtocolDatabase();var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using var command=connection.CreateCommand();command.CommandText="SELECT CAST(NULL AS int), CAST(123.45 AS decimal(10,2)), CAST('2025-01-02' AS date), CAST('12:34:56' AS time), N'日本語'";
        using var reader=command.ExecuteReader();Assert.True(reader.Read());Assert.True(reader.IsDBNull(0));Assert.Equal(123.45m,reader.GetDecimal(1));Assert.Equal(new DateTime(2025,1,2),reader.GetDateTime(2));Assert.Equal(new TimeSpan(12,34,56),reader.GetTimeSpan(3));Assert.Equal("日本語",reader.GetString(4));Assert.False(reader.Read());
    }
    [Fact]public void TdsPoolReset(){
        using var first=new ProtocolDatabase();using var second=new ProtocolDatabase();var adapter=new InProcessDatabaseAdapter(first);var registry=new DatabaseRegistry();registry.Register("first",adapter);registry.Register("second",new InProcessDatabaseAdapter(second));
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        using(var connection=Connect("second",true)){
            Execute(connection,"USE first");using(var command=connection.CreateCommand()){command.CommandText="SELECT Value FROM dbo.Rows WHERE Id=@id";command.Parameters.Add("@id",SqlDbType.Int).Value=1;command.Prepare();Assert.Null(command.ExecuteScalar());}
            Execute(connection,"BEGIN TRANSACTION");Execute(connection,"INSERT INTO dbo.Rows (Id,Value) VALUES (1,10)");
        }
        using(var connection=Connect("second",true)){
            try{
                Assert.Equal("second",Scalar(connection,"SELECT DB_NAME()"));Execute(connection,"USE first");Assert.Null(Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=1"));
                using var command=connection.CreateCommand();command.CommandText="SELECT Value FROM dbo.Rows WHERE Id=@id";command.Parameters.Add("@id",SqlDbType.Int).Value=1;command.Prepare();Assert.Null(command.ExecuteScalar());
            }finally{SqlConnection.ClearPool(connection);}
        }
    }

    [Fact]public async Task TdsLargeResult(){
        using var database=new ProtocolDatabase();for(var i=0;i<2048;i++){database.Rows.Add(new(i,i));}
        var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using(var command=connection.CreateCommand()){
            command.CommandText="SELECT Id, Value FROM dbo.Rows";using var reader=command.ExecuteReader();var count=0;while(reader.Read()){Assert.Equal(reader.GetInt32(0),reader.GetInt32(1));count++;}Assert.Equal(2048,count);
        }
        using(var command=connection.CreateCommand()){
            command.CommandText="SELECT Id FROM dbo.Rows";using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await command.ExecuteReaderAsync(cancelled.Token));
        }
        Assert.Equal("first",Scalar(connection,"SELECT DB_NAME()"));
    }
    [Fact]public async Task TdsAttention(){
        using var database=new ProtocolDatabase();for(var i=0;i<2048;i++){database.Rows.Add(new(i,i));}
        var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();using var connection=Connect();
        using var transaction=connection.BeginTransaction();
        using(var command=connection.CreateCommand()){
            command.Transaction=transaction;command.CommandTimeout=20;command.CommandText="SELECT a.Id,b.Id FROM dbo.Rows a CROSS JOIN dbo.Rows b";
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var reader=await command.ExecuteReaderAsync(timeout.Token);Assert.True(await reader.ReadAsync(timeout.Token));
            command.Cancel();var count=1;try{while(await reader.ReadAsync(timeout.Token)){count++;}}catch(SqlException){}catch(OperationCanceledException) when(!timeout.IsCancellationRequested){}
            Assert.True(count<2048*2048,"The result completed instead of being cancelled.");
        }
        Execute(connection,"INSERT INTO dbo.Rows (Id,Value) VALUES (3000,3000)",transaction);transaction.Rollback();Assert.Null(Scalar(connection,"SELECT Value FROM dbo.Rows WHERE Id=3000"));Assert.Equal("first",Scalar(connection,"SELECT DB_NAME()"));
    }

    [Fact]public async Task NativeInterruptedTransaction(){
        using var database=new ProtocolDatabase();database.Rows.Add(new(1,10));var registry=new DatabaseRegistry();registry.Register("first",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];var definitions=new[]{new NativeQueryDefinition("stream","first",Fingerprint,"int","int"),new NativeQueryDefinition("insert","first",Fingerprint,"int","int",false),new NativeQueryDefinition("rows","first",Fingerprint,"int","int")};
        var module=new ProtocolModule();using var server=new NativeQueryServer(registry,14945);server.Register(new NativeModuleManifest("test","","","",version,definitions),module,version);server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));await using var client=new NativeQueryClient("127.0.0.1",14945);var transaction=await client.BeginTransactionAsync("first",timeout.Token);
        await foreach(var value in transaction.QueryAsync<int,int>("test","insert",Fingerprint,55,timeout.Token)){Assert.Equal(55,value);}
        var iterator=transaction.QueryAsync<int,int>("test","stream",Fingerprint,0,timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await iterator.MoveNextAsync());Assert.Equal(123,iterator.Current);await module.Blocked.Task.WaitAsync(timeout.Token);await iterator.DisposeAsync();await module.Exited.Task.WaitAsync(timeout.Token);
        await Assert.ThrowsAsync<NativeQueryException>(()=>transaction.RollbackAsync(timeout.Token));
        await using var verifier=new NativeQueryClient("127.0.0.1",14945);var values=new List<int>();await foreach(var value in verifier.QueryAsync<int,int>("test","rows","first",Fingerprint,0,timeout.Token)){values.Add(value);}Assert.Equal(new[]{10},values);
    }
    [Fact]public async Task LegacySqlClient(){
        using var database=new ProtocolDatabase();database.Rows.Add(new(1,10));var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("first",adapter);
        using var server=new TdsServer(14943,adapter,new SqlExecutionEngine(registry));server.Start();
        var sourceRoot=Environment.GetEnvironmentVariable("SEAMLESS_PROTOCOL_SOURCE_ROOT");if(string.IsNullOrWhiteSpace(sourceRoot)){var directory=new DirectoryInfo(AppContext.BaseDirectory);while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){directory=directory.Parent;}Assert.NotNull(directory);sourceRoot=directory!.FullName;}
        var script=Path.Combine(sourceRoot,"TestSeamless","Tests","Protocol-Legacy.ps1");var start=new System.Diagnostics.ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-NonInteractive");start.ArgumentList.Add("-ExecutionPolicy");start.ArgumentList.Add("Bypass");start.ArgumentList.Add("-File");start.ArgumentList.Add(script);
        var connection=new SqlConnectionStringBuilder{DataSource="127.0.0.1,14943",InitialCatalog="first",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};start.Environment["SEAMLESS_PROTOCOL_CONNECTION_STRING"]=connection.ConnectionString;
        using var process=System.Diagnostics.Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);await process.WaitForExitAsync();throw;}
        Assert.True(process.ExitCode==0,(await output)+(await error));
    }
    [Fact]public async Task NativeClient(){
        using var database=new ProtocolDatabase();database.Rows.Add(new(1,10));database.Rows.Add(new(2,20));
        var registry=new DatabaseRegistry();registry.Register("first",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);registry.Register("readonly",new InProcessDatabaseAdapter(database),readOnly:true,schemaFingerprint:Fingerprint);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var definitions=new[]{new NativeQueryDefinition("rows","first",Fingerprint,"int","int"),new NativeQueryDefinition("insert","first",Fingerprint,"int","int",false),new NativeQueryDefinition("insert","readonly",Fingerprint,"int","int",false)};
        var manifest=new NativeModuleManifest("test","","","",version,definitions);using var server=new NativeQueryServer(registry,14945);server.Register(manifest,new ProtocolModule(),version);server.Start();
        await using var client=new NativeQueryClient("127.0.0.1",14945);var values=new List<int>();await foreach(var value in client.QueryAsync<int,int>("test","rows","first",Fingerprint,0)){values.Add(value);}Assert.Equal(new[]{10,20},values.Order());
        await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("test","rows","first","stale",0));
        await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("test","insert","readonly",Fingerprint,9));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.ExecuteScalarAsync<int,int>("test","rows","first",Fingerprint,0,cancelled.Token));
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("test","insert","first",Fingerprint,8));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using(var transaction=await client.BeginTransactionAsync("first",timeout.Token)){
            var inserted=new List<int>();await foreach(var value in transaction.QueryAsync<int,int>("test","insert",Fingerprint,11,timeout.Token)){inserted.Add(value);}Assert.Equal(new[]{11},inserted);
            var visible=new List<int>();await foreach(var value in transaction.QueryAsync<int,int>("test","rows",Fingerprint,0,timeout.Token)){visible.Add(value);}Assert.Contains(11,visible);
            await transaction.RollbackAsync(timeout.Token);
        }
        values.Clear();await foreach(var value in client.QueryAsync<int,int>("test","rows","first",Fingerprint,0,timeout.Token)){values.Add(value);}Assert.DoesNotContain(11,values);
        await using(var transaction=await client.BeginTransactionAsync("first",timeout.Token)){
            await foreach(var value in transaction.QueryAsync<int,int>("test","insert",Fingerprint,12,timeout.Token)){Assert.Equal(12,value);}await transaction.CommitAsync(timeout.Token);
        }
        values.Clear();await foreach(var value in client.QueryAsync<int,int>("test","rows","first",Fingerprint,0,timeout.Token)){values.Add(value);}Assert.Contains(12,values);
        await Assert.ThrowsAsync<NativeQueryException>(()=>client.BeginTransactionAsync("readonly",timeout.Token));
    }
}
