using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class PlanSnapshotVisualization{
    private const string Secret="private_source_secret";
    [Fact]public void FormatOneKeepsItsExactDataHashAndHasNoExtensionProperty(){
        var (target,data,_,_)=Example();var snapshot=PlanSnapshot.Create(target,data.Plan);
        var value=snapshot.Data;
        var legacy=new{value.DefinitionId,value.ObjectId,value.Version,value.ArtifactHash,value.SourceHash,value.FromDraft,value.DraftRevision,value.Plan,value.Statistics,value.Display,value.IncludesCode};
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(legacy,ManagementJson.Options)));
        Assert.Equal(1,snapshot.Format);Assert.Equal(hash,snapshot.ContentHash);Assert.DoesNotContain("extension",Encoding.UTF8.GetString(snapshot.ToJson()),StringComparison.OrdinalIgnoreCase);
        Assert.Equal(hash,PlanSnapshot.Read(snapshot.ToJson()).ContentHash);
    }
    [Theory][InlineData("Sankey")][InlineData("Gantt")][InlineData("Heat")]
    public void FormatTwoRestoresQuantitiesClocksIntervalsHeatAndTheActualDisplay(string mode){
        var (target,data,heat,stats)=Example();var view=new PlanSnapshotViewState(mode,"Where",data.Plan.Nodes[1].Id){Sankey=new("Bytes","Compare","Log1p",false),Heat=new("Process","Memory","Mono",true,2,350){Overlay=true,PlaybackSpeed=2,RangeStart=2,RangeEnd=6},GanttZoom=3};
        var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,view,stats,includeCode:true);
        var restored=PlanSnapshot.Read(saved.ToJson());var projected=restored.Visualization()!;
        Assert.Equal(2,restored.Format);Assert.Equal(view,restored.Data.Extension!.View);Assert.Equal(data.Ports,projected.Ports);Assert.Equal(data.Links,projected.Links);
        Assert.Equal(data.PortQuantities,projected.PortQuantities);Assert.Equal(data.Clocks,projected.Clocks);Assert.Equal(data.Intervals,projected.Intervals);Assert.Equal(JsonSerializer.Serialize(heat),JsonSerializer.Serialize(restored.Data.Extension.ResourceHeat));
        using var json=JsonDocument.Parse(restored.ToJson());var extension=json.RootElement.GetProperty("data").GetProperty("extension");
        Assert.False(extension.TryGetProperty("plan",out _));Assert.False(extension.TryGetProperty("ports",out _));Assert.False(extension.TryGetProperty("links",out _));
        var svg=計画保存出力.SVG(restored,value=>value);var html=計画保存出力.HTML(restored,value=>value);
        Assert.Equal(mode,XDocument.Parse(svg).Root!.Attribute("data-mode")!.Value);Assert.Contains("data-mode=\""+mode+"\"",html);Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]public void CodeFreeProjectionRelabelsEveryReferenceAndRemovesFreeFormMetadata(){
        var (target,data,heat,stats)=Example();
        data=data with{Clocks=data.Clocks.Select(clock=>clock with{Id=Secret,Origin=Secret}).ToArray(),Intervals=data.Intervals.Select(interval=>interval with{Id=Secret,ClockId=Secret,LaneId=Secret,InstanceId=Secret,Scope=Secret}).ToArray(),Events=[],Diagnostics=[Secret]};
        heat=heat with{Clock=heat.Clock with{Id=Secret},Lanes=heat.Lanes.Select(lane=>lane with{Label=Secret,PhysicalResourceId=Secret,Topology=new(){{Secret,Secret}},PercentBasis=new(Secret,100,Secret,Secret)}).ToArray(),Diagnostics=[Secret],Persistence=new("Failed",Secret)};
        var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,new("Heat",Secret,Secret),stats);
        var text=Encoding.UTF8.GetString(saved.ToJson());Assert.DoesNotContain(Secret,text);Assert.DoesNotContain(Secret,計画保存出力.HTML(saved,value=>value));
        var restored=PlanSnapshot.Read(saved.ToJson());var model=restored.Visualization()!;model.Validate();
        Assert.Equal("node0",restored.Data.Extension!.View.SelectedNodeId);Assert.Equal("",restored.Data.Extension.View.Search);
        Assert.Equal(model.Identity.PlanKey,model.Intervals[0].Node!.PlanKey);Assert.Equal("node0",model.Intervals[0].Node!.NodeId);
        Assert.All(model.PortQuantities,value=>Assert.Contains(model.Ports,port=>port.Id==value.PortId));
        Assert.Equal(model.Identity.PlanKey,restored.Data.Extension.ResourceHeat!.Samples[0].Node!.PlanKey);
        Assert.Equal("Failed",restored.Data.Extension.ResourceHeat.Persistence!.State);Assert.Null(restored.Data.Extension.ResourceHeat.Persistence.Reason);
        Assert.Equal(restored.ContentHash,PlanSnapshot.Read(restored.ToJson()).ContentHash);
    }
    [Fact]public void FormatAndExtensionMustAgreeAndForgedCrossExecutionReferencesAreRejected(){
        var (target,data,heat,stats)=Example();var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,statistics:stats);
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data,1)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data with{Extension=null},2)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data,3)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.CreateVisualization(target,data.Plan,data with{Identity=data.Identity with{ObjectId="Other"}}));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.CreateVisualization(target,data.Plan,data,heat with{ExecutionId=Guid.NewGuid()}));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data with{Extension=saved.Data.Extension! with{Intervals=[data.Intervals[0] with{Node=new(new('D',64),Secret)}]}},2)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data with{Extension=saved.Data.Extension! with{PortQuantities=[data.PortQuantities[0] with{PortId="missing"}]}},2)));
    }
    [Fact]public void OversizeExtensionIsAnErrorAndNeverFallsBackToFormatOne(){
        var (target,data,heat,_)=Example();heat=heat with{Samples=Enumerable.Repeat(heat.Samples[0],10000).ToArray()};
        var exception=Assert.Throws<InvalidDataException>(()=>PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,includeCode:true));Assert.Contains("サイズ上限",exception.Message);
    }
    [Fact]public void ImportedTimeSelectionMustStayInsideTheRecordedClockDomain(){
        var (target,data,heat,_)=Example();var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,includeCode:true);
        var view=saved.Data.Extension!.View;
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data with{Extension=saved.Data.Extension with{View=view with{Heat=view.Heat with{RangeStart=-1,RangeEnd=9}}}},2)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(saved.Data with{Extension=saved.Data.Extension with{View=view with{Heat=view.Heat with{RangeStart=1}}}},2)));
        var valid=PlanSnapshot.Read(Raw(saved.Data with{Extension=saved.Data.Extension with{View=view with{Heat=view.Heat with{RangeStart=0,RangeEnd=8}}}},2));Assert.Equal(8,valid.Data.Extension!.View.Heat.RangeEnd);
    }
    [Fact]public async Task OfflineSaveViewReplaysTheSavedModeWithoutAnyManagementApi(){
        var (target,data,heat,stats)=Example();var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data,heat,new("Heat","",Secret),stats);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            Replay? view=null;var rendered=await renderer.RenderComponentAsync<Replay>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<Replay>)(value=>view=value)}}));
            view!.Open(saved);var html=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());Assert.Contains("資源ヒートガント",html);Assert.Equal("Heat",view.Mode);Assert.DoesNotContain(Secret,html);
        });
    }
    public sealed class Replay:計画保存{
        [Parameter]public Action<Replay> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public void Open(PlanSnapshot value){typeof(計画保存).GetMethod("読込を設定",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[value]);this.StateHasChanged();}
        public string? Mode=>(string?)typeof(計画保存).GetField("読込表示方式",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this);
    }
    private static byte[] Raw(PlanSnapshotData data,int format){var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data,ManagementJson.Options)));return JsonSerializer.SerializeToUtf8Bytes(new PlanSnapshot(format,data,hash),ManagementJson.Options);}
    private static (QueryPlanResult Target,QueryVisualizationData Data,ResourceHeatTrace Heat,ObjectExecutionStatistics Statistics) Example(){
        var a=new ExecutionPlanNode(Secret,"SourceOperator",Secret,[],null,Secret,0,3,new()){Layer="Original"};
        var b=new ExecutionPlanNode("filter","SourceOperator","Where",[Secret],null,null,0,0,new()){Layer="Original"};
        var plan=new PlanDocument(2,Secret,"test",new('B',64),new('C',64),[a,b],JsonSerializer.SerializeToElement<object?>(null)){Edges=[new(Secret,"filter","OriginalDataFlow",Secret)]};
        var target=new QueryPlanResult("definition",1,new('A',64),new('B',64),false,new("Query",[Secret],[plan]));var id=Guid.NewGuid();var data=QueryVisualizationData.FromPlan(target,plan,id);
        var unknown=FlowQuantity.Unknown();var value=new FlowQuantity("0","Observed","Logical",true);
        data=data with{PortQuantities=[new(data.Ports[0].Id,unknown,value,FlowQuantity.Unknown(),new("12","Observed","Logical",true,ByteFormat:"Serialized"))],
            Clocks=[new("clock",id,1000,"Stopwatch")],Intervals=[new("query",id,"worker",new(data.Identity.PlanKey,Secret),"clock","worker","Task",0,8,"TaskExecution")],Events=[new("done",id,"worker",null,"clock","worker",8,"Completed")]};
        var heat=new ResourceHeatTrace(id,"definition","Query",1,new('A',64),true,new("clock",1000,0,DateTimeOffset.UnixEpoch),
            [new("mem","Memory","Memory","Process","WorkingSet","Bytes",PercentBasis:new("Capacity",100,"Bytes","OS"))],
            [new("mem",0,8,50,50,ExecutionId:id,Node:null)],TimeSpan.FromMilliseconds(100),0,[]);
        // 帰属を含む参照も交換形式で同じ計画へ束縛する。
        heat=heat with{Lanes=[heat.Lanes[0] with{Scope="Node"}],Samples=[heat.Samples[0] with{Node=new(data.Identity.PlanKey,Secret)}]};
        var stats=new ObjectExecutionStatistics(id,"definition","Query",1,new('A',64),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(8),TimeSpan.FromMilliseconds(8),TimeSpan.Zero,null,1,1,false);
        return(target,data,heat,stats);
    }
}
