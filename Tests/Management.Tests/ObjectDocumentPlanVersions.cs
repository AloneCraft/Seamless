using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ObjectDocumentPlanVersions{
    [Fact]public async Task UnsavedSourceNeverLoadsAPlanFromThePreviousDraftRevision(){
        var operations=DispatchProxy.Create<IManagementOperations,UnexpectedOperations>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var definition=Seamless.Management.DatabaseDesigner.Empty("db","Database");
        var state=new 対象文書状態("db:db","db",new("View","View","","View","Row")){表示="実行計画",計画を開いたか=true};
        var html=await renderer.Dispatcher.InvokeAsync(async()=>{
            var root=await renderer.RenderComponentAsync<対象文書ビュー>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                [nameof(対象文書ビュー.状態)]=state,[nameof(対象文書ビュー.定義)]=definition,[nameof(対象文書ビュー.DB名)]="Database",
                [nameof(対象文書ビュー.公開版)]=1L,[nameof(対象文書ビュー.下書き版)]=2L,[nameof(対象文書ビュー.未保管か)]=true,
                [nameof(対象文書ビュー.公開済みか)]=true,[nameof(対象文書ビュー.表示中か)]=true
            }));
            return WebUtility.HtmlDecode(root.ToHtmlString());
        });
        Assert.Contains("実行計画を取得する前に下書きを保存してください。",html);
        Assert.Equal(0,((UnexpectedOperations)(object)operations).Calls);
    }
    public class UnexpectedOperations:DispatchProxy{
        public int Calls;
        protected override object? Invoke(MethodInfo? method,object?[]? arguments){this.Calls++;throw new InvalidOperationException("Unexpected management call: "+method?.Name);}
    }
}
