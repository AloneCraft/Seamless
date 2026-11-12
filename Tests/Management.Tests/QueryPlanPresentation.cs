using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Plan=Seamless.Management.ExecutionPlanDocument;
using View=Seamless.Management.Components.実行計画ビュー;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class QueryPlanPresentation{
    [Theory][InlineData("Parent")][InlineData("Historical")][InlineData("Fetch")][InlineData("HashMismatch")][InlineData("VersionMismatch")][InlineData("Permission")]
    public async Task TooltipsUseOnlyAuthorizedMatchingSourceAndKeepTheFullSelectedSnippet(string sourceMode){
        var text=new string('A',511)+"\U0001F600"+new string('B',700);
        var source=new SourceSnapshot("1",[new("Query.cs",text)],[]);
        var node=new ExecutionPlanNode("source","SourceOperator","Operation",[],null,"Query.cs",0,text.Length,new()){Layer="Original"};
        var plan=new Plan(2,"Query","test",new('A',64),new('B',64),[node,node with{Id="invalid",Start=text.Length,Length=20}],JsonSerializer.SerializeToElement(new{}));
        var result=new QueryPlanResult("db",1,new('C',64),source.Hash(),false,new("Items",[plan.MethodKey],[plan])){HistoricalSource=sourceMode=="Historical"?source:null};
        var sourceCalls=0;var planCalls=0;
        var operations=DispatchProxy.Create<IManagementOperations,ManagerReadState.ReadOperations>();
        ((ManagerReadState.ReadOperations)(object)operations).InvokeRead=(name,_)=>{
            if(name==nameof(IManagementOperations.QueryPlansAsync)){planCalls++;return Task.FromResult(result);}
            if(name==nameof(IManagementOperations.QueryTraceAsync)){return Task.FromException<QueryTracePage>(new ManagementException("Unsupported","trace unavailable"));}
            Assert.Equal(nameof(IManagementOperations.GetDefinitionAsync),name);sourceCalls++;
            if(sourceMode=="Permission"){return Task.FromException<DefinitionVersion>(new ManagementException("Permission","source denied"));}
            var fetched=sourceMode=="HashMismatch"?source with{Files=[new("Query.cs","OTHER_SOURCE")]}:source;
            return Task.FromResult(new DefinitionVersion(Designer.Empty("db","DB") with{Source=fetched},sourceMode=="VersionMismatch"?2:1,"owner",DateTimeOffset.UtcNow));
        };
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<Microsoft.JSInterop.IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestView? view=null;var rendered=await renderer.RenderComponentAsync<TestView>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<TestView>)(value=>view=value)},{"DB名","DB"},{"定義ID","db"},{"対象",new QueryPlanRequest("db","Items",1)},{"ソース",sourceMode=="Parent"?source:null}
            }));
            var allowed=sourceMode is "Parent" or "Historical" or "Fetch";
            var tooltip=System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString());
            if(allowed){Assert.Contains("Operation\n"+new string('A',511)+"\n",tooltip);Assert.DoesNotContain("\U0001F600",tooltip);Assert.Contains("全文はノードを選択して確認できます。",tooltip);}
            else{Assert.DoesNotContain(new string('A',511),tooltip);}
            var calls=sourceCalls;var mapping=view!.TooltipMap;
            view.Cards(true);view.Cards(false);view.Refresh();Assert.Same(mapping,view.TooltipMap);Assert.Equal(calls,sourceCalls);Assert.Equal(1,planCalls);
            Assert.False(view.TooltipMap.ContainsKey("invalid"));
            if(allowed){await view.Select(node);view.Refresh();Assert.Contains(text,System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));Assert.Equal(calls,sourceCalls);}
            Assert.Equal(sourceMode is "Parent" or "Historical"?0:1,sourceCalls);
        });
    }

    [Fact]public async Task SourceSelectionAndDisplayChangesNeverExecuteOrKeepAnotherMethodsSnippet(){
        var source=new SourceSnapshot("1",[new("Query.cs","UniqueSourceSnippetA UniqueSourceSnippetB")],[]);
        var node=new ExecutionPlanNode("source","SourceOperator","Where",[],null,"Query.cs",0,20,new()){Layer="Original"};
        var plan=new Plan(2,"First","test",new('A',64),new('B',64),[node],JsonSerializer.SerializeToElement<object?>(null));
        var result=new QueryPlanResult("db",1,new('C',64),source.Hash(),false,new("Items",["First","Second"],[plan,plan with{MethodKey="Second"}]));
        var calls=0;
        var operations=DispatchProxy.Create<IManagementOperations,ManagerReadState.ReadOperations>();
        ((ManagerReadState.ReadOperations)(object)operations).InvokeRead=(name,_)=>{
            Assert.Equal(nameof(IManagementOperations.QueryPlansAsync),name);calls++;return Task.FromResult(result);
        };
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<Microsoft.JSInterop.IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestView? view=null;
            var rendered=await renderer.RenderComponentAsync<TestView>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<TestView>)(value=>view=value)},{"DB名","DB"},{"定義ID","db"},{"対象",new QueryPlanRequest("db","Items",1)},{"ソース",source}
            }));
            await view!.Select(node);view.Refresh();
            Assert.Contains("UniqueSourceSnippetA",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            view.SelectMethod(1);view.Refresh();
            Assert.Null(view.Snippet);
            view.Cards(true);view.Refresh();Assert.Contains("hidden",rendered.ToHtmlString());
            view.Cards(false);view.Refresh();Assert.Contains("計画グラフ",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            Assert.Equal(1,calls);
        });
    }
    public sealed class TestView:View{
        [Parameter]public Action<TestView> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Select(ExecutionPlanNode node)=>(Task)typeof(View).GetMethod("ノード選択時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[node])!;
        public void SelectMethod(int value)=>typeof(View).GetProperty("選択インデックス",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(this,value);
        public void Cards(bool value){var mode="DAG";if(value){mode="Tree";}typeof(View).GetMethod("表示選択時",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[mode]);}
        public void Refresh()=>this.StateHasChanged();
        public string? Snippet=>(string?)typeof(View).GetField("対応コード",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this);
        public IReadOnlyDictionary<string,string> TooltipMap=>(IReadOnlyDictionary<string,string>)typeof(View).GetProperty("ノード対応コード",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(this)!;
    }
}
