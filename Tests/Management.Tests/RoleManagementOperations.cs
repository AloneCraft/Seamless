using System.Collections.Immutable;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;
using RolesView=Seamless.Management.Components.ロール管理ビュー;

namespace Management.Tests;

public sealed class RoleManagementOperations{
    [Fact]public async Task SharedEntriesEditTheExistingPolicyAndEnforceCurrentManagementPermission(){
        var root=Root("roles");var database=Guid.NewGuid();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            InitializePolicy(store,database);
            await using var operations=new ConcurrentManagementOperations(service);await Check(operations,database);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);await Check(ipc,database);
            using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:24504/"));
            var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24504");
            var browser=new BrowserManagementOperations(sessions,operations,()=>Task.FromResult<string?>(session.Id));await Check(browser,database);
            sessions.RevokeAll();Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.AuthorizationDatabasesAsync(new()))).Code);
            var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);service.ChangeUser(admin,new("editor","TestPassword-123",[new("*",ManagementPermission.All)]));
            using var userSessions=new UserBrowserSessions(service);var userSession=userSessions.SignIn(new("editor","TestPassword-123"));
            IManagementOperations authenticated=new AuthenticatedManagementOperations(operations,userSessions,()=>Task.FromResult<string?>(userSession.Id));await Check(authenticated,database);
            var before=await operations.DatabaseAuthorizationAsync(new(database));service.ChangeUser(admin,new("editor",null,[new("Sample",ManagementPermission.Manage)]));
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.AuthorizationDatabasesAsync(new()))).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.ChangeDatabaseAuthorizationAsync(new(Guid.NewGuid(),database,before.Version)))).Code);
            using var policy=StoredAuthorization.Open(store);Assert.Contains(policy.History().Items,item=>item.AppliedBy.Subject.Value.StartsWith("os-owner:",StringComparison.Ordinal));
            Assert.Contains(policy.History().Items,item=>item.AppliedBy.Subject.Value=="management-user:editor");
        }
        using var embedded=InProcessManagementHost.Open(root);await Check(embedded,database);

        static async Task Check(IManagementOperations operations,Guid database){
            Assert.Contains("DatabaseAuthorizationV1",(await operations.InfoAsync()).Capabilities);
            var databases=await operations.AuthorizationDatabasesAsync(new());Assert.Equal(database,Assert.Single(databases.Items).Id);
            var original=await operations.DatabaseAuthorizationAsync(new(database));var roleId=Guid.NewGuid();var subject=new SecuritySubjectId(Guid.NewGuid(),"employee");
            var edit=new DatabaseAuthorizationEdit(Guid.NewGuid(),database,original.Version){UpsertRoles=[new(roleId,"Reader",[new(database,SecurityPermission.Read)])],Assign=[new(subject,SecurityPrincipalKind.User,roleId)]};
            var saved=await operations.ChangeDatabaseAuthorizationAsync(edit);Assert.Equal(original.Version+1,saved.Version);Assert.Equal(saved,await operations.AuthorizationChangeResultAsync(edit.OperationId));
            var after=await operations.DatabaseAuthorizationAsync(new(database));Assert.Contains(after.Items,item=>item.Role?.Id==roleId);Assert.Contains(after.Items,item=>item.Binding?.Subject==subject);
            var conflict=await Assert.ThrowsAsync<ManagementException>(()=>operations.ChangeDatabaseAuthorizationAsync(edit with{OperationId=Guid.NewGuid()}));Assert.Equal("Conflict",conflict.Code);
            var invalid=new DatabaseAuthorizationEdit(Guid.NewGuid(),database,saved.Version){UpsertRoles=[new(roleId,"Reader",[new(Guid.NewGuid(),SecurityPermission.AllDatabase)])]};
            var rejected=await Assert.ThrowsAsync<ManagementException>(()=>operations.ChangeDatabaseAuthorizationAsync(invalid));Assert.Equal("OutsideDatabaseScope",rejected.Code);
            var updated=await operations.ChangeDatabaseAuthorizationAsync(new(Guid.NewGuid(),database,saved.Version){UpsertRoles=[new(roleId,"RestrictedReader",[new(database,SecurityPermission.Read,SecurityPermission.Read)])],Unassign=[new(subject,SecurityPrincipalKind.User,roleId)]});
            var latest=await operations.DatabaseAuthorizationAsync(new(database));Assert.Equal(updated.Version,latest.Version);Assert.DoesNotContain(latest.Items,item=>item.Binding?.Subject==subject);
            Assert.Contains(latest.Items,item=>item.Rule?.RoleId==roleId&&item.Rule.Rule.Deny==SecurityPermission.Read);
            await operations.ChangeDatabaseAuthorizationAsync(new(Guid.NewGuid(),database,updated.Version){RemoveRoles=[roleId]});
        }
    }

    [Fact]public async Task EditorPreservesInputOnConflictCanUndoAndEditsAssignments(){
        using var store=ManagementStore.Initialize(Root("roles-ui"),"admin","TestPassword-123");var database=Guid.NewGuid();InitializePolicy(store,database);
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);await using var operations=new ConcurrentManagementOperations(service);
        var unreliable=DispatchProxy.Create<IManagementOperations,ReplyLoss>();((ReplyLoss)(object)unreliable).Target=operations;
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(unreliable).BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestView? view=null;var dirty=false;var parameters=new Dictionary<string,object?>{{"表示中か",true},{"Capture",(Action<TestView>)(component=>view=component)},{"未保存変更時",EventCallback.Factory.Create<bool>(new object(),value=>dirty=value)}};
            var rendered=await renderer.RenderComponentAsync<TestView>(ParameterView.FromDictionary(parameters));
            var current=await operations.DatabaseAuthorizationAsync(new(database));var original=Assert.Single(current.Items,item=>item.Role is not null).Role!;
            await Invoke(view!,"選択時Async",original);Set(view!,"名前","ChangedReader");await Invoke(view!,"変更時Async");Assert.True(dirty);
            parameters["表示中か"]=false;await view!.SetParametersAsync(ParameterView.FromDictionary(parameters));parameters["表示中か"]=true;await view.SetParametersAsync(ParameterView.FromDictionary(parameters));Assert.Equal("ChangedReader",Get(view,"名前"));
            await operations.ChangeDatabaseAuthorizationAsync(new(Guid.NewGuid(),database,current.Version){UpsertRoles=[new(original.Id,"ExternalReader",[new(database,SecurityPermission.Read)])]});
            await view.保存Async();Assert.True(dirty);Assert.Equal("ChangedReader",Get(view,"名前"));Assert.Contains("保持",System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            await Invoke(view,"取消時Async");await view.一覧取得Async();Assert.False(dirty);
            var refreshed=Assert.Single((await operations.DatabaseAuthorizationAsync(new(database))).Items,item=>item.Role is not null).Role!;
            await Invoke(view,"選択時Async",refreshed);Set(view,"名前","SavedReader");Set(view,"主体認証元",Guid.NewGuid().ToString());Set(view,"主体名","group-a");Set(view,"主体種別",SecurityPrincipalKind.Group);await Invoke(view,"割当追加時Async");await view.保存Async();Assert.True(dirty);Assert.NotNull(Get(view,"未確定操作"));await view.結果照会Async();Assert.False(dirty);
            var saved=await operations.DatabaseAuthorizationAsync(new(database));Assert.Contains(saved.Items,item=>item.Role?.Name=="SavedReader");Assert.Contains(saved.Items,item=>item.Binding?.Subject.Value=="group-a"&&item.Binding.Kind==SecurityPrincipalKind.Group);
        });
    }

    [Fact]public async Task UninitializedPoliciesRequireExplicitRegistration(){
        using var store=ManagementStore.Initialize(Root("roles-empty"),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);await using var operations=new ConcurrentManagementOperations(service);
        Assert.Equal("InitializationRequired",(await Assert.ThrowsAsync<ManagementException>(()=>operations.AuthorizationDatabasesAsync(new()))).Code);Assert.Null(store.Read<StoredAuthorizationRevision>("authorization","active"));
    }
    private static void InitializePolicy(ManagementStore store,Guid database){
        var authority=Guid.NewGuid();var admin=new SecuritySubjectId(authority,"administrator");var administrator=Guid.NewGuid();var reader=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var policy=new Seamless.Security.AuthorizationPolicySnapshot(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"Server"),new(database,store.InstanceId,SecurityResourceKind.Database,"Sample")],
            [new(administrator,"ServerAdministrator",[new(store.InstanceId,SecurityPermission.All)]),new(reader,"Reader",[new(database,SecurityPermission.Read)])],[new(admin,SecurityPrincipalKind.User,administrator)]);
        using var initialized=StoredAuthorization.Initialize(store,policy,new(admin,"Administrator",[],now.AddMinutes(5),now));
    }
    public sealed class TestView:RolesView{
        [Parameter]public Action<TestView> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    public class ReplyLoss:DispatchProxy{
        public IManagementOperations Target=null!;
        private bool drop=true;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            var result=method!.Invoke(this.Target,args);
            if(method.Name==nameof(IManagementOperations.ChangeDatabaseAuthorizationAsync)){return this.HideAsync((Task<AuthorizationChangeResult>)result!);}return result;
        }
        private async Task<AuthorizationChangeResult> HideAsync(Task<AuthorizationChangeResult> operation){var result=await operation;if(this.drop){this.drop=false;throw new IOException("injected response loss after commit");}return result;}
    }
    private static string Root(string prefix)=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,prefix+"-"+Guid.NewGuid().ToString("N"));
    private static object? Get(RolesView target,string name)=>typeof(RolesView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(RolesView target,string name,object? value)=>typeof(RolesView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static Task Invoke(RolesView target,string name,params object[] args)=>(Task)typeof(RolesView).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args)!;
}
