using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using AuditView=Seamless.Management.Components.監査ビュー;
using PlanView=Seamless.Management.Components.実行計画ビュー;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class ManagerReadState{
    [Theory]
    [InlineData("イベント","Permission")][InlineData("イベント","Authentication")]
    [InlineData("参照集計","Permission")][InlineData("参照集計","Authentication")]
    [InlineData("転記状態","Permission")][InlineData("転記状態","Authentication")]
    [InlineData("イベント","Connection")]
    public async Task AuditRefreshProtectsPreviousResultsAfterAccessLoss(string mode,string code){
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();var proxy=(ReadOperations)(object)operations;
        Exception? failure=null;proxy.InvokeRead=(name,args)=>{
            if(failure is{} error){return name switch{
                nameof(IManagementOperations.AuditEventsAsync)=>Task.FromException<AuditPage>(error),
                nameof(IManagementOperations.ReadSummariesAsync)=>Task.FromException<AuditReadPage>(error),
                nameof(IManagementOperations.AuditDeliveryStatusAsync)=>Task.FromException<AuditDeliveryPage>(error),
                _=>throw new InvalidOperationException(name)
            };}
            return AuditResponse(name);
        };
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestAudit? view=null;var rendered=await renderer.RenderComponentAsync<TestAudit>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestAudit>)(value=>view=value)}}));
            await Mode(view!,mode);Assert.Contains("SensitiveRecord",Html(rendered.ToHtmlString()));
            failure=new ManagementException(code,"access-check-"+code);await view!.検索Async();var html=Html(rendered.ToHtmlString());
            Assert.Contains("access-check-"+code,html);Assert.Contains("data-エラー=\"true\"",html);
            if(code=="Connection"){Assert.Contains("SensitiveRecord",html);}else{Assert.DoesNotContain("SensitiveRecord",html);Assert.DoesNotContain("続きを表示",html);}
            failure=null;await view.検索Async();Assert.Contains("SensitiveRecord",Html(rendered.ToHtmlString()));
        });
    }

    [Theory][InlineData("Permission")][InlineData("Authentication")][InlineData("Connection")]
    public async Task PlanRefreshProtectsPreviousResultsAfterAccessLoss(string code){
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();var proxy=(ReadOperations)(object)operations;
        Task<PlanDocument[]> response=Task.FromResult(Plans("SensitivePlan"));proxy.InvokeRead=(_,_)=>response;
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;var rendered=await renderer.RenderComponentAsync<TestPlan>(Parameters("DB","definition",value=>view=value));
            Assert.Contains("SensitivePlan",Html(rendered.ToHtmlString()));
            response=Task.FromException<PlanDocument[]>(new ManagementException(code,"access-check-"+code));await view!.再取得Async();var html=Html(rendered.ToHtmlString());
            Assert.Contains("access-check-"+code,html);
            if(code=="Connection"){Assert.Contains("SensitivePlan",html);}else{Assert.DoesNotContain("SensitivePlan",html);}
            response=Task.FromResult(Plans("RecoveredPlan"));await view.再取得Async();Assert.Contains("RecoveredPlan",Html(rendered.ToHtmlString()));
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task PlanDatabaseSwitchCancelsAndIgnoresOldSuccessOrFailure(bool failOld){
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();var proxy=(ReadOperations)(object)operations;
        var pending=new TaskCompletionSource<PlanDocument[]>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken oldCancellation=default;
        Task<PlanDocument[]> response=Task.FromResult(Plans("InitialPlan"));proxy.InvokeRead=(_,args)=>{oldCancellation=(CancellationToken)args[1]!;return response;};
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;var rendered=await renderer.RenderComponentAsync<TestPlan>(Parameters("FirstDB","same-id",value=>view=value));
            response=pending.Task;var old=view!.再取得Async();var cancelled=oldCancellation;
            response=Task.FromResult(Plans("CurrentPlan"));await view.SetParametersAsync(Parameters("SecondDB","same-id",value=>view=value));Assert.True(cancelled.IsCancellationRequested);
            if(failOld){pending.SetException(new IOException("ObsoleteFailure"));}else{pending.SetResult(Plans("ObsoletePlan"));}
            await old;var html=Html(rendered.ToHtmlString());Assert.Contains("CurrentPlan",html);Assert.DoesNotContain("Obsolete",html);Assert.DoesNotContain("読み込んでいます",html);
        });
    }

    [Fact]public async Task AuditModeSwitchAndDisposalCancelPendingReads(){
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();var proxy=(ReadOperations)(object)operations;
        TaskCompletionSource<AuditPage>? pending=null;CancellationToken cancellation=default;
        proxy.InvokeRead=(name,args)=>{
            if(name==nameof(IManagementOperations.AuditEventsAsync)&&pending is not null){cancellation=(CancellationToken)args[1]!;return pending.Task;}
            return AuditResponse(name);
        };
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestAudit? view=null;var rendered=await renderer.RenderComponentAsync<TestAudit>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestAudit>)(value=>view=value)}}));
            pending=new(TaskCreationOptions.RunContinuationsAsynchronously);var old=view!.検索Async();var switched=cancellation;
            await Mode(view,"転記状態");Assert.True(switched.IsCancellationRequested);pending.SetException(new IOException("ObsoleteFailure"));await old;
            var html=Html(rendered.ToHtmlString());Assert.Contains("WAL転記",html);Assert.DoesNotContain("ObsoleteFailure",html);
            pending=new(TaskCreationOptions.RunContinuationsAsynchronously);var loading=Mode(view,"イベント");view.Dispose();Assert.True(cancellation.IsCancellationRequested);
            pending.SetException(new ManagementException("Permission","DisposedFailure"));await loading;Assert.DoesNotContain("DisposedFailure",Html(rendered.ToHtmlString()));
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task PlanDisposalCancelsAndIgnoresPendingSuccessOrFailure(bool fail){
        var operations=DispatchProxy.Create<IManagementOperations,ReadOperations>();var proxy=(ReadOperations)(object)operations;
        var pending=new TaskCompletionSource<PlanDocument[]>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationToken cancellation=default;
        Task<PlanDocument[]> response=Task.FromResult(Plans("InitialPlan"));proxy.InvokeRead=(_,args)=>{cancellation=(CancellationToken)args[1]!;return response;};
        using var provider=Provider(operations);await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestPlan? view=null;var rendered=await renderer.RenderComponentAsync<TestPlan>(Parameters("DB","definition",value=>view=value));
            response=pending.Task;var loading=view!.再取得Async();view.Dispose();view.Dispose();Assert.True(cancellation.IsCancellationRequested);
            if(fail){pending.SetException(new IOException("DisposedFailure"));}else{pending.SetResult(Plans("DisposedPlan"));}
            await loading;Assert.DoesNotContain("Disposed",Html(rendered.ToHtmlString()));
        });
    }

    private static object AuditResponse(string name){
        var now=DateTimeOffset.UtcNow;
        return name switch{
            nameof(IManagementOperations.AuditEventsAsync)=>Task.FromResult(new AuditPage([new(Guid.NewGuid(),null,now,now,Guid.NewGuid(),"subject","DB","SensitiveRecord",AuditAction.Read,AuditOutcome.Succeeded,null,null,null,"OK")],1,2,true,1)),
            nameof(IManagementOperations.ReadSummariesAsync)=>Task.FromResult(new AuditReadPage([new(1,new(now,now,Guid.NewGuid(),"subject","DB","SensitiveRecord",null,null),1,now)],1,2,true,1)),
            nameof(IManagementOperations.ReadAuditStatusAsync)=>Task.FromResult(new AuditReadBufferStatus(Guid.NewGuid(),now,true,1,1,0,0,1,1,0,0,now,null,null,false,false)),
            nameof(IManagementOperations.AuditDeliveryStatusAsync)=>Task.FromResult(new AuditDeliveryPage([new(Guid.NewGuid(),"DB","SensitiveRecord","hash",false,false,1,0,now,null)],1,true,true,null)),
            _=>throw new InvalidOperationException(name)
        };
    }
    private static PlanDocument[] Plans(string method)=>[new(1,method,"1","source","body",[new("n","SourceOperator","Where",[],null,"query.cs",0,1,[])],JsonSerializer.SerializeToElement(new{}))];
    private static ServiceProvider Provider(IManagementOperations operations)=>new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<Microsoft.JSInterop.IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
    private static string Html(string value)=>System.Net.WebUtility.HtmlDecode(value);
    private static Task Mode(AuditView view,string mode){var type=typeof(AuditView).GetNestedType("監査種別",BindingFlags.NonPublic)!;return(Task)typeof(AuditView).GetMethod("表示変更時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,[Enum.Parse(type,mode)])!;}
    private static ParameterView Parameters(string database,string definition,Action<TestPlan> capture)=>ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",capture},{"DB名",database},{"定義ID",definition}});
    public sealed class TestAudit:AuditView{
        [Parameter]public Action<TestAudit> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    public sealed class TestPlan:PlanView{
        [Parameter]public Action<TestPlan> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    public class ReadOperations:DispatchProxy{
        public Func<string,object?[],object?> InvokeRead=null!;
        protected override object? Invoke(MethodInfo? method,object?[]? args)=>this.InvokeRead(method!.Name,args!);
    }
}
