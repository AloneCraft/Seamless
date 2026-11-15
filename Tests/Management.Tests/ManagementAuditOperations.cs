using Seamless.Management;
using Seamless.Management.Server;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AuditView=Seamless.Management.Components.監査ビュー;
using PlanView=Seamless.Management.Components.実行計画ビュー;

namespace Management.Tests;

public sealed class ManagementAuditOperations{
    [Fact]public async Task AuditAndRealPlansUseSharedInProcessIpcAndBrowserOperations(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"admin-"+Guid.NewGuid().ToString("N"));
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
            var definition=NativeWorkflow.Database();
            definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("using Seamless.Database;","using Seamless.Database;using System.Linq;using Seamless.FLinq;").Replace("public Table<Keys.Row,Tables.Row> Rows=>","public int Total()=>SumValues(this.Rows.Select(row=>row.Value).ToArray());[LinqIntercept(true)] private static int SumValues(int[] values)=>values.Where(value=>value>0).Select(value=>value*2).Sum();public Table<Keys.Row,Tables.Row> Rows=>")}).ToArray()}};
            using(var direct=new RuntimeCompiler().Compile(definition.Source)){
                Assert.NotEmpty(direct.Result.Plans);
            }
            NativeWorkflow.Publish(service,user,definition);
            Assert.NotEmpty(service.Plans(user,"db").Plans);
            await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=10})),new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=2,Value=20}))]));
            var query=new Definition("sum","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Total();}")],[]));
            NativeWorkflow.Publish(service,user,query);
            Assert.Equal(60,Assert.Single(service.Execute(user,new("sum",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());
            var now=DateTimeOffset.UtcNow;
            foreach(var index in Enumerable.Range(1,125)){
                store.AppendAudit(new(Guid.NewGuid(),null,now,now,store.InstanceId,"tester","Native","Rows",AuditAction.DataChange,AuditOutcome.Succeeded,null,null,null,"Fixture-"+index,"AuditDemo"));
            }
            store.ApplyReadSummaryBatch(Guid.NewGuid(),[new(new(now.AddMinutes(-1),now,store.InstanceId,"tester","Native","AuditDemo.Query",null,null),7)]);
            await using var concurrent=new ConcurrentManagementOperations(service);await Check(concurrent);await CheckRendering(concurrent);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);await Check(ipc);
            using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:24501/"));
            var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24501");
            var browser=new BrowserManagementOperations(sessions,concurrent,()=>Task.FromResult<string?>(session.Id));await Check(browser);
            sessions.RevokeAll();Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.AuditEventsAsync(new()))).Code);
            service.ChangeUser(user,new("auditor","TestPassword-456",[new("*",ManagementPermission.All)]));
            using var userSessions=new UserBrowserSessions(service);var auditSession=userSessions.SignIn(new("auditor","TestPassword-456"));
            IManagementOperations authenticated=new AuthenticatedManagementOperations(concurrent,userSessions,()=>Task.FromResult<string?>(auditSession.Id));
            await Check(authenticated);
            service.ChangeUser(user,new("auditor",null,[new("Native",ManagementPermission.View)]));
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.AuditEventsAsync(new()))).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.ReadSummariesAsync(new()))).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.AuditDeliveryStatusAsync())).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.ReadAuditStatusAsync())).Code);
            var viewer=new UserAccount("viewer","",[new("Native",ManagementPermission.View)]);
            Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.AuditEvents(viewer,new())).Code);
        }
        using var embedded=InProcessManagementHost.Open(root);await Check(embedded);

        static async Task Check(IManagementOperations operations){
            var first=await operations.AuditEventsAsync(new(Text:"AuditDemo",Database:"Native",Limit:100));Assert.Equal(100,first.Items.Length);Assert.True(first.HasMore);
            var next=await operations.AuditEventsAsync(new(Text:"AuditDemo",Database:"Native",Before:first.NextBefore,Ceiling:first.Ceiling));
            Assert.Equal(25,next.Items.Length);Assert.False(next.HasMore);Assert.Equal(125,first.Items.Concat(next.Items).Select(item=>item.EventId).Distinct().Count());
            Assert.Equal(7,Assert.Single((await operations.ReadSummariesAsync(new(Text:"AuditDemo"))).Items).Count);
            Assert.NotNull(await operations.ReadAuditStatusAsync());Assert.NotNull(await operations.AuditDeliveryStatusAsync());
            var plans=await operations.PlansAsync("db");Assert.NotEmpty(plans);
            Assert.Contains(plans.SelectMany(plan=>plan.Nodes),node=>node.Kind=="SourceOperator"&&node.Label=="Where");
            Assert.Contains(plans.SelectMany(plan=>plan.Nodes),node=>node.SourcePath is not null&&node.Start>=0);
            Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>operations.AuditEventsAsync(new(FromUtc:DateTimeOffset.UtcNow,ToUtc:DateTimeOffset.UtcNow.AddDays(-1))))).Code);
        }
    }
    private static async Task CheckRendering(IManagementOperations operations){
        using var provider=new ServiceCollection().AddLogging().AddSingleton(operations).AddSingleton<Microsoft.JSInterop.IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestAudit? audit=null;
            var rendered=await renderer.RenderComponentAsync<TestAudit>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestAudit>)(component=>audit=component)}}));
            Set(typeof(AuditView),audit!,"検索語","AuditDemo");await audit!.検索Async();Assert.Contains("100 件",Html(rendered.ToHtmlString()));
            Set(typeof(AuditView),audit,"検索語","not-matching");await audit.続き取得Async();Assert.Contains("25 件",Html(rendered.ToHtmlString()));
            await audit.検索Async();Assert.Contains("該当する記録はありません",Html(rendered.ToHtmlString()));
            Set(typeof(AuditView),audit,"開始日時",DateTime.UtcNow);Set(typeof(AuditView),audit,"終了日時",DateTime.UtcNow.AddDays(-1));await audit.検索Async();
            Assert.Contains("監査の検索範囲が不正",Html(rendered.ToHtmlString()));
            Set(typeof(AuditView),audit,"開始日時",null);Set(typeof(AuditView),audit,"終了日時",null);Set(typeof(AuditView),audit,"検索語","AuditDemo");
            var mode=typeof(AuditView).GetNestedType("監査種別",BindingFlags.NonPublic)!;
            await(Task)typeof(AuditView).GetMethod("表示変更時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(audit,[Enum.Parse(mode,"参照集計")])!;
            Assert.Contains("AuditDemo.Query",Html(rendered.ToHtmlString()));Assert.Contains("参照集計 · 1 件",Html(rendered.ToHtmlString()));
            await(Task)typeof(AuditView).GetMethod("表示変更時Async",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(audit,[Enum.Parse(mode,"転記状態")])!;
            Assert.Contains("WAL転記",Html(rendered.ToHtmlString()));
            var plans=await operations.PlansAsync("db");var draft=plans.Select(plan=>plan with{MethodKey="DraftMethod"}).ToArray();TestPlan? planView=null;
            var parameters=ParameterView.FromDictionary(new Dictionary<string,object?>{{"Capture",(Action<TestPlan>)(component=>planView=component)},{"DB名","Native"},{"定義ID","db"},{"外部計画",draft}});
            var planHtml=await renderer.RenderComponentAsync<TestPlan>(parameters);Assert.Contains("DraftMethod",Html(planHtml.ToHtmlString()));
            await planView!.再取得Async();Assert.DoesNotContain("DraftMethod",Html(planHtml.ToHtmlString()));Assert.Contains("公開済み定義の計画",Html(planHtml.ToHtmlString()));Assert.Contains("schema.cs",Html(planHtml.ToHtmlString()));
            // 表示方式は列挙(計画描画方式)で選ぶ。DOT を選ぶと DOT のコードを表示する。
            var drawing=typeof(PlanView).GetField("描画方式",BindingFlags.Instance|BindingFlags.NonPublic)!;Set(typeof(PlanView),planView,"描画方式",Enum.Parse(drawing.FieldType,"DOT"));await planView.SetParametersAsync(parameters);Assert.Contains("digraph ExecutionPlan",Html(planHtml.ToHtmlString()));
        });
    }
    public sealed class TestAudit:AuditView{
        [Parameter]public Action<TestAudit> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    public sealed class TestPlan:PlanView{
        [Parameter]public Action<TestPlan> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    private static string Html(string value)=>System.Net.WebUtility.HtmlDecode(value);
    private static void Set(Type type,object target,string name,object? value)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
}
