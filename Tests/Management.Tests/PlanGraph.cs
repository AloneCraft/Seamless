using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Xunit.Abstractions;
using Plan=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class PlanGraph{
    private readonly ITestOutputHelper output;
    public PlanGraph(ITestOutputHelper output){this.output=output;}

    [Fact]public async Task MaskedSnapshotDropsCodeEvenWhenAReusedGraphStillHasTheOldTooltipMap(){
        var plan=Create(2,[Node("node0")]) with{SourceHash=new('A',64),GeneratedBodyHash=new('B',64)};
        var target=new QueryPlanResult("db",1,new('C',64),new('D',64),false,new("query",[plan.MethodKey],[plan]));
        var masked=PlanSnapshot.Create(target,plan).Data.Plan;
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestGraph? graph=null;var root=await renderer.RenderComponentAsync<TestGraph>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"計画",plan},{"対応コード一覧",new Dictionary<string,string>{{"node0","SECRET_SOURCE_TOOLTIP"}}},{"Capture",(Action<TestGraph>)(value=>graph=value)}
            }));
            Assert.Contains("SECRET_SOURCE_TOOLTIP",root.ToHtmlString());
            await graph!.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",masked}}));
            Assert.DoesNotContain("SECRET_SOURCE_TOOLTIP",root.ToHtmlString());
        });
    }

    [Fact]public async Task StandardZoomReturnsFromWholeGraphAndTinyScaleToOneHundredPercent(){
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestGraph? graph=null;var root=await renderer.RenderComponentAsync<TestGraph>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",Create(2,[Node("input")])},{"Capture",(Action<TestGraph>)(value=>graph=value)}}));
            Assert.True(graph!.Fit);
            graph!.StandardZoom();Assert.Equal(100,graph.Scale);Assert.False(graph.Fit);
            graph.TinyZoom();graph.StandardZoom();Assert.Equal(100,graph.Scale);Assert.False(graph.Fit);
            Assert.Contains("標準倍率100%に戻す",WebUtility.HtmlDecode(root.ToHtmlString()));
        });
    }

    [Theory][InlineData("SharedDag")][InlineData("Chain")]
    public async Task MaximumNodeGraphsMeasureLayoutAndRealStaticRendering(string shape){
        var nodes=Enumerable.Range(0,Plan.MaximumNodes).Select(index=>Node("n"+index) with{Layer="Selected",QueryId=null,SourcePath=null,Start=0,Length=0}).ToArray();
        var edges=new List<ExecutionPlanEdge>();
        if(shape=="SharedDag"){
            const int width=64;
            for(var layer=1;layer<32;layer++){
                for(var column=0;column<width;column++){
                    for(var input=0;input<4;input++){edges.Add(new("n"+((layer-1)*width+(column+input)%width),"n"+(layer*width+column),"SelectedReference"));}
                }
            }
            for(var column=0;column<width;column++){
                for(var input=0;input<4;input++){edges.Add(new("n"+((column+input)%width),"n"+(31*width+column),"SelectedReference"));}
            }
            Assert.Equal(Plan.MaximumEdges,edges.Count);
        }else{
            for(var index=1;index<nodes.Length;index++){edges.Add(new("n"+(index-1),"n"+index,"SelectedReference"));}
            Assert.Equal(Plan.MaximumNodes-1,edges.Count);
        }
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("SyntheticScaleFixture:"+shape)));
        var plan=Create(2,nodes) with{MethodKey="SyntheticScaleFixture."+shape,GeneratorVersion="SyntheticScaleFixture",GeneratorBuild="NotExecuted",SourceHash=hash,GeneratedBodyHash=hash,Edges=edges.ToArray(),Diagnostics=["Synthetic scale fixture. This graph was not executed."]};
        var snapshot=ScaleSnapshot(plan,shape);var bytes=snapshot.ToJson();
        Assert.InRange(bytes.Length,1,PlanSnapshot.MaximumBytes);
        var restored=PlanSnapshot.Read(bytes);plan=restored.Data.Plan;
        Assert.Equal(Plan.MaximumNodes,plan.Nodes.Length);Assert.Equal(edges.Count,plan.Edges.Length);

        var layoutAllocation=GC.GetAllocatedBytesForCurrentThread();var layoutClock=Stopwatch.StartNew();
        var layout=計画グラフ配置.作成(plan,計画表示層.処理);
        layoutClock.Stop();layoutAllocation=GC.GetAllocatedBytesForCurrentThread()-layoutAllocation;
        Assert.False(layout.循環を含むか);Assert.False(layout.識別子重複か);Assert.Equal(0,layout.不明な入力数);
        Assert.Equal(Plan.MaximumNodes,layout.ノード.Length);Assert.Equal(edges.Count,layout.接続.Length);
        Assert.All(layout.接続,edge=>Assert.True(edge.入力.Y<edge.出力.Y));
        Assert.Equal(shape=="Chain"?1:64,layout.集約元.Length);

        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var rendered=await renderer.Dispatcher.InvokeAsync(async()=>{
            var allocation=GC.GetTotalAllocatedBytes(true);var clock=Stopwatch.StartNew();
            var root=await renderer.RenderComponentAsync<計画グラフ>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",plan}}));
            var html=WebUtility.HtmlDecode(root.ToHtmlString());clock.Stop();
            return(Html:html,Milliseconds:clock.Elapsed.TotalMilliseconds,AllocatedBytes:GC.GetTotalAllocatedBytes(true)-allocation);
        });
        Assert.Equal(Plan.MaximumNodes,Count(rendered.Html,"data-ノード-id=\""));
        Assert.Contains("data-モデルノード数=\"2048\"",rendered.Html);
        Assert.Contains("data-モデル接続数=\""+edges.Count+"\"",rendered.Html);
        Assert.Contains("data-詳細ノード数=\"0\"",rendered.Html);
        Assert.Equal(1,Count(rendered.Html,"class=\"計画入力参照 計画合成接続\""));
        Assert.Equal(Plan.MaximumNodes,Count(rendered.Html,"class=\"計画簡略ノード\""));
        Assert.Equal(1,Count(rendered.Html,"class=\"計画グラフノード 計画表示結果\""));
        this.output.WriteLine(JsonSerializer.Serialize(new{Measurement="PlanGraphScale",Shape=shape,Nodes=nodes.Length,Edges=edges.Count,SnapshotBytes=bytes.Length,LayoutMilliseconds=layoutClock.Elapsed.TotalMilliseconds,LayoutThreadAllocatedBytes=layoutAllocation,RenderMilliseconds=rendered.Milliseconds,RenderProcessAllocatedBytes=rendered.AllocatedBytes,HtmlUtf8Bytes=Encoding.UTF8.GetByteCount(rendered.Html),LayoutWidth=layout.幅,LayoutHeight=layout.高さ,PerformanceThresholdApplied=false}));
        ExportScaleSnapshot(shape,bytes);
    }

    [Fact]public async Task LargeGraphCullsDetailsButRetainsCrossingEdgesAndFullModelSearch(){
        var nodes=Enumerable.Range(0,512).Select(index=>Node("n"+index) with{Layer="Selected"}).ToArray();
        var plan=Create(2,nodes) with{Edges=Enumerable.Range(1,nodes.Length-1).Select(index=>new ExecutionPlanEdge("n"+(index-1),"n"+index,"SelectedReference")).Append(new("n0","n511","SelectedReference","Shared")).ToArray()};
        var selected=new List<string>();
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestGraph? graph=null;var root=await renderer.RenderComponentAsync<TestGraph>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"計画",plan},{"Capture",(Action<TestGraph>)(value=>graph=value)},{"ノード選択時",EventCallback.Factory.Create<ExecutionPlanNode?>(selected,(ExecutionPlanNode? node)=>selected.Add(node!.Id))}
            }));
            Assert.Contains("data-簡略ノード数=\"512\"",root.ToHtmlString());graph!.StandardZoom();
            var layout=計画グラフ配置.作成(plan,計画表示層.処理);var middle=layout.ノード[256];
            await graph.表示範囲変更Async(graph.Generation,1,middle.X,middle.Y,400,400,100);
            var html=root.ToHtmlString();Assert.InRange(graph.DetailedCount,1,256);Assert.DoesNotContain("data-ノード-id=\"n0\"",html);Assert.DoesNotContain("data-ノード-id=\"n511\"",html);
            // The long shared edge crosses the viewport even though both endpoints are outside.
            Assert.Contains("M "+layout.ノード[0].中心X+" "+layout.ノード[0].下端+" C "+layout.ノード[0].中心X+" ",html);
            await graph.Search("n511");Assert.Equal("n511",Assert.Single(selected));Assert.Contains("data-ノード-id=\"n511\"",root.ToHtmlString());
            var count=graph.DetailedCount;
            await graph.表示範囲変更Async(graph.Generation-1,2,0,0,100,100,100);
            await graph.表示範囲変更Async(graph.Generation,1,0,0,100,100,100);
            await graph.表示範囲変更Async(graph.Generation,2,double.NaN,0,100,100,100);
            Assert.Equal(count,graph.DetailedCount);
            await graph.キーボードノード移動Async(graph.Generation,-1);Assert.Equal("n510",selected[^1]);
            await graph.DisposeAsync();await graph.表示範囲変更Async(graph.Generation,99,0,0,100,100,100);Assert.Equal(count,graph.DetailedCount);
        });
    }

    [Fact]public async Task LowZoomAndPendingCameraNeverRenderAllDetailedCards(){
        var plan=Create(2,Enumerable.Range(0,512).Select(index=>Node("n"+index)).ToArray());
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestGraph? graph=null;await renderer.RenderComponentAsync<TestGraph>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",plan},{"Capture",(Action<TestGraph>)(value=>graph=value)}}));
            await graph!.表示範囲変更Async(graph.Generation,1,0,0,100000,100000,10);Assert.Equal(0,graph.DetailedCount);
            graph.StandardZoom();Assert.Equal(0,graph.DetailedCount);
            await graph.表示範囲変更Async(graph.Generation,2,0,0,100000,100000,100);Assert.Equal(256,graph.DetailedCount);
        });
    }

    private static PlanSnapshot ScaleSnapshot(Plan plan,string shape){
        var input=Environment.GetEnvironmentVariable("SEAMLESS_PLAN_SCALE_INPUT");
        if(!string.IsNullOrEmpty(input)){
            var path=OwnedScalePath(input);Assert.InRange(new FileInfo(path).Length,1,PlanSnapshot.MaximumBytes);
            var source=PlanSnapshot.Read(File.ReadAllBytes(path));
            plan=plan with{Diagnostics=[..plan.Diagnostics,"Derived from snapshot content hash "+source.ContentHash]};
        }
        var target=new QueryPlanResult("synthetic-scale",1,plan.SourceHash,plan.SourceHash,false,new("synthetic-"+shape,[plan.MethodKey],[plan]));
        return PlanSnapshot.Create(target,plan,includeCode:true);
    }
    private void ExportScaleSnapshot(string shape,byte[] bytes){
        var output=Environment.GetEnvironmentVariable("SEAMLESS_PLAN_SCALE_OUTPUT");if(string.IsNullOrEmpty(output)){return;}
        Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEAMLESS_PLAN_SCALE_INPUT")),"Export requires the fixed P0 snapshot input.");
        var directory=OwnedScalePath(output);Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"synthetic-"+shape+".json");
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);
        this.output.WriteLine("SyntheticScaleSnapshot="+path);
    }
    private static string OwnedScalePath(string value){
        var path=Path.GetFullPath(value);var root=Path.GetFullPath(OperatingSystem.IsWindows()?"F:/Seamless":"/mnt/f/Seamless")+Path.DirectorySeparatorChar;
        if(!path.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)){throw new InvalidOperationException("Scale snapshot input/output must remain under F:/Seamless.");}return path;
    }

    [Fact]public void SharedInputHasOneNodeAndTerminatesAboveDisplayResult(){
        var plan=Create(1,[Node("input"),Node("left","input"),Node("right","input")]);
        var layout=計画グラフ配置.作成(plan,計画表示層.処理);
        Assert.Single(layout.ノード,node=>node.元.Id=="input");
        Assert.Equal(2,layout.接続.Length);
        var input=layout.ノード.Single(node=>node.元.Id=="input");
        Assert.All(layout.接続,edge=>{Assert.Same(input,edge.入力);Assert.True(edge.入力.Y<edge.出力.Y);Assert.Equal("Unknown",edge.種類);});
        Assert.Equal(new[]{"left","right"},layout.集約元.Select(node=>node.元.Id));
        Assert.All(layout.ノード,node=>Assert.True(node.下端<layout.結果Y));
    }

    [Fact]public void OriginalSelectedAndDecisionLayersStaySeparate(){
        var source=Node("source") with{Kind="SourceOperator"};
        var decision=Node("decision") with{Kind="LoweringDecision"};
        var layout=計画グラフ配置.作成(Create(1,[source,Node("input"),Node("output","input"),decision]),計画表示層.すべて);
        Assert.Equal(new[]{計画表示層.元のクエリ,計画表示層.処理,計画表示層.判断と条件},layout.領域.Select(group=>group.層));
        Assert.Equal("output",Assert.Single(layout.集約元).元.Id);
        var edge=Assert.Single(layout.接続);
        Assert.Equal("input",edge.入力.元.Id);Assert.Equal("output",edge.出力.元.Id);
    }

    [Fact]public void TypedExpressionEdgesDetermineLayoutWithoutLegacyInputs(){
        var nodes=new[]{"a","b","multiply","c","equal"}.Select(id=>Node(id) with{Kind="Expression",Layer="OriginalExpression"}).ToArray();
        var plan=Create(2,nodes) with{Edges=[
            new("a","multiply","ExpressionDependency","Left"),new("b","multiply","ExpressionDependency","Right"),
            new("multiply","equal","ExpressionDependency","Left"),new("c","equal","ExpressionDependency","Right")
        ]};
        var layout=計画グラフ配置.作成(plan,計画表示層.数式);
        Assert.Equal(5,layout.ノード.Length);Assert.Equal(4,layout.接続.Length);
        Assert.All(layout.接続,edge=>{Assert.Equal("ExpressionDependency",edge.種類);Assert.True(edge.入力.Y<edge.出力.Y);});
        Assert.Equal("equal",Assert.Single(layout.集約元).元.Id);
        Assert.Contains(layout.接続,edge=>edge.入力役割=="Left");
    }

    [Fact]public void SourceMappingDoesNotCreateExecutionStages(){
        var plan=Create(2,[
            Node("source0") with{Layer="Original"},Node("source1","source0") with{Layer="Original"},Node("selected") with{Layer="Selected"}
        ]) with{Edges=[new("source0","source1","SourceMapping",null),new("source1","selected","SourceMapping",null)]};
        var layout=計画グラフ配置.作成(plan,計画表示層.すべて);
        Assert.Equal(2,layout.接続.Length);
        Assert.All(layout.接続,edge=>Assert.Equal("SourceMapping",edge.種類));
        Assert.Equal(layout.ノード.Single(node=>node.元.Id=="source0").Y,layout.ノード.Single(node=>node.元.Id=="source1").Y);
        Assert.Equal("selected",Assert.Single(layout.集約元).元.Id);
    }

    [Fact]public void CyclesAndMissingInputsAreReported(){
        var layout=計画グラフ配置.作成(Create(1,[Node("a","b","missing"),Node("b","a")]),計画表示層.処理);
        Assert.True(layout.循環を含むか);Assert.Equal(1,layout.不明な入力数);
        Assert.Equal(2,layout.ノード.Length);Assert.Equal(2,layout.接続.Length);
        Assert.Empty(layout.集約元);
    }

    [Fact]public void DuplicateIdentifiersDoNotProduceAnAmbiguousGraph(){
        var layout=計画グラフ配置.作成(Create(1,[Node("same"),Node("same")]),計画表示層.すべて);
        Assert.True(layout.識別子重複か);Assert.Empty(layout.ノード);Assert.Empty(layout.接続);
    }

    [Fact]public async Task StaticRenderingUsesRecordedMetadataAndOneDisplayResult(){
        var plan=Create(2,[Node("recorded") with{Layer="Selected",OutputType="System.Int32",Nullability="NotNullable"}]) with{
            MetadataComplete=false,OmittedNodeCount=2,Diagnostics=["Recorded diagnostic"]
        };
        using var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<計画グラフ>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"計画",plan}}));
            var html=WebUtility.HtmlDecode(root.ToHtmlString());
            Assert.Contains("href=\"./_content/Seamless.Management.Components/execution-plan.css\"",html);
            return html;
        });
        Assert.Contains("data-ノード-id=\"recorded\"",html);Assert.Contains("System.Int32",html);Assert.Contains("Recorded diagnostic",html);
        Assert.Equal(1,Count(html,"class=\"計画グラフノード 計画表示結果\""));
        Assert.DoesNotContain("0 ms",html);Assert.Contains("execution-plan.css",html);
    }

    private static ExecutionPlanNode Node(string id,params string[] inputs)=>new(id,"Fragment",id,inputs,"query","query.cs",0,1,[]);
    private static Plan Create(int version,ExecutionPlanNode[] nodes)=>new(version,"Query.Run()","test","source","body",nodes,JsonSerializer.SerializeToElement(new{}));
    private static int Count(string text,string value)=>text.Split(value,StringSplitOptions.None).Length-1;
    public sealed class TestGraph:計画グラフ{
        [Parameter]public Action<TestGraph> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public double Scale=>(double)typeof(計画グラフ).GetField("倍率",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!;
        public bool Fit=>(bool)typeof(計画グラフ).GetField("全体表示か",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!;
        public long Generation=>(long)typeof(計画グラフ).GetField("描画世代",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!;
        public int DetailedCount=>((Array)typeof(計画グラフ).GetField("詳細ノード",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!).Length;
        public async Task Search(string text){
            typeof(計画グラフ).GetMethod("検索変更時",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(this,[new ChangeEventArgs{Value=text}]);
            await(Task)typeof(計画グラフ).GetMethod("次の一致時Async",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(this,null)!;this.StateHasChanged();
        }
        public void TinyZoom(){typeof(計画グラフ).GetField("倍率",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(this,0.18);}
        public void StandardZoom(){typeof(計画グラフ).GetMethod("標準倍率時",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(this,null);this.StateHasChanged();}
    }
}

internal sealed class StaticRenderScripts:IJSRuntime{
    public StaticRenderScripts(){}
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static rendering must not initialize browser scripts.");
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
}
