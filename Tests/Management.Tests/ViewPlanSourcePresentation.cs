using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ViewPlanSourcePresentation{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkPathsShowOnlyUnambiguousAuthorizedSource(bool ambiguous){
        const string snippet="UniqueSdkViewSource";
        SourceFile[] files=[new("Views/Shared.cs",snippet),new("Other/Shared.cs","DifferentSource")];
        if(ambiguous){files=[..files,new("Shared.cs","AmbiguousSource")];}
        var source=new SourceSnapshot("1",files,[]);
        var node=new ExecutionPlanNode("sdk","SourceOperator","Where",[],null,"F:/SDK/Views/Shared.cs",0,snippet.Length,new()){Layer="Original"};
        var plan=new global::Seamless.Management.ExecutionPlanDocument(2,"Query","test",new('A',64),new('B',64),[node],JsonSerializer.SerializeToElement(new{}));
        var result=new QueryPlanResult("db",1,new('C',64),source.Hash(),false,new("Shared",["Query"],[plan]));
        var operations=DispatchProxy.Create<IManagementOperations,Reads>();
        ((Reads)(object)operations).Result=result;
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,Scripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            Editor? editor=null;
            var rendered=await renderer.RenderComponentAsync<Editor>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<Editor>)(value=>editor=value)},{"DB名","DB"},{"定義ID","db"},{"対象",new QueryPlanRequest("db","Shared",1)},{"ソース",source}
            }));
            await editor!.Select(node);
            editor.Refresh();
            var html=WebUtility.HtmlDecode(rendered.ToHtmlString());
            if(ambiguous){Assert.DoesNotContain(snippet,html);Assert.Contains("対応するソース範囲を取得できません",html);}
            else{Assert.Contains(snippet,html);Assert.DoesNotContain("DifferentSource",html);}
        });
    }
    public sealed class Editor:実行計画ビュー{
        [Parameter]public Action<Editor> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public Task Select(ExecutionPlanNode node)=>(Task)typeof(実行計画ビュー).GetMethod("ノード選択時Async",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(this,[node])!;
        public void Refresh()=>this.StateHasChanged();
    }
    public class Reads:DispatchProxy{
        public QueryPlanResult Result=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            if(method!.Name==nameof(IManagementOperations.QueryTraceAsync)){return Task.FromException<QueryTracePage>(new ManagementException("Unsupported","trace unavailable"));}
            Assert.Equal(nameof(IManagementOperations.QueryPlansAsync),method!.Name);
            return Task.FromResult(this.Result);
        }
    }
    public sealed class Scripts:IJSRuntime{
        public ValueTask<T> InvokeAsync<T>(string identifier,object?[]? args)=>throw new InvalidOperationException("Static rendering must not call scripts.");
        public ValueTask<T> InvokeAsync<T>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<T>(identifier,args);
    }
}
