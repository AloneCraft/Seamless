using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Execution;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;
using HistoryView=Seamless.Management.Components.実績履歴;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryExecutionHistory{
    [Fact]public async Task IdleRecorderCreatesNoFilesAndBlockedWriterDoesNotBlockDbOrExceedBudget(){
        var path=Root();var budget=new ResourceBudget(1<<20,0);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var recorder=new QueryExecutionHistoryStore(path,budget,new(MaximumPending:1),async(_,_)=>{entered.SetResult();await release.Task;});
        try{
            Assert.False(Directory.Exists(path));Assert.Equal(0,budget.MemoryBytes);
            Assert.True(recorder.TryRecord(Observation()));await entered.Task;
            Assert.False(recorder.TryRecord(Observation()));Assert.Equal(1,recorder.Status().Dropped);Assert.True(budget.MemoryBytes>0);
        }finally{release.TrySetResult();await recorder.DisposeAsync();}
        Assert.Equal(1,recorder.Status().Saved);Assert.Equal(0,budget.MemoryBytes);Assert.False(Directory.Exists(path));
    }
    [Fact]public async Task SaveFailureIsDiagnosedAndReservationsAreReleased(){
        var root=Root();Directory.CreateDirectory(Path.GetDirectoryName(root)!);File.WriteAllText(root,"occupied");
        var budget=new ResourceBudget(1<<20,0);
        await using var recorder=new QueryExecutionHistoryStore(root,budget);
        Assert.True(recorder.TryRecord(Observation()));await recorder.DisposeAsync();
        Assert.Equal(1,recorder.Status().SaveFailures);Assert.Equal(1,recorder.Status().Dropped);Assert.Equal("SaveFailed",recorder.Status().ErrorCode);Assert.Equal(0,budget.MemoryBytes);
    }
    [Fact]public async Task ConcurrentRecordAndDisposeNeverReleaseAClosedSignalOrLeakReservations(){
        var budget=new ResourceBudget(1<<20,0);
        var recorder=new QueryExecutionHistoryStore(Root(),budget,writeOverride:(_,_)=>Task.CompletedTask);
        using var start=new ManualResetEventSlim();
        var producers=Enumerable.Range(0,4).Select(_=>Task.Run(()=>{start.Wait();for(var index=0;index<128;index++){recorder.TryRecord(Observation());}})).ToArray();
        var stopping=Task.Run(async()=>{start.Wait();await recorder.DisposeAsync();});start.Set();
        await Task.WhenAll(producers.Append(stopping));await recorder.DisposeAsync();
        Assert.True(recorder.Status().Closed);Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,recorder.Status().PendingCount);
    }
    [Fact]public async Task IndependentPeriodsRemainReadableAfterManifestFilesAndDirectoryAreDeleted(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);var session=Guid.NewGuid();
        var locations=new List<string>();for(var index=1;index<=3;index++){locations.Add(periods.Write([new(session,index,Observation())]));}
        await using var recorder=new QueryExecutionHistoryStore(root,budget);
        await recorder.DisposeAsync();
        var all=await recorder.ReadAsync(new("definition","Value"),CancellationToken.None);Assert.Equal(3,all.Items.Length);
        File.Delete(Path.Combine(locations[1],"store.json"));
        var remaining=await recorder.ReadAsync(new("definition","Value"),CancellationToken.None);
        Assert.Equal(2,remaining.Items.Length);Assert.Equal(1,remaining.MissingSequences);Assert.Equal(1,remaining.InvalidFiles);Assert.False(remaining.CompletenessGuaranteed);
        Assert.False(File.Exists(Path.Combine(locations[1],"store.json")));
        var broken=Path.Combine(root,"period-broken");Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"period.seal"),"not a seal");
        Assert.Equal(2,(await recorder.ReadAsync(new("definition","Value"),CancellationToken.None)).InvalidFiles);
        Directory.Delete(root,true);Assert.True((await recorder.ReadAsync(new("definition","Value"),CancellationToken.None)).DirectoryMissing);
        await using var replacement=new QueryExecutionHistoryStore(root,budget);Assert.True(replacement.TryRecord(Observation()));await replacement.DisposeAsync();
        Assert.Single((await replacement.ReadAsync(new("definition","Value"),CancellationToken.None)).Items);
    }
    [Fact]public async Task PaginationFiltersExactTargetsAndRetentionBoundsFiles(){
        var root=Root();var budget=new ResourceBudget(64L<<20,128L<<20);var periods=new ExecutionHistoryPeriods(root,budget);var session=Guid.NewGuid();
        for(var index=1;index<=4;index++){periods.Write([new(session,index,Observation() with{CompletedAtUtc=DateTimeOffset.UnixEpoch.AddSeconds(index)})]);}
        await using var recorder=new QueryExecutionHistoryStore(root,budget,new(MaximumFiles:3));
        Assert.True(recorder.TryRecord(Observation()));await recorder.DisposeAsync();Assert.Equal(3,Directory.GetDirectories(root,"period-*").Length);
        var first=await recorder.ReadAsync(new("definition","Value",Limit:2),CancellationToken.None);Assert.Equal(2,first.Items.Length);Assert.True(first.HasMore);
        var next=await recorder.ReadAsync(new("definition","Value",Limit:2,Before:first.NextBefore),CancellationToken.None);Assert.Single(next.Items);Assert.False(next.HasMore);
        Assert.Empty((await recorder.ReadAsync(new("definition","dbo.Value"),CancellationToken.None)).Items);
    }
    [Fact]public async Task RealSummaryRetainsSuccessFailureAndUnboundCancellationWithoutChangingExceptions(){
        var root=Root();using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Service(store,null,startAuditWorker:false);var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var definition=Designer.ApplyQuery(Designer.Empty("definition","History"),new("Value","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 7;"));
        definition=Designer.ApplyQuery(definition,new("Fail","",DatabaseQueryKind.ScalarFunction,[],[],"int","throw new System.InvalidOperationException(\"do not persist this secret\");"));
        NativeWorkflow.Publish(service,owner,definition);
        await service.ExecuteObjectAsync(owner,new("History","Value",[]));Assert.False(Directory.Exists(Path.Combine(root,"execution-history-tsavorite-v1")));
        var success=await service.ExecuteObjectAsync(owner,new("History","Value",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        Assert.Equal("do not persist this secret",(await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExecuteObjectAsync(owner,new("History","Fail",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary}))).Message);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var runtime=typeof(Service).GetField("runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        var holding=Task.Run(()=>runtime.GetType().GetMethod("WithDatabaseIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(int)).Invoke(runtime,["definition",CancellationToken.None,(Func<int>)(()=>{entered.Set();release.Wait();return 0;})]));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try{
            using var cancellation=new CancellationTokenSource();var waiting=service.ExecuteObjectAsync(owner,new("History","Value",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary},cancellation.Token);
            cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        }finally{release.Set();await holding;}
        await service.DisposeExecutionHistoryAsync();
        var records=await service.QueryExecutionHistoryAsync(owner,new("definition","Value"));Assert.Equal(2,records.Items.Length);
        var completed=Assert.Single(records.Items,entry=>entry.Observation.Outcome=="Succeeded").Observation;
        Assert.Equal(success.Statistics!.ExecutionId,completed.ExecutionId);Assert.Equal(success.Statistics.Elapsed,completed.Elapsed);Assert.Equal(success.Statistics.ArtifactHash,completed.ArtifactHash);Assert.True(completed.IdentityResolved);
        var cancelled=Assert.Single(records.Items,entry=>entry.Observation.Outcome=="Cancelled").Observation;Assert.False(cancelled.IdentityResolved);Assert.Null(cancelled.Version);Assert.Null(cancelled.ArtifactHash);Assert.Equal("DatabaseGate",cancelled.Phase);
        var failed=Assert.Single((await service.QueryExecutionHistoryAsync(owner,new("definition","Fail"))).Items).Observation;
        Assert.Equal("Failed",failed.Outcome);Assert.Equal("InvalidOperationException",failed.ErrorCode);Assert.True(failed.IdentityResolved);
        Assert.DoesNotContain("do not persist this secret",string.Join("",Directory.GetFiles(Path.Combine(root,"execution-history-tsavorite-v1"),"*",SearchOption.AllDirectories).Select(File.ReadAllText)));
        Assert.Equal(7,(await service.ExecuteObjectAsync(owner,new("History","Value",[]))).Value!.Value.GetInt32());
        var viewer=new UserAccount("viewer","",[new("*",ManagementPermission.View)]);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.QueryExecutionHistoryAsync(viewer,new("definition","Value")))).Code);
        await using var concurrent=new ConcurrentManagementOperations(service);
        Assert.Equal(2,(await concurrent.QueryExecutionHistoryAsync(new("definition","Value"))).Items.Length);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Equal(2,(await ipc.QueryExecutionHistoryAsync(new("definition","Value"))).Items.Length);
        runtime.GetType().GetProperty("ExecutionObserved",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(runtime,(Action<QueryExecutionObservation>)(_=>throw new IOException("observer unavailable")));
        Assert.Equal(7,(await service.ExecuteObjectAsync(owner,new("History","Value",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary})).Value!.Value.GetInt32());
        Assert.Equal("do not persist this secret",(await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExecuteObjectAsync(owner,new("History","Fail",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary}))).Message);
        var diagnostic=await service.QueryExecutionHistoryAsync(owner,new("definition","Value"));Assert.Equal(2,diagnostic.Status.ObservationFailures);Assert.Equal("ObservationFailed",diagnostic.Status.ErrorCode);
    }
    [Theory][InlineData("Permission")][InlineData("Authentication")]
    public async Task HistoryViewClearsPreviouslyVisibleRowsAfterAccessLoss(string code){
        var operations=DispatchProxy.Create<IManagementOperations,HistoryOperations>();var proxy=(HistoryOperations)(object)operations;
        Exception? error=null;var entry=new QueryExecutionHistoryEntry(Guid.NewGuid(),1,Observation());
        proxy.Read=()=>error is null?Task.FromResult(new QueryExecutionHistoryPage([entry],null,false,new(Guid.NewGuid(),1,1,0,0,0,0,null,false),0,0,false,false)):Task.FromException<QueryExecutionHistoryPage>(error);
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestHistory? view=null;var rendered=await renderer.RenderComponentAsync<TestHistory>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB定義ID","definition"},{"対象ID","Value"},{"Capture",(Action<TestHistory>)(value=>view=value)}}));
            Assert.Contains(entry.Observation.StartedAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"),System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            Assert.Contains("<span>v1</span>",rendered.ToHtmlString());
            Assert.DoesNotContain("v@",rendered.ToHtmlString());
            var historyHtml=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Contains("今回起動の収集状況",historyHtml);Assert.Contains("表示中の記録1件",historyHtml);
            error=new ManagementException(code,"access lost");await view!.読むAsync();view.Refresh();
            Assert.Contains("access lost",rendered.ToHtmlString());Assert.DoesNotContain(entry.Observation.StartedAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"),rendered.ToHtmlString());
        });
    }
    public sealed class TestHistory:HistoryView{
        [Parameter]public Action<TestHistory> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public void Refresh()=>this.StateHasChanged();
    }
    public class HistoryOperations:DispatchProxy{
        public Func<Task<QueryExecutionHistoryPage>> Read=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>this.Read();
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"execution-history-"+Guid.NewGuid().ToString("N"));
    private static QueryExecutionObservation Observation()=>new(Guid.NewGuid(),"definition","Value",1,new('A',64),true,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(10),TimeSpan.FromMilliseconds(10),TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(4),1,1,false,"Succeeded","Execution",null);
}
