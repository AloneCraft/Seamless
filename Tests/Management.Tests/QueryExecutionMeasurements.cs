using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;
using ResultsView=Seamless.Management.Components.問い合わせ結果;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class QueryExecutionMeasurements{
    [Fact]public void DefaultContractsRetainTheirLegacyWireShape(){
        var request=new ObjectExecutionRequest("DB","Query",[]);
        var result=new ObjectExecutionResult(1,true,null,[],[],false);
        Assert.Equal(ObjectExecutionMeasurementMode.None,request.MeasurementMode);
        Assert.Null(result.Statistics);
        Assert.DoesNotContain("measurementMode",JsonSerializer.Serialize(request,ManagementJson.Options));
        Assert.DoesNotContain("traceOptions",JsonSerializer.Serialize(request,ManagementJson.Options));
        Assert.DoesNotContain("statistics",JsonSerializer.Serialize(result,ManagementJson.Options));
        Assert.DoesNotContain("traceExecutionId",JsonSerializer.Serialize(result,ManagementJson.Options));
        var measured=request with{MeasurementMode=ObjectExecutionMeasurementMode.Summary};
        Assert.Equal(ObjectExecutionMeasurementMode.Summary,measured.Freeze().MeasurementMode);
        Assert.Equal(ObjectExecutionMeasurementMode.Summary,JsonSerializer.Deserialize<ObjectExecutionRequest>(JsonSerializer.Serialize(measured,ManagementJson.Options),ManagementJson.Options)!.MeasurementMode);
    }

    [Fact]public async Task SummaryMeasuresActualScalarViewAndFunctionWithoutChangingTheirOutput(){
        using var fixture=new PublishedQueries();
        var identities=new HashSet<Guid>();
        foreach(var (name,args) in new (string,JsonElement[])[]{("Amount",[]),("ItemList",[]),("Find",[JsonSerializer.SerializeToElement(0m)])}){
            var request=new ObjectExecutionRequest("Measurements",name,args,ExpectedVersion:1);
            var plain=await fixture.Execute(request);
            var result=await fixture.Execute(request with{MeasurementMode=ObjectExecutionMeasurementMode.Summary});
            Assert.Null(plain.Statistics);
            Assert.Equal(plain.IsSequence,result.IsSequence);
            Assert.Equal(plain.Value?.GetRawText(),result.Value?.GetRawText());
            Assert.Equal(plain.Rows.Select(row=>row.GetRawText()).Order(),result.Rows.Select(row=>row.GetRawText()).Order());
            var statistics=Assert.IsType<ObjectExecutionStatistics>(result.Statistics);
            Assert.True(identities.Add(statistics.ExecutionId));
            Assert.Equal("measurements",statistics.DefinitionId);Assert.Equal(name,statistics.ObjectId);
            Assert.Equal(1,statistics.Version);Assert.Equal(fixture.Service.GetDefinition(fixture.Owner,"measurements").ArtifactHash,statistics.ArtifactHash);
            Assert.Equal(TimeSpan.Zero,statistics.StartedAtUtc.Offset);Assert.Equal(TimeSpan.Zero,statistics.CompletedAtUtc.Offset);
            Assert.True(statistics.Elapsed>=statistics.DatabaseGateWait);
            Assert.NotNull(statistics.TimeToFirstOutput);Assert.InRange(statistics.TimeToFirstOutput!.Value,statistics.DatabaseGateWait,statistics.Elapsed);
            Assert.Equal(result.IsSequence?2L:1L,statistics.ReturnedOutputCount);
            Assert.Equal(statistics.ReturnedOutputCount,statistics.ObservedOutputCount);Assert.False(statistics.IsPartial);
        }
    }

    [Fact]public async Task EmptyAndLimitedSequencesReportObservedAndReturnedCountsSeparately(){
        using var fixture=new PublishedQueries();
        var empty=await fixture.Execute(new("Measurements","Find",[JsonSerializer.SerializeToElement(100m)]){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        var emptyStatistics=Assert.IsType<ObjectExecutionStatistics>(empty.Statistics);
        Assert.Empty(empty.Rows);Assert.Null(emptyStatistics.TimeToFirstOutput);Assert.Equal(0,emptyStatistics.ObservedOutputCount);Assert.Equal(0,emptyStatistics.ReturnedOutputCount);Assert.False(emptyStatistics.IsPartial);
        var limited=await fixture.Execute(new("Measurements","ItemList",[],Limit:1){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        var statistics=Assert.IsType<ObjectExecutionStatistics>(limited.Statistics);
        Assert.Single(limited.Rows);Assert.True(limited.HasMore);Assert.True(statistics.IsPartial);Assert.Equal(2,statistics.ObservedOutputCount);Assert.Equal(1,statistics.ReturnedOutputCount);
    }

    [Fact]public async Task GateWaitingAndCancellationLeaveTheDatabaseUsable(){
        using var fixture=new PublishedQueries();
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var holding=Task.Run(()=>fixture.HoldGate(()=>{entered.Set();release.Wait();}));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try{
            using var cancellation=new CancellationTokenSource();
            var cancelled=fixture.Execute(new("Measurements","ItemList",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary},cancellation.Token);
            Assert.False(cancelled.IsCompleted);cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelled);
            var waiting=fixture.Execute(new("Measurements","ItemList",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
            Assert.False(waiting.IsCompleted);release.Set();await holding;
            var result=await waiting;Assert.Equal(2,result.Rows.Length);Assert.True(result.Statistics!.DatabaseGateWait>TimeSpan.Zero);
        }finally{release.Set();await holding;}
        Assert.Equal(2,(await fixture.Execute(new("Measurements","ItemList",[]))).Rows.Length);
    }

    [Fact]public async Task ScalarAndIteratorDisposalFailuresKeepTheirExceptionsAndReleaseTheGate(){
        using var fixture=new PublishedQueries();
        foreach(var (name,message) in new[]{("Fail","measurement failure"),("DisposeFailure","iterator disposed")}){
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Execute(new("Measurements",name,[],Limit:1){MeasurementMode=ObjectExecutionMeasurementMode.Summary}));
            Assert.Equal(message,error.Message);
            Assert.Equal(2,(await fixture.Execute(new("Measurements","ItemList",[]))).Rows.Length);
        }
        var invalid=await Assert.ThrowsAsync<ManagementException>(()=>fixture.Execute(new("Measurements","ItemList",[]){MeasurementMode=(ObjectExecutionMeasurementMode)99}));
        Assert.Equal("InvalidRequest",invalid.Code);
    }

    [Fact]public async Task MeasurementAndVisibilityChangesDoNotExecuteAndOnlySummaryPublishesStatistics(){
        var calls=0;var callbacks=0;
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();
        ((ReadOperations)(object)operations).Read=(_,args)=>{
            calls++;var request=(ObjectExecutionRequest)args[0]!;
            Assert.Equal(calls==1?ObjectExecutionMeasurementMode.None:ObjectExecutionMeasurementMode.Summary,request.MeasurementMode);
            return Task.FromResult(Result());
        };
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestResults? view=null;
            var rendered=await renderer.RenderComponentAsync<TestResults>(Parameters(value=>view=value,_=>callbacks++));
            Assert.Equal(0,calls);await view!.Execute();Assert.Equal(1,calls);Assert.Equal(0,callbacks);
            view.Mode(ObjectExecutionMeasurementMode.Summary);view.Show(false);view.Show(true);Assert.Equal(1,calls);
            await view.Execute();view.Refresh();Assert.Equal(2,calls);Assert.Equal(1,callbacks);
            Assert.Contains("軽量実測表示",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            Assert.Contains("<strong>v1</strong>",rendered.ToHtmlString());
            Assert.DoesNotContain("v@",rendered.ToHtmlString());
            view.Show(false);Assert.DoesNotContain("軽量実測表示",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));Assert.Equal(2,calls);Assert.Equal(1,callbacks);
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task TargetChangesAndDisposalCancelAndIgnoreLateMeasurements(bool dispose){
        var callbacks=0;CancellationToken token=default;
        var completion=new TaskCompletionSource<ObjectExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();
        ((ReadOperations)(object)operations).Read=(_,args)=>{token=(CancellationToken)args[1]!;return completion.Task;};
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestResults? view=null;
            var rendered=await renderer.RenderComponentAsync<TestResults>(Parameters(value=>view=value,_=>callbacks++));
            view!.Mode(ObjectExecutionMeasurementMode.Summary);var executing=view.Execute();
            if(dispose){view.Dispose();view.Dispose();}else{await view.SetParametersAsync(Parameters(value=>view=value,_=>callbacks++,"AnotherDB"));}
            Assert.True(token.IsCancellationRequested);completion.SetResult(Result());await executing;view.Refresh();
            Assert.Equal(0,callbacks);Assert.DoesNotContain("軽量実測表示",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
        });
    }

    private static ObjectExecutionResult Result()=>new(1,true,null,[JsonSerializer.SerializeToElement(new{Id=1})],[new("Id","int")],true){Statistics=new(Guid.NewGuid(),"measurements","ItemList",1,new('A',64),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(12),TimeSpan.FromMilliseconds(12),TimeSpan.FromMilliseconds(3),TimeSpan.FromMilliseconds(5),2,1,true)};
    private static ServiceProvider Provider(IManagementOperations operations)=>new ServiceCollection().AddLogging().AddSingleton(operations).BuildServiceProvider();
    private static ParameterView Parameters(Action<TestResults> capture,Action<ObjectExecutionStatistics> measured,string database="Measurements")=>ParameterView.FromDictionary(new Dictionary<string,object?>{
        {"Capture",capture},{"DB名",database},{"公開版",1L},{"定義",new QueryDesign("ItemList","",DatabaseQueryKind.View,[],[],"","")},{"実測完了時",EventCallback.Factory.Create(measured,measured)}
    });
    public sealed class TestResults:ResultsView{
        [Parameter]public Action<TestResults> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Execute()=>(Task)typeof(ResultsView).GetMethod("実行時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,null)!;
        public void Mode(ObjectExecutionMeasurementMode value)=>typeof(ResultsView).GetField("計測モード",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,value);
        public void Show(bool value){typeof(ResultsView).GetField("統計表示か",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,value);this.StateHasChanged();}
        public void Refresh()=>this.StateHasChanged();
    }
    public class ReadOperations:DispatchProxy{
        public Func<string,object?[],object?> Read=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>this.Read(method!.Name,args!);
    }
    private sealed class PublishedQueries:IDisposable{
        private readonly ManagementStore store;
        public global::Seamless.Management.Server.ManagementService Service{get;private set;}=null!;
        public UserAccount Owner{get;}=new("admin","",[new("*",ManagementPermission.All)]);
        public PublishedQueries(){
            var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"measurements-"+Guid.NewGuid().ToString("N"));
            this.store=ManagementStore.Initialize(root,"admin","TestPassword-123");
            try{
                this.Service=new(this.store,null,startAuditWorker:false);
                var definition=Designer.Apply(Designer.Empty("measurements","Measurements"),new("Items","",[new("Id","int",true),new("Value","decimal",false)],[]));
                definition=Designer.ApplyQuery(definition,new("ItemList","",DatabaseQueryKind.View,[new("Id","int",false),new("Value","decimal",false)],[],"","return q.Items.Select(row=>new ItemList(row.Id,row.Value));"));
                definition=Designer.ApplyQuery(definition,new("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"decimal","return q.Items.Sum(row=>row.Value);"));
                definition=Designer.ApplyQuery(definition,new("Find","",DatabaseQueryKind.TableFunction,[new("Id","int",false)],[new("minimum","decimal")],"ISet<FindResult>","return q.Items.Where(row=>row.Value>=minimum).Select(row=>new FindResult(row.Id));"));
                definition=Designer.ApplyQuery(definition,new("Fail","",DatabaseQueryKind.ScalarFunction,[],[],"int","throw new System.InvalidOperationException(\"measurement failure\");"));
                definition=Designer.ApplyQuery(definition,new("DisposeFailure","",DatabaseQueryKind.TableFunction,[new("Id","int",false)],[],"ISet<DisposeFailureResult>","return q.Items.Select(row=>row.Id==2?throw new System.InvalidOperationException(\"iterator disposed\"):new DisposeFailureResult(row.Id));"));
                NativeWorkflow.Publish(this.Service,this.Owner,definition);
                this.Service.CommitRowsAsync(this.Owner,new("Measurements",1,Guid.NewGuid(),[new("Items",TableRowChangeKind.Insert,Values:JsonSerializer.SerializeToElement(new{Id=1,Value=10m})),new("Items",TableRowChangeKind.Insert,Values:JsonSerializer.SerializeToElement(new{Id=2,Value=20m}))])).GetAwaiter().GetResult();
            }catch{this.Service?.Dispose();this.store.Dispose();throw;}
        }
        public Task<ObjectExecutionResult> Execute(ObjectExecutionRequest request,CancellationToken cancellation=default)=>this.Service.ExecuteObjectAsync(this.Owner,request,cancellation);
        public void HoldGate(Action action){
            var runtime=typeof(global::Seamless.Management.Server.ManagementService).GetField("runtime",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this.Service)!;
            var method=runtime.GetType().GetMethod("WithDatabaseIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(int));
            method.Invoke(runtime,["measurements",CancellationToken.None,(Func<int>)(()=>{action();return 0;})]);
        }
        public void Dispose(){this.Service.Dispose();this.store.Dispose();}
    }
}
