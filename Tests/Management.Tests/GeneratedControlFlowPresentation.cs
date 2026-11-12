using System.Net;
using System.Reflection;
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
using FlowView=Seamless.Management.Components.生成コードフロー;
using PlanView=Seamless.Management.Components.実行計画ビュー;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class GeneratedControlFlowPresentation{
    [Fact]public void LogicalSvgExportRetainsSelectedAndExpressionLayersOnly(){
        var plan=new PlanDocument(2,"Query.Run()","test",new('A',64),new('B',64),[
            new("original","SourceOperator","ORIGINAL_ONLY",[],null,null,0,0,[]){Layer="Original"},
            new("selected","Fragment","SELECTED_LOGICAL",[],null,null,0,0,[]){Layer="Selected"},
            new("expression","Expression","EXPR_LOGICAL",[],null,null,0,0,[]){Layer="OriginalExpression"}
        ],JsonSerializer.SerializeToElement(new{}));
        var target=new QueryPlanResult("db",1,new('C',64),new('D',64),false,new("Items",[plan.MethodKey],[plan]));
        var snapshot=PlanSnapshot.Create(target,plan,display:new("Logical"){StructureLevel="Logical"},includeCode:true);
        var svg=計画保存出力.SVG(snapshot,value=>value);
        Assert.Contains("SELECTED_LOGICAL",svg);Assert.Contains("EXPR_LOGICAL",svg);Assert.DoesNotContain("ORIGINAL_ONLY",svg);
    }
    [Fact]public void ControlLayoutRetainsLoopsSelfEdgesAndActualBlockNumbers(){
        var code=Code();var layout=生成コード配置.作成(code);
        Assert.False(layout.不正な識別子か);Assert.Equal(new[]{0,1,2},layout.ブロック.Select(block=>block.元.Id));
        Assert.Equal(code.Edges.Length,layout.接続.Length);Assert.Equal(2,layout.接続.Count(edge=>edge.戻りか));
        Assert.All(layout.接続,edge=>Assert.Contains("C ",edge.経路));
        Assert.True(layout.ブロック[0].Y<layout.ブロック[1].Y);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task GeneratedSnippetRequiresItsOwnFinalBodyHash(bool matching){
        var code=Code();if(!matching){code=code with{BodyHash=new('0',64)};}
        using var provider=Provider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<FlowView>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"コード",code}}));return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("生成C#の制御フロー",html);Assert.Contains("data-戻り辺=\"true\"",html);Assert.Contains("B1",html);Assert.DoesNotContain("B@",html);
        if(matching){Assert.Contains("return seed;",html);Assert.Contains("本文UTF16範囲",html);}
        else{Assert.DoesNotContain("return seed;",html);Assert.Contains("生成本文のハッシュが一致しない",html);}
        Assert.DoesNotContain("最終生成C#本文\"",html);Assert.DoesNotContain("0 ms",html);
    }

    [Fact]public async Task SyntheticOperationsHaveNoFalseSourceZeroRange(){
        var code=Code();var synthetic=new GeneratedOperation("synthetic","FlowCapture",-1,0,"int",null,true);
        code=code with{Blocks=[new(0,"Entry",true,"None",[],null),new(1,"Block",true,"None",[synthetic],null),new(2,"Exit",true,"None",[],null)]};
        using var provider=Provider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<FlowView>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"コード",code}}));return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("この操作の本文範囲は記録されていません。",html);Assert.DoesNotContain("本文UTF16範囲 0",html);Assert.DoesNotContain("return seed;",html);
    }

    [Theory][InlineData("Available")][InlineData("VersionMismatch")][InlineData("Permission")]
    public async Task ThreeLevelsFetchGeneratedCodeOnlyOnExplicitSelectionAndKeepItsVersion(string response){
        var plan=new PlanDocument(2,"Query.Run()","test",new('A',64),new('B',64),[
            new("operator","SourceOperator","Where",[],null,null,0,0,[]){Layer="Original"},new("selected","Fragment","Loop",[],null,null,0,0,[]){Layer="Selected"},
            new("expression","Expression","Multiply",[],null,null,0,0,[]){Layer="OriginalExpression"}
        ],JsonSerializer.SerializeToElement(new{}));
        var result=new QueryPlanResult("db",1,new('C',64),new('D',64),false,new("Items",[plan.MethodKey],[plan]));
        var requests=new List<QueryPlanRequest>();
        var operations=DispatchProxy.Create<IManagementOperations,ManagerReadState.ReadOperations>();
        ((ManagerReadState.ReadOperations)(object)operations).InvokeRead=(name,args)=>{
            if(name==nameof(IManagementOperations.QueryTraceAsync)){return Task.FromException<QueryTracePage>(new ManagementException("Unsupported","trace unavailable"));}
            Assert.Equal(nameof(IManagementOperations.QueryPlansAsync),name);var request=(QueryPlanRequest)args[0]!;requests.Add(request);
            if(!request.IncludeGeneratedCode){return Task.FromResult(result);}
            if(response=="Permission"){return Task.FromException<QueryPlanResult>(new ManagementException("Permission","generated denied"));}
            var generated=result with{Selection=result.Selection with{Plans=[plan with{GeneratedCode=Code()}]}};
            if(response=="VersionMismatch"){generated=generated with{Version=2};}
            return Task.FromResult(generated);
        };
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;var root=await renderer.RenderComponentAsync<TestPlan>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestPlan>)(value=>view=value)},{"DB名","DB"},{"定義ID","db"},{"対象",new QueryPlanRequest("db","Items",1)}}));
            Assert.Single(requests);Assert.False(requests[0].IncludeGeneratedCode);Assert.DoesNotContain("return seed;",root.ToHtmlString());
            await view!.Level("Logical");Assert.Single(requests);
            var logical=WebUtility.HtmlDecode(root.ToHtmlString());Assert.Contains("Multiply",logical);Assert.Contains("Loop",logical);
            await view.Level("Generated");Assert.Equal(2,requests.Count);Assert.True(requests[1].IncludeGeneratedCode);
            var html=WebUtility.HtmlDecode(root.ToHtmlString());
            if(response=="Available"){Assert.Contains("return seed;",html);Assert.Contains("展開コードのSVG・HTML出力は未対応",html);}
            else{Assert.DoesNotContain("return seed;",html);Assert.DoesNotContain("生成C#の制御フロー\"",html);}
            if(response=="VersionMismatch"){Assert.Contains("生成構造の版が現在の計画と異なります",html);}
            if(response=="Permission"){Assert.Contains("generated denied",html);}
            await view.Level("Relational");Assert.Equal(2,requests.Count);
        });
    }

    private static ServiceProvider Provider(IManagementOperations? operations=null){
        var services=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>();if(operations is not null){services.AddSingleton(operations);}return services.BuildServiceProvider();
    }
    private static GeneratedExecutionCode Code(){
        const string body="{ int seed=0; return seed; }";var operation=new GeneratedOperation("return","Return",body.IndexOf("return",StringComparison.Ordinal),"return seed;".Length,"int","seed",false);
        return new("Available","Interceptor.Run()",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))),body,[new(0,"Entry",true,"None",[],null),new(1,"Block",true,"WhenTrue",[operation],null),new(2,"Exit",true,"None",[],null)],
            [new(0,1,"FallThrough","Regular",[],[],[]),new(1,1,"Conditional","Regular",[],[],[]),new(1,0,"Conditional","Regular",[],[],[]),new(1,2,"FallThrough","Return",[],[],[])],[],[]);
    }
    public sealed class TestPlan:PlanView{
        [Parameter]public Action<TestPlan> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public async Task Level(string value){await(Task)typeof(PlanView).GetMethod("構造変更時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[new ChangeEventArgs{Value=value}])!;this.StateHasChanged();}
    }
}
