using System.Collections.Concurrent;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Host=Seamless.Management.Server.ManagementApplicationHost;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class EmbeddedBusinessOperations{
    [Fact]public async Task CollectionCopiesCommitBeforeReadingAndReleaseOnHostStop(){
        var root=Root();await using var application=await Host.CreateAsync(new(root,ManagementExecutionMode.Embedded){Defaults=new(){ReadAuditEnabled=false}});
        var host=application.Embedded!;Initialize(host);var threads=new ConcurrentQueue<int>();var rowThreads=new ConcurrentQueue<int>();
        host.Invoke((service,user)=>{
            NativeWorkflow.Publish(service,user,Query("stream","System.Collections.Generic.IEnumerable<object?>","var db=context.GetDatabase<Db>();db.Rows.Add(new Records.Row(id,id));yield return id;if(mode==1){throw new System.InvalidOperationException(\"rollback\");}if(mode==2){yield return \"wrong\";}else{yield return id+1;}",true,",int id,int mode"));
            NativeWorkflow.Publish(service,user,Query("texts","System.Collections.Generic.IEnumerable<string>","for(var i=0;i<40;i++){yield return i+new string('x',100);}",false));
            service.BeforeQueryExecution=()=>threads.Enqueue(Environment.CurrentManagedThreadId);service.AfterQueryRow=()=>rowThreads.Enqueue(Environment.CurrentManagedThreadId);return true;
        });
        using(var result=host.ExecuteStream<int>(Request("stream",1))){
            Assert.Equal(2,result.Count);using var iterator=result.GetEnumerator();Assert.True(iterator.MoveNext());Assert.Equal(1,iterator.Current);
            Assert.Equal(1,host.ExecuteScalar<int>(Request("count")));Assert.Equal(2,host.ExecuteScalar<int>(Request("mutate",2)));
        }
        Assert.ThrowsAny<Exception>(()=>host.ExecuteStream<int>(Request("stream",3,1)));
        Assert.ThrowsAny<JsonException>(()=>host.ExecuteStream<int>(Request("stream",4,2)));
        Assert.Equal("ResultLimit",Assert.Throws<ManagementException>(()=>host.ExecuteStream<int>(Request("stream",5) with{MaximumRows=1})).Code);
        Assert.Throws<global::Seamless.Execution.ResourceLimitException>(()=>host.ExecuteStream<int>(Request("stream",6),memoryLimitBytes:128,temporaryStorageLimitBytes:0));
        Assert.Equal(2,host.ExecuteScalar<int>(Request("count")));
        using(var empty=host.ExecuteStream<int>(Request("mutate",7,2))){Assert.Empty(empty);}
        Assert.Equal(3,host.ExecuteScalar<int>(Request("count")));
        rowThreads.Clear();
        using(var result=host.ExecuteStream<string>(Request("texts"),memoryLimitBytes:4096)){Assert.Equal(40,result.Count);Assert.Equal(40,rowThreads.Count);Assert.All(rowThreads,id=>Assert.Equal(host.ExecutionThreadId,id));}
        using(var cancellation=new CancellationTokenSource()){
            var copied=0;
            host.Invoke((service,_)=>{service.AfterQueryRow=()=>{rowThreads.Enqueue(Environment.CurrentManagedThreadId);if(++copied==2){cancellation.Cancel();}};return true;});
            Assert.ThrowsAny<OperationCanceledException>(()=>host.ExecuteStream<int>(Request("stream",8),cancellation.Token));
            host.Invoke((service,_)=>{service.AfterQueryRow=()=>rowThreads.Enqueue(Environment.CurrentManagedThreadId);return true;});
            Assert.Equal(2,copied);Assert.Equal(3,host.ExecuteScalar<int>(Request("count")));
        }
        using(var cancellation=new CancellationTokenSource()){
            using var result=host.ExecuteStream<string>(Request("texts"),cancellation.Token,memoryLimitBytes:4096);
            Assert.True(result.Spilled);using var iterator=result.GetEnumerator();Assert.True(iterator.MoveNext());cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(()=>iterator.MoveNext());Assert.Equal(3,host.ExecuteScalar<int>(Request("count")));
        }
        var pending=host.ExecuteStream<string>(Request("texts"),memoryLimitBytes:4096);Assert.True(pending.Spilled);
        var memory=pending.Budget;Assert.True(memory.TemporaryStorageBytes>0);
        using var pendingIterator=pending.GetEnumerator();Assert.True(pendingIterator.MoveNext());var retained=pendingIterator.Current;
        Assert.NotEmpty(threads);Assert.All(threads,id=>Assert.Equal(host.ExecutionThreadId,id));
        await application.DisposeAsync();Assert.Equal(0,memory.MemoryBytes);Assert.Equal(0,memory.TemporaryStorageBytes);
        Assert.Equal("0"+new string('x',100),retained);Assert.Throws<ObjectDisposedException>(()=>pendingIterator.MoveNext());pending.Dispose();
        File.WriteAllText(Path.Combine(root,"stream-fixture.json"),JsonSerializer.Serialize(new{Root=root,ExpectedCount=3,Rows=40,host.ExecutionThreadId,QueryThreads=threads.ToArray(),RowThreads=rowThreads.ToArray(),RetainedValue=retained,MemoryBytes=memory.MemoryBytes,TemporaryStorageBytes=memory.TemporaryStorageBytes},ManagementJson.Options));
        await using var reopened=await Host.OpenAsync(new(root,ManagementExecutionMode.Embedded));Assert.Equal(3,reopened.Embedded!.ExecuteScalar<int>(Request("count")));
    }
    [Fact]public async Task SynchronousBusinessAndManagerOperationsShareTheThreadAndAtomicWrites(){
        var root=Root();
        await using(var application=await Host.CreateAsync(new(root,ManagementExecutionMode.Embedded){Defaults=new(){ReadAuditSaveSeconds=3600}})){
            var host=application.Embedded!;Initialize(host);var threads=new ConcurrentQueue<int>();var auditThreads=new ConcurrentQueue<int>();
            host.Invoke((service,_)=>{service.BeforeQueryExecution=()=>threads.Enqueue(Environment.CurrentManagedThreadId);service.AfterRowAuthorization=()=>threads.Enqueue(Environment.CurrentManagedThreadId);service.AuditTransferCheckpoint=phase=>{if(phase=="BeforeTargetCommit"){auditThreads.Enqueue(Environment.CurrentManagedThreadId);}};return true;});
            Assert.Equal(0,host.ExecuteScalar<int>(Request("count")));
            Assert.Equal(1,host.ExecuteScalar<int>(Request("mutate",1,0)));
            Assert.ThrowsAny<Exception>(()=>host.ExecuteScalar<int>(Request("mutate",101,1)));
            Assert.Equal("ResultShape",Assert.Throws<ManagementException>(()=>host.ExecuteScalar<int>(Request("mutate",102,2))).Code);
            Assert.Equal("ResultShape",Assert.Throws<ManagementException>(()=>host.ExecuteScalar<int>(Request("mutate",103,3) with{MaximumRows=1})).Code);
            Assert.ThrowsAny<JsonException>(()=>host.ExecuteScalar<int>(Request("mutate",104,4)));
            Assert.Equal(1,host.ExecuteScalar<int>(Request("count")));
            Assert.Null(host.ExecuteScalar<string?>(Request("mutate",2,5)));
            var rows=host.ReadRows(new("Native","Rows"));Assert.Equal(2,rows.Rows.Length);var original=Assert.Single(rows.Rows,row=>row.Values.GetProperty("id").GetInt32()==1);
            var commit=host.CommitRows(new("Native",rows.Version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Replace,original.Key,original.Stamp,ManagementJson.Element(new{Id=1,Value=42}))]));Assert.Equal(1,commit.ChangedRows);
            var launch=application.Manager.Sessions.CreateLaunch();var session=application.Manager.Sessions.Redeem(new Uri(launch.Url).Fragment[1..],application.Manager.Origin.GetLeftPart(UriPartial.Authority));
            IManagementOperations browser=new BrowserManagementOperations(application.Manager.Sessions,host,()=>Task.FromResult<string?>(session.Id));
            var browserRows=await browser.ReadRowsAsync(new("Native","Rows"));Assert.Equal(42,Assert.Single(browserRows.Rows,row=>row.Values.GetProperty("id").GetInt32()==1).Values.GetProperty("value").GetInt32());
            var writes=await Task.WhenAll(Enumerable.Range(10,6).Select(id=>Task.Run(()=>host.ExecuteScalar<int>(Request("mutate",id,0)))));Assert.Equal(Enumerable.Range(3,6),writes.Order());
            await browser.CommitRowsAsync(new("Native",rows.Version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=30,Value=300}))]));
            Assert.Equal(9,host.ExecuteScalar<int>(Request("count")));
            Assert.Equal(50,host.ExecuteObject(new("Native","TimesTen",[ManagementJson.Element(5)])).Value!.Value.GetInt32());
            using var auditDeadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while((await application.Operations.AuditEventsAsync(new(Action:AuditAction.DataChange),auditDeadline.Token)).Items.Length<10){await Task.Delay(10,auditDeadline.Token);}
            Assert.Equal(10,(await application.Operations.AuditEventsAsync(new(Action:AuditAction.DataChange))).Items.Length);
            Assert.NotEmpty(threads);Assert.All(threads,thread=>Assert.Equal(host.ExecutionThreadId,thread));
            Assert.NotEmpty(auditThreads);Assert.All(auditThreads,thread=>Assert.Equal(host.ExecutionThreadId,thread));
            File.WriteAllText(Path.Combine(root,"business-fixture.json"),JsonSerializer.Serialize(new{ExpectedCount=9,host.ExecutionThreadId,QueryThreads=threads.ToArray(),AuditThreads=auditThreads.ToArray()},ManagementJson.Options));
        }
        await using var reopened=await Host.OpenAsync(new(root,ManagementExecutionMode.Embedded));
        Assert.Equal(9,reopened.Embedded!.ExecuteScalar<int>(Request("count")));Assert.Equal(9,reopened.Embedded.ReadRows(new("Native","Rows")).Rows.Length);
    }
    [Fact]public async Task RestartProjectsPendingWalAuditOnTheDbThreadWithoutAnotherQuery(){
        var root=Root();
        await using(var application=await Host.CreateAsync(new(root,ManagementExecutionMode.Embedded){Defaults=new(){ReadAuditEnabled=false}})){
            var host=application.Embedded!;Initialize(host);
            host.Invoke((service,_)=>{service.AuditTransferCheckpoint=phase=>{if(phase=="BeforeTargetCommit"){throw new IOException("injected projection failure");}};return true;});
            Assert.Equal(1,host.ExecuteScalar<int>(Request("mutate",1,0)));
            host.Invoke((service,_)=>{service.FlushAuditTransfersAsync().GetAwaiter().GetResult();return true;});
            Assert.Empty((await application.Operations.AuditEventsAsync(new(Action:AuditAction.DataChange))).Items);
            Assert.Contains((await application.Operations.AuditDeliveryStatusAsync()).Items,item=>item.ErrorCode=="ProjectionFailed");
        }
        await using var reopened=await Host.OpenAsync(new(root,ManagementExecutionMode.Embedded));var threads=new ConcurrentQueue<int>();
        reopened.Embedded!.Invoke((service,_)=>{service.AuditTransferCheckpoint=phase=>{if(phase=="BeforeTargetCommit"){threads.Enqueue(Environment.CurrentManagedThreadId);}};return true;});
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while((await reopened.Operations.AuditEventsAsync(new(Action:AuditAction.DataChange),deadline.Token)).Items.Length==0){await Task.Delay(10,deadline.Token);}
        Assert.Single((await reopened.Operations.AuditEventsAsync(new(Action:AuditAction.DataChange))).Items);
        Assert.NotEmpty(threads);Assert.All(threads,id=>Assert.Equal(reopened.Embedded.ExecutionThreadId,id));
        Assert.True((await reopened.Operations.AuditDeliveryStatusAsync()).Running);
        Assert.Equal(1,reopened.Embedded.ExecuteScalar<int>(Request("count")));
    }

    [Fact]public async Task QueuedCancellationAndFrozenInputsRemainSafeUnderBackpressure(){
        var root=Root();await using var application=await Host.CreateAsync(new(root,ManagementExecutionMode.Embedded){EmbeddedQueueCapacity=1,Defaults=new(){ReadAuditEnabled=false}});var host=application.Embedded!;Initialize(host);
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
            var running=host.InvokeAsync((_,_)=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));return true;}).AsTask();Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            try{
                using var cancellation=new CancellationTokenSource();var pending=host.ReadRowsAsync(new("Native","Rows"),cancellation.Token);cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(1)));
                using var scalarCancellation=new CancellationTokenSource();scalarCancellation.Cancel();
                Assert.ThrowsAny<OperationCanceledException>(()=>host.ExecuteScalar<int>(Request("mutate",999,0),scalarCancellation.Token));
            }finally{release.Set();await running;}
        }
        Assert.Equal(0,host.ExecuteScalar<int>(Request("count")));var version=host.ReadRows(new("Native","Rows")).Version;
        using(var values=JsonDocument.Parse("{\"Id\":7,\"Value\":70}")){
            var changes=new[]{new TableRowChange("Rows",TableRowChangeKind.Insert,Values:values.RootElement)};
            var saved=await WhileBlocked(host,()=>host.CommitRowsAsync(new("Native",version,Guid.NewGuid(),changes)),()=>{values.Dispose();changes[0]=new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=8,Value=80}));});Assert.Equal(1,saved.ChangedRows);
        }
        using(var key=JsonDocument.Parse("{\"Id\":7}")){
            var rows=await WhileBlocked(host,()=>host.ReadRowsAsync(new("Native","Rows",Key:key.RootElement)),key.Dispose);Assert.Equal(7,Assert.Single(rows.Rows).Values.GetProperty("id").GetInt32());
        }
        using(var value=JsonDocument.Parse("5")){
            var arguments=new[]{value.RootElement};var result=await WhileBlocked(host,()=>host.ExecuteObjectAsync(new("Native","TimesTen",arguments)),()=>{value.Dispose();arguments[0]=ManagementJson.Element(9);});Assert.Equal(50,result.Value!.Value.GetInt32());
        }
        Assert.Equal(1,host.ExecuteScalar<int>(Request("count")));
        await application.DisposeAsync();using var reopened=new ManagementStore(root);Assert.Single(reopened.AuditEvents(new(Action:AuditAction.DataChange)).Items);
    }
    private static async Task<T> WhileBlocked<T>(InProcessManagementHost host,Func<Task<T>> enqueue,Action afterQueued){
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var running=host.InvokeAsync((_,_)=>{entered.Set();Assert.True(release.Wait(TimeSpan.FromSeconds(10)));return true;}).AsTask();Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        Task<T> pending;
        try{pending=enqueue();afterQueued();}finally{release.Set();await running;}
        return await pending;
    }
    private static void Initialize(InProcessManagementHost host)=>host.Invoke((service,user)=>{
        var database=NativeWorkflow.Database();database=database with{Source=database.Source with{Files=database.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<","public ScalarFunction<int> TimesTen(int value)=>this.ScalarFunction(()=>value*10);public Table<",StringComparison.Ordinal)}).ToArray()}};
        NativeWorkflow.Publish(service,user,database);
        NativeWorkflow.Publish(service,user,Query("count","int","return context.GetDatabase<Db>().Rows.Count;",false));
        NativeWorkflow.Publish(service,user,Query("mutate","object?","var db=context.GetDatabase<Db>();db.Rows.Add(new Records.Row(id,id*10));if(mode==1){throw new System.InvalidOperationException(\"rollback\");}if(mode==2){return new int[0];}if(mode==3){return new[]{1,2};}if(mode==4){return \"not-a-number\";}if(mode==5){return null;}return db.Rows.Count;",true,",int id,int mode"));return true;
    });
    private static Definition Query(string id,string result,string body,bool writes,string parameters="")=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static "+result+" Run(ManagementExecutionContext context"+parameters+"){"+body+"}}")],[]),writes);
    private static ExecuteRequest Request(string id,int rowId=0,int mode=0)=>new(id,null,ManagementJson.Element(new{id=rowId,mode}));
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"business-"+Guid.NewGuid().ToString("N"));
    internal static int RunDirectProbe(string root){
        using var host=InProcessManagementHost.Open(root);var observed=0;
        host.Invoke((service,_)=>{service.BeforeQueryExecution=()=>observed=Environment.CurrentManagedThreadId;return true;});
        var count=host.ExecuteScalar<int>(Request("count"));
        var clients=AppDomain.CurrentDomain.GetAssemblies().Where(assembly=>assembly.GetName().Name?.Contains("ClientSchema",StringComparison.Ordinal)==true).Select(assembly=>assembly.GetName().Name).ToArray();
        if(observed!=host.ExecutionThreadId||clients.Length!=0){throw new InvalidOperationException("直接呼び出しの実行スレッドまたは参照DLLが一致しません。");}
        Console.WriteLine(JsonSerializer.Serialize(new{ProcessId=Environment.ProcessId,host.ExecutionThreadId,ObservedThreadId=observed,Count=count,GeneratedClientAssemblies=clients},ManagementJson.Options));return 0;
    }
    internal static int RunStreamProbe(string root){
        using var host=InProcessManagementHost.Open(root);var observed=0;var rowThreads=new List<int>();
        host.Invoke((service,_)=>{service.BeforeQueryExecution=()=>observed=Environment.CurrentManagedThreadId;service.AfterQueryRow=()=>rowThreads.Add(Environment.CurrentManagedThreadId);return true;});
        using var result=host.ExecuteStream<string>(Request("texts"),memoryLimitBytes:4096);var budget=result.Budget;
        using(var iterator=result.GetEnumerator()){
            for(var index=0;index<5;index++){if(!iterator.MoveNext()||iterator.Current!=index+new string('x',100)){throw new InvalidDataException("集合の内容が一致しません。");}}
            if(host.ExecuteScalar<int>(Request("count"))!=3||!result.Spilled){throw new InvalidOperationException("部分読取中の次クエリまたは退避が一致しません。");}
        }
        var clients=AppDomain.CurrentDomain.GetAssemblies().Where(assembly=>assembly.GetName().Name?.Contains("ClientSchema",StringComparison.Ordinal)==true).Select(assembly=>assembly.GetName().Name).ToArray();
        if(observed!=host.ExecutionThreadId||rowThreads.Count!=40||rowThreads.Any(id=>id!=host.ExecutionThreadId)||clients.Length!=0||budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0){throw new InvalidOperationException("集合の実行スレッド・所有資源・参照DLLが一致しません。");}
        Console.WriteLine(JsonSerializer.Serialize(new{ProcessId=Environment.ProcessId,host.ExecutionThreadId,ObservedThreadId=observed,RowThreads=rowThreads,Count=result.Count,ReadCount=5,Spilled=result.Spilled,budget.MemoryBytes,budget.TemporaryStorageBytes,GeneratedClientAssemblies=clients},ManagementJson.Options));return 0;
    }
}
