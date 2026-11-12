using System.Reflection;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class WorkspaceStartup{
    [Theory][InlineData("Pending")][InlineData("Ready")][InlineData("Failed")]
    public async Task StartupSuppressesOperationsUntilReadyAndKeepsRecoveryOutsideTheInertRegion(string mode){
        var operations=DispatchProxy.Create<IManagementOperations,EmptyOperations>();
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestWorkspace? component=null;
            var output=await renderer.RenderComponentAsync<TestWorkspace>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestWorkspace>)(value=>component=value)},{"利用者",new UserInfo("owner",[],false)}}));
            Assert.NotNull(component);
            if(mode=="Ready"){typeof(管理ワークスペース).GetField("操作受付中か",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(component,true);component!.Refresh();}
            if(mode=="Failed"){await(Task)typeof(管理ワークスペース).GetMethod("OnAfterRenderAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(component,[true])!;}
            var html=WebUtility.HtmlDecode(output.ToHtmlString());var root=Regex.Match(html,"<div[^>]*class=\"管理ワークスペース\"[^>]*>").Value;
            Assert.NotEmpty(root);
            if(mode=="Ready"){Assert.Contains("data-interactive-ready=\"true\"",root);Assert.Contains("aria-busy=\"false\"",root);Assert.DoesNotContain(" inert",root);Assert.DoesNotContain("class=\"起動状態案内\"",html);}
            else{
                Assert.Contains("data-interactive-ready=\"false\"",root);Assert.Contains("aria-busy=\"true\"",root);Assert.Contains(" inert",root);
                Assert.Contains("href=\"./\" data-enhance-nav=\"false\"",html);
                Assert.True(html.IndexOf("class=\"起動状態案内\"",StringComparison.Ordinal)<html.IndexOf("class=\"管理ワークスペース\"",StringComparison.Ordinal));
                var role="status";if(mode=="Failed"){role="alert";}Assert.Contains("class=\"起動状態案内\" role=\""+role+"\"",html);
            }
        });
    }
    public sealed class TestWorkspace:管理ワークスペース{
        [Parameter]public Action<TestWorkspace> Capture{get;set;}=default!;
        protected override void OnInitialized()=>this.Capture(this);
        public void Refresh()=>this.StateHasChanged();
    }
    public class EmptyOperations:DispatchProxy{
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{
            nameof(IManagementOperations.InfoAsync)=>Task.FromResult(new ServerInfo(Guid.NewGuid(),"fixture","fixture",[])),
            nameof(IManagementOperations.DatabasesAsync)=>Task.FromResult(new CatalogPage<CatalogDatabase>([],0,false,"")),
            _=>throw new InvalidOperationException(method.Name)
        };
    }
}
