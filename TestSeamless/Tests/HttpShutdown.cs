using System.Net.Http;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Sql;
namespace TestSeamless.Tests;
[Collection("Protocol serial")]public sealed class HttpShutdown{
    [Fact]public async Task StopWaitsForActiveEnumeratorDisposal(){
        using var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register(adapter.DatabaseName,adapter);
        using var server=new HttpServer(14944,adapter,new SqlExecutionEngine(registry));server.Start();using var client=new HttpClient();
        using var response=await client.GetAsync("http://localhost:14944/api/tables/dbo.Slow",HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var stopping=server.StopAsync();
        try{await adapter.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.False(stopping.IsCompleted,"Stop returned before the HTTP query enumerator finished disposing.");}
        finally{adapter.Release.Set();await stopping.WaitAsync(TimeSpan.FromSeconds(10));}
        Assert.True(adapter.Disposed);
    }
    private sealed class Adapter:IDatabaseAdapter,IDisposable{
        private static readonly ColumnMetadata[] Columns=[new("Value",typeof(string),"nvarchar",false,false,0,-1)];
        public TaskCompletionSource Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposing{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release{get;}=new(false);
        public bool Disposed{get;private set;}
        public string DatabaseName=>"http-fixture";
        public IReadOnlyList<TableMetadata> GetTables()=>[new("dbo","Slow",Columns)];
        public TableMetadata? FindTable(string name)=>this.GetTables()[0];
        public QueryResult ExecuteSelect(string table,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>new(Columns,this.Rows(),0);
        private IEnumerable<object?[]> Rows(){try{this.Entered.TrySetResult();while(true){yield return[new string('x',32768)];}}finally{this.Disposing.TrySetResult();this.Release.Wait(TimeSpan.FromSeconds(20));this.Disposed=true;}}
        public int ExecuteInsert(string table,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string table,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string table,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
        public void Dispose()=>this.Release.Dispose();
    }
}
