using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class DraftStartup{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ADatabaseOpensWithoutThrowingForOptionalDraftOrBuildState(bool hasOwnDraft){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"startup-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var publisher=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        if(hasOwnDraft){publisher=LocalOwnerIdentity.Capture().Account();}
        NativeWorkflow.Publish(service,publisher,Seamless.Management.DatabaseDesigner.Empty("published-only","PublishedDb"));
        await using var operations=new ConcurrentManagementOperations(service);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(operations).AddSingleton<IJSRuntime,NoScript>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        var missingDraftExceptions=0;
        void Observe(object? sender,FirstChanceExceptionEventArgs args){
            if(args.Exception is ManagementException{Code:"NotFound"}){Interlocked.Increment(ref missingDraftExceptions);}
        }
        AppDomain.CurrentDomain.FirstChanceException+=Observe;
        try{
            var html=await renderer.Dispatcher.InvokeAsync(async()=>{
                var component=await renderer.RenderComponentAsync<管理ワークスペース>(ParameterView.Empty);
                return component.ToHtmlString();
            });
            Assert.Contains("PublishedDb",html);
            Assert.Equal(0,Volatile.Read(ref missingDraftExceptions));
        }finally{AppDomain.CurrentDomain.FirstChanceException-=Observe;}
    }
    private sealed class NoScript:IJSRuntime{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>throw new InvalidOperationException("Startup rendering must not execute JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken cancellationToken,object?[]? args)=>this.InvokeAsync<TValue>(identifier,args);
    }
}
