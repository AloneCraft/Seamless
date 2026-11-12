using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class QueryVisualizationIntegration{
    [Fact]public async Task FiveDisplaysAreExclusiveAndKeepSharedSelectionSearchAndStructureIndependent(){
        var fixture=new Fixture();using var provider=Provider(fixture.Operations);
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;var rendered=await renderer.RenderComponentAsync<TestPlan>(fixture.Parameters(value=>view=value));
            await view!.Select(new(QueryVisualizationData.PlanKey(fixture.Plan),"filter"));view.Search("Where");
            foreach(var mode in new[]{"Sankey","Gantt","Tree","DOT","DAG"}){
                view.Mode(mode);view.Refresh();var html=WebUtility.HtmlDecode(rendered.ToHtmlString());
                Assert.Single(Regex.Matches(html,"data-表示方式=\"[^\"]+\" data-選択中=\"true\""));
                Assert.Contains("data-表示方式=\""+mode+"\" data-選択中=\"true\"",html);
                Assert.Equal("filter",view.Selected!.Id);Assert.Equal("Where",view.Query);Assert.Equal("Relational",view.Structure);
                if(mode=="Gantt"){Assert.DoesNotContain("この表示のSVG・HTML出力は未対応です。",html);}
            }
            Assert.Equal(1,fixture.PlanCalls);Assert.Equal(1,fixture.TraceCalls);Assert.Equal(0,fixture.OtherCalls);
        });
    }

    [Theory][InlineData("Explicit")][InlineData("History")][InlineData("Latest")]
    public async Task ExecutionIdentityPriorityNeverMixesDifferentRuns(string priority){
        var fixture=new Fixture();var latest=fixture.Statistics(Guid.NewGuid(),9);var history=fixture.Observation(fixture.Statistics(Guid.NewGuid(),4));var explicitId=Guid.NewGuid();
        var parameters=fixture.ParameterValues();parameters["実測"]=latest;
        if(priority!="Latest"){parameters["実績記録"]=history;}
        if(priority=="Explicit"){parameters["トレース実行ID"]=explicitId;}
        using var provider=Provider(fixture.Operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;parameters["Capture"]=(Action<TestPlan>)(value=>view=value);
            var rendered=await renderer.RenderComponentAsync<TestPlan>(ParameterView.FromDictionary(parameters));view!.Mode("Gantt");view.Refresh();
            var model=view.Data!;
            if(priority=="Explicit"){
                Assert.Equal(explicitId,model.Identity.ExecutionId);Assert.Empty(model.Intervals);Assert.Null(view.Statistics);Assert.Null(view.Observation);
                Assert.Contains("選択した実行IDに一致する全体実測はありません。",WebUtility.HtmlDecode(rendered.ToHtmlString()));
            }else if(priority=="History"){
                Assert.Equal(history.ExecutionId,model.Identity.ExecutionId);Assert.Equal(history.ExecutionId,view.Statistics!.ExecutionId);Assert.Equal(history.ExecutionId,view.Observation!.ExecutionId);Assert.Equal(TimeSpan.FromMilliseconds(4).Ticks,model.Intervals[0].EndTicks);
            }else{Assert.Equal(latest.ExecutionId,model.Identity.ExecutionId);Assert.Equal(TimeSpan.FromMilliseconds(9).Ticks,model.Intervals[0].EndTicks);}
            Assert.Equal(model.Identity.ExecutionId,Assert.Single(fixture.TraceQueries).ExecutionId);
        });
    }

    [Fact]public async Task InactivePlanDoesNotReadUntilShownAndKeepsItsDisplayStateWhenHidden(){
        var fixture=new Fixture();var parameters=fixture.ParameterValues();parameters["表示中か"]=false;
        using var provider=Provider(fixture.Operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;parameters["Capture"]=(Action<TestPlan>)(value=>view=value);
            await renderer.RenderComponentAsync<TestPlan>(ParameterView.FromDictionary(parameters));Assert.Equal(0,fixture.PlanCalls);
            await view!.Visible(true);Assert.Equal(1,fixture.PlanCalls);view.Mode("Gantt");view.Search("retained");
            await view.Visible(false);Assert.Equal("Gantt",view.ModeValue);Assert.Equal("retained",view.Query);
            await view.Visible(true);Assert.Equal(1,fixture.PlanCalls);Assert.Equal("Gantt",view.ModeValue);Assert.Equal("retained",view.Query);
        });
    }

    [Fact]public async Task InactiveTraceCancelsItsWatchAndPreservesTheRecordedPage(){
        var fixture=new Fixture();var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken watchToken=default;
        var operations=DispatchProxy.Create<IManagementOperations,ManagerReadState.ReadOperations>();
        ((ManagerReadState.ReadOperations)(object)operations).InvokeRead=(name,args)=>{
            if(name==nameof(IManagementOperations.QueryTraceAsync)){return Task.FromResult(new QueryTracePage([],new(Guid.NewGuid(),7,7,0,0,0,0,0,0,null,false),false,0,false));}
            Assert.Equal(nameof(IManagementOperations.WatchQueryTraceAsync),name);watchToken=(CancellationToken)args[1]!;started.TrySetResult();return new TaskCompletionSource<QueryTraceWatchResult>().Task.WaitAsync(watchToken);
        };
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;
            var rendered=await renderer.RenderComponentAsync<TestTrace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"DB定義ID","db"},{"対象ID","Query"},{"Capture",(Action<TestTrace>)(value=>view=value)}}));
            await view!.Events();await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await view.Visible(false);Assert.True(watchToken.IsCancellationRequested);
            Assert.Contains("観測 7・保存 7・欠落 0・保存失敗 0",WebUtility.HtmlDecode(rendered.ToHtmlString()));Assert.True(view.EventMode);
        });
    }

    [Fact]public async Task UnsupportedVisualExportsDisableSvgHtmlAndCannotInvokeDownload(){
        var fixture=new Fixture();using var provider=Provider(fixture.Operations);
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestSave? view=null;
            var rendered=await renderer.RenderComponentAsync<TestSave>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"対象",fixture.Target},{"計画",fixture.Plan},{"図出力可能か",false},{"Capture",(Action<TestSave>)(value=>view=value)}}));
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Equal(2,Regex.Matches(html,"<button[^>]* disabled[^>]*>").Count);
            Assert.Contains("サンキーの数量やGanttの表示設定・任意区間は含みません。",html);
            await view!.Save("SVG");await view.Save("HTML");Assert.Null(view.Notice);
        });
    }

    public sealed class TestSave:計画保存{
        [Parameter]public Action<TestSave> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Save(string value)=>(Task)typeof(計画保存).GetMethod("保存時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[value])!;
        public string? Notice=>(string?)typeof(計画保存).GetField("案内",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this);
    }

    public sealed class TestPlan:実行計画ビュー{
        [Parameter]public Action<TestPlan> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public void Mode(string value)=>Method("表示選択時").Invoke(this,[value]);
        public string ModeValue=>Field("描画方式")!.ToString()!;
        public string Query=>(string)Field("共通検索")!;
        public string Structure=>(string)Field("構造段階")!;
        public ExecutionPlanNode? Selected=>(ExecutionPlanNode?)Field("選択ノード");
        public QueryVisualizationData? Data=>(QueryVisualizationData?)Property("共通可視化");
        public ObjectExecutionStatistics? Statistics=>(ObjectExecutionStatistics?)Property("適合実測");
        public QueryExecutionObservation? Observation=>(QueryExecutionObservation?)Property("適合観測");
        public void Search(string value)=>Method("検索を共有する時").Invoke(this,[value]);
        public Task Select(PlanNodeRef value)=>(Task)Method("共有ノード選択時Async").Invoke(this,[value])!;
        public async Task Visible(bool value){this.表示中か=value;await base.OnParametersSetAsync();this.StateHasChanged();}
        public void Refresh()=>this.StateHasChanged();
        private static MethodInfo Method(string name)=>typeof(実行計画ビュー).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!;
        private object? Field(string name)=>typeof(実行計画ビュー).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this);
        private object? Property(string name)=>typeof(実行計画ビュー).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this);
    }
    public sealed class TestTrace:クエリトレース{
        [Parameter]public Action<TestTrace> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public async Task Visible(bool value){this.表示中か=value;await base.OnParametersSetAsync();this.StateHasChanged();}
        public Task Events()=>(Task)typeof(クエリトレース).GetMethod("更新方式変更時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[new ChangeEventArgs{Value="True"}])!;
        public bool EventMode=>(bool)typeof(クエリトレース).GetField("イベント更新か",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this)!;
    }
    private sealed class Fixture{
        internal readonly PlanDocument Plan=new(2,"Query.Run()","test",new('A',64),new('B',64),[
            new("scan","SourceOperator","Scan",[],null,null,0,0,[]){Layer="Original"},new("filter","SourceOperator","Where",["scan"],null,null,0,0,[]){Layer="Original"}
        ],JsonSerializer.SerializeToElement<object?>(null)){Edges=[new("scan","filter","OriginalDataFlow","source")]};
        internal readonly QueryPlanResult Target;
        internal readonly IManagementOperations Operations;
        internal readonly List<QueryTraceQuery> TraceQueries=[];
        internal int PlanCalls,TraceCalls,OtherCalls;
        internal Fixture(){
            this.Target=new("db",1,new('C',64),new('D',64),false,new("Query",[this.Plan.MethodKey],[this.Plan]));
            this.Operations=DispatchProxy.Create<IManagementOperations,ManagerReadState.ReadOperations>();
            ((ManagerReadState.ReadOperations)(object)this.Operations).InvokeRead=(name,args)=>{
                if(name==nameof(IManagementOperations.QueryPlansAsync)){this.PlanCalls++;return Task.FromResult(this.Target);}
                if(name==nameof(IManagementOperations.QueryTraceAsync)){this.TraceCalls++;this.TraceQueries.Add((QueryTraceQuery)args[0]!);return Task.FromException<QueryTracePage>(new ManagementException("Unsupported","No trace fixture"));}
                this.OtherCalls++;throw new InvalidOperationException("Unexpected management call "+name);
            };
        }
        internal Dictionary<string,object?> ParameterValues()=>new(){{"DB名","DB"},{"定義ID","db"},{"対象",new QueryPlanRequest("db","Query",1)}};
        internal ParameterView Parameters(Action<TestPlan> capture){var values=this.ParameterValues();values["Capture"]=capture;return ParameterView.FromDictionary(values);}
        internal ObjectExecutionStatistics Statistics(Guid id,int milliseconds)=>new(id,"db","Query",1,this.Target.ArtifactHash,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(milliseconds),TimeSpan.FromMilliseconds(milliseconds),TimeSpan.FromMilliseconds(1),TimeSpan.FromMilliseconds(2),7,7,false);
        internal QueryExecutionObservation Observation(ObjectExecutionStatistics value)=>new(value.ExecutionId,value.DefinitionId,value.ObjectId,value.Version,value.ArtifactHash,true,value.StartedAtUtc,value.CompletedAtUtc,value.Elapsed,value.DatabaseGateWait,value.TimeToFirstOutput,value.ObservedOutputCount,value.ReturnedOutputCount,value.IsPartial,"Succeeded","Execution",null);
    }
    private static ServiceProvider Provider(IManagementOperations operations)=>new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
}
