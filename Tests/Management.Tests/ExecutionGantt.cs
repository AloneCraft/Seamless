using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Components;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class ExecutionGantt{
    [Fact]public void ResourcePreparationIsOutsideTheActualGateWaitInterval(){
        var data=Create();var stats=Statistics(data) with{GateWaitStartedOffset=TimeSpan.FromMilliseconds(3),TimeToFirstOutput=TimeSpan.FromMilliseconds(6)};
        var projected=実行時間モデル.実測を投影(data,stats);
        var gate=Assert.Single(projected.Intervals,interval=>interval.Scope=="DatabaseGateWait");
        Assert.Equal(TimeSpan.FromMilliseconds(3).Ticks,gate.StartTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(5).Ticks,gate.EndTicks);
        Assert.Equal(gate.EndTicks,Assert.Single(projected.Events,point=>point.Kind=="GateAcquired").AtTicks);
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{GateWaitStartedOffset=TimeSpan.FromTicks(-1)}));
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{GateWaitStartedOffset=TimeSpan.FromMilliseconds(7)}));
    }
    [Fact]public void LegacySummaryKeepsItsOwnClockAndInclusiveIntervals(){
        var data=Create();var stats=Statistics(data);
        var projected=実行時間モデル.実測を投影(data,stats);
        var clock=Assert.Single(projected.Clocks);
        Assert.Equal("execution-relative:"+stats.ExecutionId.ToString("N"),clock.Id);
        Assert.Equal("ExecutionRelativeDerived",clock.Origin);Assert.Equal(TimeSpan.TicksPerSecond,clock.TicksPerSecond);
        Assert.Equal(new[]{"QueryInclusive","DatabaseGateWait"},projected.Intervals.Select(interval=>interval.Scope));
        Assert.All(projected.Intervals,interval=>Assert.Null(interval.Node));
        Assert.Equal(new[]{"Started","GateAcquired","FirstOutput","Completed"},projected.Events.Select(item=>item.Kind));
        var layout=Assert.Single(実行時間モデル.作成(projected));
        Assert.Equal(TimeSpan.FromMilliseconds(8).Ticks,layout.経過Ticks);Assert.Equal(System.Numerics.BigInteger.Zero,layout.タスク合計Ticks);
        Assert.Equal(760,layout.区間[0].幅);Assert.Equal(190,layout.区間[1].幅);
    }

    [Fact]public void RealParallelFixtureShowsEightElapsedAndTwelveTaskMillisecondsWithoutCpuInference(){
        var data=Create();var id=data.Identity.ExecutionId!.Value;
        data=data with{Clocks=[new("clock",id,1000,"MonotonicHostSession")],Intervals=[
            new("scanA",id,"A",new(data.Identity.PlanKey,"n"),"clock","worker1","Worker",0,4,"TaskExecution"),
            new("scanB",id,"B",new(data.Identity.PlanKey,"n"),"clock","worker2","Worker",0,6,"TaskExecution"),
            new("join",id,"J",new(data.Identity.PlanKey,"n"),"clock","worker1","Worker",6,8,"TaskExecution")
        ]};
        var layout=Assert.Single(実行時間モデル.作成(data));
        Assert.Equal(8,layout.経過Ticks);Assert.Equal(new System.Numerics.BigInteger(12),layout.タスク合計Ticks);
        Assert.Equal(2,layout.レーン.Length);Assert.Equal(layout.区間[0].X,layout.区間[1].X);
        Assert.True(layout.区間[2].X>=layout.区間[1].X+layout.区間[1].幅);
    }

    [Fact]public void IndependentClockOriginsNeverShareASingleTimelineAxis(){
        var data=Create();var id=data.Identity.ExecutionId!.Value;
        data=data with{Clocks=[new("first",id,1000,"FirstOrigin"),new("second",id,1000000,"SecondOrigin")],Intervals=[
            new("a",id,"a",null,"first","lane","Query",0,8,"QueryInclusive"),
            new("b",id,"b",null,"second","lane","Query",1000000,1008000,"QueryInclusive")
        ]};
        var layouts=実行時間モデル.作成(data);Assert.Equal(2,layouts.Length);
        Assert.Equal(8,layouts[0].経過Ticks);Assert.Equal(8000,layouts[1].経過Ticks);
        Assert.Equal(実行時間モデル.左端,layouts[0].区間[0].X);Assert.Equal(実行時間モデル.左端,layouts[1].区間[0].X);
    }

    [Fact]public void ZeroDurationRemainsAnObservedMarkerAndUnknownHasNoInventedInterval(){
        var data=Create();Assert.Empty(実行時間モデル.作成(data));
        var projected=実行時間モデル.実測を投影(data,Statistics(data) with{Elapsed=TimeSpan.Zero,DatabaseGateWait=TimeSpan.Zero,TimeToFirstOutput=null});
        var layout=Assert.Single(実行時間モデル.作成(projected));
        Assert.Equal(0,layout.経過Ticks);Assert.All(layout.区間,interval=>Assert.Equal(0,interval.幅));Assert.Equal(3,layout.イベント.Length);
        Assert.All(layout.区間,interval=>Assert.True(double.IsFinite(interval.X)));
    }

    [Theory][InlineData("Failed")][InlineData("Cancelled")]
    public void FailedOrCancelledGateWaitNeverInventsAGateAcquisition(string outcome){
        var data=Create();var stats=Statistics(data) with{DatabaseGateWait=TimeSpan.FromMilliseconds(8),TimeToFirstOutput=null,IsPartial=true};
        var observation=new QueryExecutionObservation(stats.ExecutionId,stats.DefinitionId,stats.ObjectId,stats.Version,stats.ArtifactHash,true,stats.StartedAtUtc,stats.CompletedAtUtc,stats.Elapsed,stats.DatabaseGateWait,null,0,0,true,outcome,"DatabaseGate",outcome);
        var projected=実行時間モデル.実測を投影(data,stats,observation);
        Assert.DoesNotContain(projected.Events,item=>item.Kind=="GateAcquired");
        Assert.Equal(outcome,projected.Intervals[0].Outcome);Assert.False(projected.Intervals[0].Complete);
    }

    [Fact]public void OtherTargetOrVersionCannotReuseTheSummary(){
        var data=Create();var stats=Statistics(data);
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{Version=2}));
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{ObjectId="Other"}));
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{ExecutionId=Guid.NewGuid()}));
        Assert.Throws<InvalidDataException>(()=>実行時間モデル.実測を投影(data,stats with{Elapsed=TimeSpan.FromMilliseconds(1)}));
    }

    [Fact]public void EstimatedIntervalsAreNotPlacedAsObservedExecution(){
        var data=Create();var id=data.Identity.ExecutionId!.Value;
        data=data with{Clocks=[new("clock",id,1000,"Estimated")],Intervals=[new("a",id,"a",null,"clock","lane","Query",0,10,"QueryInclusive","Estimated")]};
        Assert.Empty(実行時間モデル.作成(data));
    }

    [Fact]public async Task SummaryRendersWholeQueryAndMissingNodeNoticeWithoutCpuOrWorkerValues(){
        var data=Create();data=実行時間モデル.実測を投影(data,Statistics(data) with{IsPartial=true});
        using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<実行Gantt>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"データ",data},{"選択ノード",new PlanNodeRef(data.Identity.PlanKey,"n")}}));
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("問い合わせ全体",html);Assert.Contains("DBゲート待機",html);Assert.Contains("選択ノードに対応する実行区間は未収集です。",html);
        Assert.Contains("ガント部分",html);Assert.Contains("data-イベント=\"FirstOutput\"",html);
        Assert.DoesNotContain("独立タスク区間の合計",html);Assert.DoesNotContain("worker1",html);
    }

    [Fact]public async Task OnlyIntervalsWithAnExplicitPlanReferenceRaiseLinkedSelection(){
        var data=Create();var id=data.Identity.ExecutionId!.Value;var node=new PlanNodeRef(data.Identity.PlanKey,"n");
        data=data with{Clocks=[new("clock",id,1000,"Monotonic")],Intervals=[new("task",id,"task",node,"clock","worker","Worker",0,8,"TaskExecution")]};
        var selected=new List<PlanNodeRef?>();
        using var provider=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestGantt? view=null;
            var rendered=await renderer.RenderComponentAsync<TestGantt>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"データ",data},{"選択ノード",node},{"検索","Query"},{"Capture",(Action<TestGantt>)(value=>view=value)},
                {"選択時",EventCallback.Factory.Create<PlanNodeRef?>(selected,(PlanNodeRef? value)=>selected.Add(value))}
            }));
            await view!.Select(null);Assert.Empty(selected);
            await view.Select(node);Assert.Equal(node,Assert.Single(selected));
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());
            Assert.Contains("data-選択=\"true\"",html);Assert.Contains("ガント検索一致",html);Assert.Contains("role=\"button\"",html);
        });
    }

    public sealed class TestGantt:実行Gantt{
        [Parameter]public Action<TestGantt> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Select(PlanNodeRef? node)=>(Task)typeof(実行Gantt).GetMethod("選ぶ時Async",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(this,[node])!;
    }

    internal static QueryVisualizationData Create(){
        var plan=new PlanDocument(2,"Query","test",new('B',64),new('C',64),[new("n","SourceOperator","Query",[],null,null,0,0,[])],JsonSerializer.SerializeToElement<object?>(null));
        var key=QueryVisualizationData.PlanKey(plan);
        return new(new("definition","Query",1,new('A',64),plan.SourceHash,key,Guid.NewGuid()),plan,[],[],[],[],[],[],[]);
    }
    private static ObjectExecutionStatistics Statistics(QueryVisualizationData data)=>new(data.Identity.ExecutionId!.Value,data.Identity.DefinitionId,data.Identity.ObjectId,data.Identity.Version,data.Identity.ArtifactHash,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(8),TimeSpan.FromMilliseconds(8),TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(3),7,7,false);
}
