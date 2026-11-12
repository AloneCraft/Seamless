using System.Reflection;
using System.Net;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using TraceView=Seamless.Management.Components.クエリトレース;
using TraceOptionsView=Seamless.Management.Components.トレース設定;
using ResultView=Seamless.Management.Components.問い合わせ結果;

namespace Management.Tests;

public sealed class QueryTracePresentation{
    [Fact]public async Task AggregatedExplanationsHighlightAndCycleEveryCorrespondingRecord(){
        var id=Guid.NewGuid();var frames=Enumerable.Range(1,8).Select(sequence=>Page(id,QueryTraceRecordMode.TimeSampled).Frames[0] with{Sequence=sequence,Elapsed=TimeSpan.Zero}).ToArray();
        var operations=Operations((_,_)=>Task.FromResult(Page(id,QueryTraceRecordMode.TimeSampled) with{Frames=frames}));
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;var root=await renderer.RenderComponentAsync<TestTrace>(Parameters(value=>view=value));
            var html=WebUtility.HtmlDecode(root.ToHtmlString());Assert.Contains("data-説明観測数=\"5\"",html);
            Assert.Equal(8,html.Split("data-トレース順=",StringSplitOptions.None).Length-1);Assert.Contains("width=\"208\"",html);
            view!.SelectLastExplanation();Assert.Equal(4,view.FocusSequence);view.SelectLastExplanation();Assert.Equal(5,view.FocusSequence);
            html=WebUtility.HtmlDecode(root.ToHtmlString());Assert.Equal(5,html.Split("data-対応観測=\"true\"",StringSplitOptions.None).Length-1);
            Assert.Contains("説明に対応する5観測を一覧で強調しています。",html);Assert.Contains("aria-controls=",html);
            Assert.Contains("観測範囲はクエリ全体の境界とサンプルです。",html);
            Assert.Contains("今回起動の収集状況",html);Assert.Contains("表示中の記録8件",html);
        });
    }
    [Fact]public async Task SettingsApplyOnlyToTheNextExecutionAndEachExecutionResetsTheTraceIdentity(){
        var requests=new List<ObjectExecutionRequest>();var notifications=new List<Guid?>();var id=Guid.NewGuid();
        var operations=Operations((name,args)=>{
            Assert.Equal(nameof(IManagementOperations.ExecuteObjectAsync),name);var request=(ObjectExecutionRequest)args[0]!;requests.Add(request);
            return Task.FromResult(new ObjectExecutionResult(1,false,JsonSerializer.SerializeToElement(7),[],[],false){TraceExecutionId=request.TraceOptions is null?null:id});
        });
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestResults? view=null;
            await renderer.RenderComponentAsync<TestResults>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<TestResults>)(value=>view=value)},{"DB名","DB"},{"公開版",1L},{"定義",new QueryDesign("Amount","",DatabaseQueryKind.ScalarFunction,[],[],"int","")},
                {"トレース実行時",EventCallback.Factory.Create<Guid?>(notifications,(Guid? value)=>notifications.Add(value))}
            }));
            var settings=new TraceOptionsView();
            typeof(TraceOptionsView).GetProperty("値変更時")!.SetValue(settings,EventCallback.Factory.Create<QueryTraceOptions?>(new object(),(QueryTraceOptions? value)=>view!.SetTrace(value)));
            await Change(settings,"方式変更時Async","BoundaryEvents");await Change(settings,"完全性変更時Async","Complete");Assert.Empty(requests);
            await view!.Execute();Assert.Equal(QueryTraceReliability.Complete,Assert.Single(requests).TraceOptions!.Reliability);Assert.Equal(new Guid?[]{null,id},notifications);
            await Change(settings,"方式変更時Async","TimeSampled");Assert.Single(requests);
            await view.Execute();Assert.Equal(QueryTraceReliability.BestEffort,requests[1].TraceOptions!.Reliability);
            await Change(settings,"方式変更時Async","None");Assert.Equal(2,requests.Count);
            await view.Execute();Assert.Null(requests[2].TraceOptions);Assert.Equal(new Guid?[]{null,id,null,id,null,null},notifications);
        });
    }

    [Theory]
    [InlineData(QueryTraceRecordMode.TimeSampled,false)][InlineData(QueryTraceRecordMode.TimeSampled,true)]
    [InlineData(QueryTraceRecordMode.BoundaryEvents,false)][InlineData(QueryTraceRecordMode.BoundaryEvents,true)]
    public async Task RecordingAndRefreshModesAreIndependentAndCoordinateChangesDoNotExecute(QueryTraceRecordMode mode,bool events){
        var id=Guid.NewGuid();var reads=0;var watches=0;var unexpected=0;
        var operations=Operations((name,args)=>name switch{
            nameof(IManagementOperations.QueryTraceAsync)=>Read(),
            nameof(IManagementOperations.WatchQueryTraceAsync)=>Wait((CancellationToken)args[1]!),
            _=>Unexpected(name)
        });
        Task<QueryTracePage> Read(){reads++;return Task.FromResult(Page(id,mode));}
        Task<QueryTraceWatchResult> Wait(CancellationToken cancellation){watches++;return new TaskCompletionSource<QueryTraceWatchResult>().Task.WaitAsync(cancellation);}
        object Unexpected(string name){unexpected++;throw new InvalidOperationException("Display must not execute: "+name);}
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;var rendered=await renderer.RenderComponentAsync<TestTrace>(Parameters(value=>view=value));
            await view!.RefreshMode(events);view.Coordinates(true);view.Coordinates(false);
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.True(reads>=1);Assert.Equal(events,watches>0);
            Assert.Contains("1970-01-01 00:00:00.010000 UTC",html);Assert.Contains("<span>v3</span>",html);Assert.DoesNotContain("v@",html);
            Assert.Equal(2,html.Split("data-トレース順=",StringSplitOptions.None).Length-1);
            Assert.Contains(mode==QueryTraceRecordMode.TimeSampled?"時間サンプリング":"境界イベント",html);
            Assert.Contains("ノード別時間、CPU、I/O、詳細イベントは未収集です。",html);
            Assert.Contains("data-トレース範囲=\"QueryBoundariesAndSamples\"",html);
            Assert.Contains("観測範囲はクエリ全体の境界とサンプルです。",html);
            Assert.Equal(0,unexpected);
            await view.DisposeAsync();
        });
    }

    [Fact]public async Task RestartAndDisposeReclaimTheUiLoopWhenTheWatchRequestIgnoresCancellation(){
        var request=new TaskCompletionSource<QueryTraceWatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken watchCancellation=default;
        var operations=Operations((name,args)=>{
            if(name==nameof(IManagementOperations.QueryTraceAsync)){return Task.FromResult(Page(Guid.NewGuid(),QueryTraceRecordMode.BoundaryEvents));}
            Assert.Equal(nameof(IManagementOperations.WatchQueryTraceAsync),name);watchCancellation=(CancellationToken)args[1]!;return request.Task;
        });
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;await renderer.RenderComponentAsync<TestTrace>(Parameters(value=>view=value));
            try{
                await view!.RefreshMode(true);var previous=view.Loop;
                Assert.False(previous.IsCompleted);Assert.False(request.Task.IsCompleted);
                await view.RefreshMode(false).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(watchCancellation.IsCancellationRequested);Assert.True(previous.IsCompletedSuccessfully);Assert.False(request.Task.IsCompleted);
                var current=view.Loop;await view.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));Assert.True(current.IsCompletedSuccessfully);
            }finally{request.TrySetResult(new(Guid.NewGuid(),1,true));}
        });
    }

    [Theory][InlineData("Permission")][InlineData("Authentication")][InlineData("Connection")]
    public async Task TraceRefreshClearsSensitiveResultsOnAccessLoss(string code){
        var id=Guid.NewGuid();Task<QueryTracePage> response=Task.FromResult(Page(id,QueryTraceRecordMode.BoundaryEvents));
        var operations=Operations((_,_)=>response);
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;var rendered=await renderer.RenderComponentAsync<TestTrace>(Parameters(value=>view=value));
            Assert.Contains(id.ToString(),rendered.ToHtmlString());
            response=Task.FromException<QueryTracePage>(new ManagementException(code,"trace-access-"+code));await view!.再取得Async();view.Refresh();
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("trace-access-"+code,html);
            if(code=="Connection"){Assert.Contains(id.ToString(),html);}else{Assert.DoesNotContain(id.ToString(),html);}
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task TargetChangesAndDisposalCancelAndIgnoreLateTracePages(bool dispose){
        var id=Guid.NewGuid();var late=Guid.NewGuid();CancellationToken cancellation=default;
        Task<QueryTracePage> response=Task.FromResult(Page(id,QueryTraceRecordMode.TimeSampled));
        var operations=Operations((_,args)=>{cancellation=(CancellationToken)args[1]!;return response;});
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestTrace? view=null;var rendered=await renderer.RenderComponentAsync<TestTrace>(Parameters(value=>view=value));
            var pending=new TaskCompletionSource<QueryTracePage>(TaskCreationOptions.RunContinuationsAsynchronously);response=pending.Task;var reading=view!.再取得Async();var oldCancellation=cancellation;
            if(dispose){await view.DisposeAsync();}else{response=Task.FromResult(Page(id,QueryTraceRecordMode.TimeSampled));await view.SetParametersAsync(Parameters(value=>view=value,"other"));}
            Assert.True(oldCancellation.IsCancellationRequested);pending.SetResult(Page(late,QueryTraceRecordMode.TimeSampled));await reading;view.Refresh();
            Assert.DoesNotContain(late.ToString(),rendered.ToHtmlString());
        });
    }

    [Fact]public async Task EnglishTraceControlsAndEmptyStateUseResources(){
        var previous=CultureInfo.CurrentUICulture;
        try{
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("en");
            var operations=Operations((_,_)=>Task.FromResult(Page(Guid.NewGuid(),QueryTraceRecordMode.TimeSampled) with{Frames=[]}));
            using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
            await renderer.Dispatcher.InvokeAsync(async()=>{
                var trace=await renderer.RenderComponentAsync<TestTrace>(Parameters(_=>{}));
                var html=WebUtility.HtmlDecode(trace.ToHtmlString());Assert.Contains("Trace display refresh mode",html);Assert.Contains("Timeline coordinates",html);Assert.Contains("This target has no trace records.",html);Assert.DoesNotContain("クエリ境界",html);
                var settings=await renderer.RenderComponentAsync<TraceOptionsView>();Assert.Contains("Query boundary recording",WebUtility.HtmlDecode(settings.ToHtmlString()));
            });
        }finally{CultureInfo.CurrentUICulture=previous;}
    }

    private static Task Change(object target,string name,string value)=>(Task)(target is TraceView?typeof(TraceView):target.GetType()).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,[new ChangeEventArgs{Value=value}])!;
    private static IManagementOperations Operations(Func<string,object?[],object?> read){var operations=DispatchProxy.Create<IManagementOperations,TraceOperations>();((TraceOperations)(object)operations).Read=read;return operations;}
    private static ServiceProvider Provider(IManagementOperations operations)=>new ServiceCollection().AddLogging().AddSingleton(operations).BuildServiceProvider();
    private static ParameterView Parameters(Action<TestTrace> capture,string target="Value")=>ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",capture},{"DB定義ID","definition"},{"対象ID",target}});
    private static QueryTracePage Page(Guid id,QueryTraceRecordMode mode)=>new([
        new(id,1,"definition","Value",3,new('A',64),true,DateTimeOffset.UnixEpoch,TimeSpan.FromMilliseconds(10),"Started","DatabaseGate",0,null,mode,QueryTraceReliability.BestEffort),
        new(id,2,"definition","Value",3,new('A',64),true,DateTimeOffset.UnixEpoch,TimeSpan.FromMilliseconds(20),"Completed","Execution",1,"Succeeded",mode,QueryTraceReliability.BestEffort)
    ],new(Guid.NewGuid(),2,2,0,0,0,0,0,0,null,false),false,0,false);
    public class TraceOperations:DispatchProxy{
        public Func<string,object?[],object?> Read=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>this.Read(method!.Name,args!);
    }
    public sealed class TestTrace:TraceView{
        [Parameter]public Action<TestTrace> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task RefreshMode(bool events)=>Change(this,"更新方式変更時Async",events.ToString());
        public void Coordinates(bool ordered){typeof(TraceView).GetMethod("座標変更時",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[new ChangeEventArgs{Value=ordered.ToString()}]);this.StateHasChanged();}
        public void Refresh()=>this.StateHasChanged();
        public Task Loop=>(Task)typeof(TraceView).GetField("更新Task",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this)!;
        public long? FocusSequence{
            get{if(typeof(TraceView).GetField("フォーカス対象",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this) is ValueTuple<Guid,long> value){return value.Item2;}return null;}
        }
        public void SelectLastExplanation(){
            var layout=(Seamless.Management.Components.トレース配置)typeof(TraceView).GetField("配置",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this)!;
            typeof(TraceView).GetMethod("説明を選ぶ",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[layout.説明[^1]]);this.StateHasChanged();
        }
    }
    public sealed class TestResults:ResultView{
        [Parameter]public Action<TestResults> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Execute()=>(Task)typeof(ResultView).GetMethod("実行時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,null)!;
        public void SetTrace(QueryTraceOptions? options)=>typeof(ResultView).GetField("トレース設定値",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,options);
    }
}
