using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class SessionLockTimeoutTests{
    private static RuntimeSqlGeneratedRoot Database(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Set the D/F test root.");
        return new(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(root,"lock-timeout-"+Guid.NewGuid().ToString("N"))});
    }
    private static SqlConnection Connect(TdsServer server,bool pooling=false)=>new(new SqlConnectionStringBuilder{DataSource=$"127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=pooling,MaxPoolSize=1,ConnectTimeout=5}.ConnectionString);
    private static SqlExecutionEngine Engine(InProcessDatabaseAdapter adapter){var registry=new DatabaseRegistry();registry.Register("fixture",adapter);return new(registry);}
    private static async Task<object?> Scalar(SqlConnection connection,string sql){using var command=connection.CreateCommand();command.CommandText=sql;command.CommandTimeout=5;return await command.ExecuteScalarAsync();}
    [Fact,Trait("Category","Integration")]
    public async Task SettingIsSessionLocalAndResetsOnPoolReuse(){
        using var database=Database();var adapter=new InProcessDatabaseAdapter(database);using var server=new TdsServer(0,adapter,Engine(adapter));server.Start();
        try{
            await using var first=Connect(server,true);await first.OpenAsync();await using var second=Connect(server);await second.OpenAsync();
            Assert.Equal(-1,await Scalar(first,"SELECT @@LOCK_TIMEOUT"));Assert.Equal(-1,await Scalar(second,"SELECT @@LOCK_TIMEOUT"));
            Assert.Equal(10000,await Scalar(first,"SET LOCK_TIMEOUT 10000; SELECT @@LOCK_TIMEOUT AS LockTimeout"));Assert.Equal(-1,await Scalar(second,"SELECT @@LOCK_TIMEOUT"));
            foreach(var value in new[]{"0","-2","2147483648","@timeout"}){var error=await Assert.ThrowsAsync<SqlException>(()=>Scalar(first,"SET LOCK_TIMEOUT "+value));Assert.Equal(40515,error.Number);Assert.Equal(10000,await Scalar(first,"SELECT @@LOCK_TIMEOUT"));}
            Assert.Equal(-1,await Scalar(first,"SET LOCK_TIMEOUT -1; SELECT @@LOCK_TIMEOUT"));
            await Scalar(first,"SET LOCK_TIMEOUT 10000");var physical=first.ClientConnectionId;await first.CloseAsync();await first.OpenAsync();Assert.Equal(physical,first.ClientConnectionId);Assert.Equal(-1,await Scalar(first,"SELECT @@LOCK_TIMEOUT"));
        }finally{SqlConnection.ClearAllPools();await server.StopAsync();}
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("SELECT ObjectId FROM dbo.TypedOrders")]
    [InlineData("BEGIN TRANSACTION")]
    [InlineData("INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(23,N'timeout',1)")]
    public async Task BlockedSqlTimesOutAndConnectionCanBeReused(string sql){
        using var database=Database();var adapter=new InProcessDatabaseAdapter(database);using var server=new TdsServer(0,adapter,Engine(adapter));server.Start();
        try{
            await using var blocker=Connect(server);await blocker.OpenAsync();await using var waiting=Connect(server);await waiting.OpenAsync();
            await Scalar(waiting,"SET LOCK_TIMEOUT 100");await Scalar(blocker,"BEGIN TRANSACTION");
            try{var error=await Assert.ThrowsAsync<SqlException>(()=>Scalar(waiting,sql));Assert.Equal(1222,error.Number);Assert.Equal(100,await Scalar(waiting,"SELECT @@LOCK_TIMEOUT"));}
            finally{await Scalar(blocker,"ROLLBACK TRANSACTION");}
            await Scalar(waiting,"BEGIN TRANSACTION");await Scalar(waiting,"ROLLBACK TRANSACTION");Assert.Equal(0,await Scalar(waiting,"SELECT COUNT(*) FROM dbo.TypedOrders"));
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task RpcTransactionUsesTheSameWaitBudget(){
        using var database=Database();var adapter=new InProcessDatabaseAdapter(database);using var server=new TdsServer(0,adapter,Engine(adapter));server.Start();
        try{
            await using var blocker=Connect(server);await blocker.OpenAsync();await using var waiting=Connect(server);await waiting.OpenAsync();await Scalar(waiting,"SET LOCK_TIMEOUT 100");
            await using(var transaction=await blocker.BeginTransactionAsync()){var error=await Assert.ThrowsAsync<SqlException>(async()=>{await using var unexpected=await waiting.BeginTransactionAsync();});Assert.Equal(1222,error.Number);await transaction.RollbackAsync();}
            await using var retry=await waiting.BeginTransactionAsync();await retry.RollbackAsync();
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task ClientCancellationIsNotReportedAsLockTimeout(){
        using var database=Database();var adapter=new InProcessDatabaseAdapter(database);using var server=new TdsServer(0,adapter,Engine(adapter));server.Start();
        try{
            await using var blocker=Connect(server);await blocker.OpenAsync();await using var waiting=Connect(server);await waiting.OpenAsync();
            await Scalar(waiting,"SET LOCK_TIMEOUT 10000");await Scalar(blocker,"BEGIN TRANSACTION");
            try{
                using var command=waiting.CreateCommand();command.CommandText="SELECT @@LOCK_TIMEOUT; SELECT ObjectId FROM dbo.TypedOrders";command.CommandTimeout=5;
                await using(var reader=await command.ExecuteReaderAsync()){
                    Assert.True(await reader.ReadAsync());Assert.Equal(10000,reader.GetInt32(0));
                    // SqlClientの結果切替が同期的に待つ場合も、別スレッドから取消を送る。
                    using var cancel=new Timer(_=>command.Cancel(),null,100,Timeout.Infinite);
                    var error=await Assert.ThrowsAsync<SqlException>(async()=>await reader.NextResultAsync());Assert.Equal(0,error.Number);
                }
                Assert.Equal(10000,await Scalar(waiting,"SELECT @@LOCK_TIMEOUT"));
            }finally{await Scalar(blocker,"ROLLBACK TRANSACTION");}
            Assert.Equal(0,await Scalar(waiting,"SELECT COUNT(*) FROM dbo.TypedOrders"));
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public void DisposingPartialEnumerationReleasesTheReadLease(){
        using var database=Database();using(var seed=database.BeginTransaction()){database.TypedOrders.Add(new(1,"one",1));database.TypedOrders.Add(new(2,"two",1));seed.Commit();}
        var engine=Engine(new(database));using var reading=new SessionContext(1,"fixture");using var writing=new SessionContext(2,"fixture");engine.Execute("SET LOCK_TIMEOUT 100",writing);
        var result=engine.Execute("SELECT ObjectId FROM dbo.TypedOrders",reading);
        using(var rows=result.Rows.GetEnumerator()){
            Assert.True(rows.MoveNext());var blocked=Assert.Throws<SqlQueryException>(()=>engine.Execute("BEGIN TRANSACTION",writing));Assert.Equal(1222,blocked.Number);
        }
        engine.Execute("BEGIN TRANSACTION",writing);engine.Execute("ROLLBACK TRANSACTION",writing);
    }
    [Fact,Trait("Category","Integration")]
    public async Task ReusedSqlUsesTheEnumeratingConnectionsWaitBudget(){
        using var database=Database();var engine=Engine(new(database));using var first=new SessionContext(1,"fixture");using var second=new SessionContext(2,"fixture");using var blocker=new SessionContext(3,"fixture");
        engine.Execute("SET LOCK_TIMEOUT 10000",first);engine.Execute("SET LOCK_TIMEOUT 100",second);
        const string sql="SELECT ObjectId FROM dbo.TypedOrders";
        _=engine.Execute(sql,first);var result=engine.Execute(sql,second);engine.Execute("BEGIN TRANSACTION",blocker);
        var pending=Task.Run(()=>result.Rows.ToArray());
        try{var error=await Assert.ThrowsAsync<SqlQueryException>(async()=>await pending.WaitAsync(TimeSpan.FromSeconds(3)));Assert.Equal(1222,error.Number);}
        finally{engine.Execute("ROLLBACK TRANSACTION",blocker);await pending.ContinueWith(_=>{},TaskScheduler.Default);}
        Assert.Empty(engine.Execute(sql,second).Rows);
    }
    [Fact,Trait("Category","Integration")]
    public async Task AcquisitionDeadlineDoesNotLimitTheTransactionLifetime(){
        using var database=Database();var adapter=new InProcessDatabaseAdapter(database);using var server=new TdsServer(0,adapter,Engine(adapter));server.Start();
        try{
            await using var connection=Connect(server);await connection.OpenAsync();await Scalar(connection,"SET LOCK_TIMEOUT 100");await Scalar(connection,"BEGIN TRANSACTION");
            await Task.Delay(200);await Scalar(connection,"INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'after deadline',1)");await Scalar(connection,"COMMIT TRANSACTION");Assert.Equal(1,await Scalar(connection,"SELECT COUNT(*) FROM dbo.TypedOrders"));
        }finally{await server.StopAsync();}
    }
}
