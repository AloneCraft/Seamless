using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Plan=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class QueryVisualizationFlow{
    [Fact]public void QuantitiesKeepLargeAndFractionalEstimatesExactAndRejectNoncanonicalOrObservedFractions(){
        foreach(var text in new[]{"0","1","0.5","1.25",new string('9',256),"0."+new string('0',253)+"1"}){
            var quantity=new FlowQuantity(text,"Estimated","OperatorOutput",true);quantity.Validate();Assert.Equal(text,quantity.DecimalValue);
        }
        foreach(var text in new[]{"","00","01","1.0","0.0",".5","1.","1e6","-1","+1","1,000",new string('9',257),"NaN","1..1"}){
            Assert.Throws<InvalidDataException>(()=>new FlowQuantity(text,"Estimated","OperatorOutput",true).Validate());
        }
        Assert.Throws<InvalidDataException>(()=>new FlowQuantity("0.5","Observed","OperatorOutput",true).Validate());
        FlowQuantity.Unknown().Validate();new FlowQuantity("0","Observed","OperatorOutput",true).Validate();
        Assert.Throws<InvalidDataException>(()=>new FlowQuantity("0","Unknown","OperatorOutput",false,"NotCollected").Validate());
        Assert.Throws<InvalidDataException>(()=>new FlowQuantity("1","Observed","OperatorOutput",true,ByteFormat:"Utf8").Validate());
    }
    [Fact]public void ProjectionIncludesOnlyOriginalFlowAndKeepsSharedNodesAndUnknownQuantities(){
        var data=Fixture();Assert.Equal(4,data.Ports.Count(port=>port.Direction=="Output"));Assert.Equal(3,data.Links.Length);
        Assert.Empty(data.PortQuantities);Assert.Empty(data.LinkQuantities);Assert.All(data.Links,link=>Assert.Equal("LogicalReference",link.Relation));
        Assert.Single(data.Ports,port=>port.Node.NodeId=="a"&&port.Direction=="Output");
        var shared=data with{Links=[..data.Links,new("shared","out:a","in:result:0","ReuseReference")]};shared.Validate();
        Assert.Equal(4,サンキー配置.作成(shared,"Rows","Observed","Linear").ノード.Length);
        Assert.Throws<InvalidDataException>(()=>(data with{Identity=data.Identity with{PlanKey=new('F',64)}}).Validate());
        var foreign=data.Ports[0] with{Node=new("other",data.Ports[0].Node.NodeId)};
        Assert.Throws<InvalidDataException>(()=>(data with{Ports=[foreign,..data.Ports.Skip(1)]}).Validate());
    }
    [Fact]public void IndependentPortsAllowJoinExpansionFilterReductionAndSharedOutputsWithoutConservation(){
        var data=Fixture();
        data=data with{PortQuantities=[Rows("out:a","1000"),Rows("out:b","500"),Rows("in:join:0","1000"),Rows("in:join:1","500"),Rows("out:join","500000"),Rows("in:result:0","500000"),Rows("out:result","100")]};
        var layout=サンキー配置.作成(data,"Rows","Observed","Linear");
        Assert.Equal("500000",layout.接続.Single(link=>link.元.SourcePortId=="out:join").始量.DecimalValue);
        Assert.Equal(64,layout.接続.Single(link=>link.元.SourcePortId=="out:join").始幅);
        Assert.True(layout.接続.First().始幅<64);Assert.True(layout.幅補正か);
        Assert.False(layout.循環か);Assert.Equal(4,layout.ノード.Length);
        Assert.Equal("100",data.PortQuantities.Single(value=>value.PortId=="out:result").ObservedRows.DecimalValue);
    }
    [Fact]public void ZeroUnknownLargeRatiosAndLogScalingRemainDistinctAndFinite(){
        var data=Fixture() with{PortQuantities=[Rows("out:a","0"),Rows("in:join:0","0"),Rows("out:b",new string('9',256))]};
        foreach(var scale in new[]{"Linear","Log1p"}){
            var layout=サンキー配置.作成(data,"Rows","Observed",scale);
            var zero=layout.接続.Single(link=>link.元.SourcePortId=="out:a");Assert.Equal(0,zero.始幅);Assert.Equal("0",zero.始量.DecimalValue);
            var unknown=layout.接続.Single(link=>link.元.SourcePortId=="out:join");Assert.Null(unknown.始量.DecimalValue);Assert.Equal(3,unknown.始幅);
            Assert.All(layout.接続,edge=>{Assert.True(double.IsFinite(edge.始幅));Assert.True(double.IsFinite(edge.終幅));Assert.DoesNotContain("NaN",edge.帯);Assert.DoesNotContain("Infinity",edge.帯);});
        }
    }
    [Fact]public void DifferentExecutionsAndClockRangesAreRejected(){
        var id=Guid.NewGuid();var data=Fixture(id);
        var clock=new VisualizationClock("execution-relative:"+id.ToString("N"),id,TimeSpan.TicksPerSecond,"ExecutionRelativeDerived");
        var interval=new VisualizationInterval("whole",id,"query",null,clock.Id,"query","Query",0,10,"QueryInclusive");
        var observed=data with{Clocks=[clock],Intervals=[interval]};observed.Validate();
        Assert.Throws<InvalidDataException>(()=>(observed with{Intervals=[interval with{ExecutionId=Guid.NewGuid()}]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(observed with{Intervals=[interval with{EndTicks=-1}]}).Validate());
        Assert.Throws<InvalidDataException>(()=>(observed with{Events=[new("first",id,"query",null,"missing","query",0,"FirstOutput")]}).Validate());
    }
    [Fact]public async Task StaticSankeyShowsUnknownAndZeroSeparatelyAndKeepsControlledSelection(){
        var data=Fixture() with{PortQuantities=[Rows("out:a","0"),Rows("in:join:0","0")]};
        var html=await Render(data,new(data.Identity.PlanKey,"a"));
        Assert.Contains("data-未知=\"true\"",html);Assert.Contains("data-ゼロ=\"true\"",html);
        Assert.Contains("data-選択中=\"true\"",html);Assert.Contains("LogicalReference",html);
        Assert.Contains("data-sankey.css",html);Assert.Contains("data-グラフ表示=\"maximize\"",html);
        Assert.Contains("0",html);Assert.DoesNotContain("500000",html);
    }
    [Fact]public async Task StaticSankeyRejectsForeignPlanSelectionAndInvalidInputWithoutInventingFlow(){
        var data=Fixture();
        var html=await Render(data,new("other","a"));Assert.DoesNotContain("data-選択中=\"true\"",html);
        html=await Render(data with{Identity=data.Identity with{PlanKey="other"}},null);
        Assert.Contains("role=\"alert\"",html);Assert.DoesNotContain("class=\"サンキー帯\"",html);
    }
    private static async Task<string> Render(QueryVisualizationData data,PlanNodeRef? selected){
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async()=>{
            var result=await renderer.RenderComponentAsync<データサンキー>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"データ",data},{"選択ノード",selected}}));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }
    internal static QueryVisualizationData Fixture(Guid? execution=null){
        ExecutionPlanNode Node(string id)=>new(id,"SourceOperator",id,[],null,null,0,0,[]){Layer="Original"};
        var plan=new Plan(2,"Queries.Run()","test",new('A',64),new('B',64),[Node("a"),Node("b"),Node("join"),Node("result"),new("expression","Expression","Equal",[],null,null,0,0,[])],JsonSerializer.SerializeToElement(new{})){
            Edges=[new("a","join","OriginalDataFlow","left"),new("b","join","OriginalDataFlow","right"),new("join","result","OriginalDataFlow"),new("expression","join","ExpressionDependency"),new("a","join","SelectedReference"),new("join","expression","SourceMapping")]
        };
        var target=new QueryPlanResult("db",1,new('C',64),new('D',64),false,new("query",[plan.MethodKey],[plan]));
        return QueryVisualizationData.FromPlan(target,plan,execution);
    }
    private static PortQuantities Rows(string id,string actual)=>new(id,FlowQuantity.Unknown(),new(actual,"Observed","OperatorBoundary",true),FlowQuantity.Unknown(),FlowQuantity.Unknown());
}
