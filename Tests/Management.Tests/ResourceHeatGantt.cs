using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ResourceHeatGantt{
    [Fact]public void ZeroAndUnavailableStatesHaveDifferentValuesAndValidation(){
        var trace=Trace();trace.Validate();Assert.Equal(0,trace.Samples[0].Value);Assert.Equal(0,trace.Samples[0].Percent);
        Assert.Null(trace.Samples[1].Value);Assert.Equal("Missing",trace.Samples[1].State);
        Assert.Throws<InvalidDataException>(()=>(trace with{Samples=[trace.Samples[1] with{Value=0}]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(trace with{Samples=[trace.Samples[0] with{Percent=double.NaN}]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(trace with{Samples=[trace.Samples[0] with{Value=double.PositiveInfinity}]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(trace with{Lanes=[trace.Lanes[0] with{PercentBasis=null}],Samples=[trace.Samples[0]]}).Validate());
        (trace with{Lanes=[trace.Lanes[0] with{Label=new('x',4096)}],Samples=[]}).Validate();
        Assert.Throws<InvalidDataException>(()=>(trace with{Lanes=[trace.Lanes[0] with{Label=new('x',4097)}],Samples=[]}).Validate());
    }

    [Fact]public void DynamicScopesNeverAllocateHostUsageToQueryOrNode(){
        var trace=Trace();var host=資源ヒート配置.作成(trace,null,"Host");var process=資源ヒート配置.作成(trace,null,"Process");
        Assert.Equal("logical-0",Assert.Single(host.レーン).値.Id);Assert.Equal(2,host.サンプル.Length);
        Assert.Equal("process-memory",Assert.Single(process.レーン).値.Id);Assert.Single(process.サンプル);
        Assert.Empty(資源ヒート配置.作成(trace,null,"Query").サンプル);Assert.Empty(資源ヒート配置.作成(trace,null,"Node").レーン);
        Assert.DoesNotContain(host.レーン,lane=>lane.値.Kind=="NUMA");Assert.DoesNotContain(host.レーン,lane=>lane.値.Kind=="PhysicalCore");
    }

    [Fact]public void ExplicitQueryStartAnchorAlignsSummaryIntervalsToTheOsClock(){
        var data=ExecutionGantt.Create();var trace=Trace(data.Identity.ExecutionId!.Value);
        var stats=new ObjectExecutionStatistics(trace.ExecutionId,"definition","Query",1,new('A',64),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(8),TimeSpan.FromMilliseconds(8),TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(3),7,7,false);
        data=実行時間モデル.実測を投影(data,stats);
        var layout=資源ヒート配置.作成(trace,data);
        Assert.Equal(2,layout.実行区間.Length);Assert.Equal(0,layout.非対応時計区間数);
        Assert.Equal(layout.位置(1000),layout.実行区間[0].X);Assert.Equal(layout.位置(1008)-layout.位置(1000),layout.実行区間[0].幅,6);
        var unknown=資源ヒート配置.作成(trace with{Clock=trace.Clock with{AnchorKind="HostStart"}},data);
        Assert.Empty(unknown.実行区間);Assert.Equal(2,unknown.非対応時計区間数);
    }

    [Fact]public void InstantSamplesRemainPointsAndWindowsKeepTheirActualWidth(){
        var trace=Trace();var process=資源ヒート配置.作成(trace,null,"Process");Assert.Equal(0,Assert.Single(process.サンプル).幅);
        var host=資源ヒート配置.作成(trace,null,"Host");Assert.All(host.サンプル,sample=>Assert.Equal(500,sample.幅));
        Assert.Equal("Instant",process.サンプル[0].値.Aggregation);
    }

    [Fact]public void DifferentExecutionAndInvalidTopologyCannotBeJoined(){
        var trace=Trace();var data=ExecutionGantt.Create();
        Assert.Throws<InvalidDataException>(()=>資源ヒート配置.作成(trace,data));
        Assert.Throws<InvalidDataException>(()=>(trace with{Lanes=[trace.Lanes[0] with{ParentId="logical-0"}],Samples=[]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(trace with{Samples=[trace.Samples[0] with{Aggregation="Instant"}]}).Validate());
    }

    [Fact]public void PercentOverOneHundredKeepsItsValueWhileTheColorScaleIsBounded(){
        var trace=Trace() with{Samples=[Trace().Samples[0] with{Percent=150}]};trace.Validate();Assert.Equal(150,trace.Samples[0].Percent);
        foreach(var palette in new[]{"Continuous","Steps","Mono","Accessible"}){Assert.Equal(資源ヒート配置.色(100,palette),資源ヒート配置.色(150,palette));}
        Assert.NotEqual(資源ヒート配置.色(0,"Continuous"),資源ヒート配置.色(100,"Continuous"));
    }

    [Fact]public void CollectionOptionsAreExplicitAndBounded(){
        var options=new ResourceHeatOptions();Assert.Equal(TimeSpan.FromMilliseconds(100),options.EffectiveSampleInterval);options.Validate();
        new ResourceHeatOptions(MaximumSamples:65536).Validate();
        Assert.Throws<ManagementException>(()=>new ResourceHeatOptions(TimeSpan.FromMilliseconds(1)).Validate());
        Assert.Throws<ManagementException>(()=>new ResourceHeatOptions(MaximumSamples:65537).Validate());
        var trace=Trace();var maximum=trace with{Samples=Enumerable.Repeat(trace.Samples[0],65536).ToArray()};maximum.Validate();
        var layout=資源ヒート配置.作成(maximum,null);Assert.Equal(8192,layout.サンプル.Length);Assert.Equal(65536-8192,layout.表示省略数);
        Assert.Throws<InvalidDataException>(()=>(trace with{Samples=Enumerable.Repeat(trace.Samples[0],65537).ToArray()}).Validate());
    }

    [Fact]public async Task RenderingDistinguishesMissingFromZeroAndExplainsUnattributedNodeSelection(){
        var trace=Trace();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<資源ヒートガント>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"記録",trace},{"選択ノード",new PlanNodeRef(new('B',64),"filter")}}));
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("data-状態=\"Available\"",html);Assert.Contains("data-状態=\"Missing\"",html);Assert.Contains("CounterUnavailable",html);
        Assert.Contains("選択ノードへ帰属する資源サンプルは未収集です。",html);Assert.Contains("OS accounting",html);Assert.DoesNotContain("NUMA 0",html);
    }

    private static ResourceHeatTrace Trace(Guid? execution=null){
        var id=execution??Guid.NewGuid();
        return new(id,"definition","Query",1,new('A',64),true,new("os-clock",1000,1000,DateTimeOffset.UnixEpoch),[
            new("logical-0","Logical CPU 0","CPU","Host","CpuBusy","Percent",PhysicalResourceId:"cpu0",PercentBasis:new("One logical processor",1,"LogicalProcessor","OS accounting")),
            new("process-memory","Process working set","Memory","Process","WorkingSet","Bytes")
        ],[
            new("logical-0",1000,1100,0,0),new("logical-0",1100,1200,null,null,"Missing","Unknown",MissingReason:"CounterUnavailable"),
            new("process-memory",1050,1050,1024,null,Aggregation:"Instant")
        ],TimeSpan.FromMilliseconds(100),0,[]);
    }
}
