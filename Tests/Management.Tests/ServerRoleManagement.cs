using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Authentication;
using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Security;
using RolesView=Seamless.Management.Components.ロール管理ビュー;
using Policy=Seamless.Security.AuthorizationPolicySnapshot;

namespace Management.Tests;

public sealed class ServerRoleManagement{
    [Fact]public Task OidcServerScopeRpcRequiresServerPermission()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        var first=Guid.NewGuid();var second=Guid.NewGuid();var admin=Identity(issuer.Id,"admin");var editor=Identity(issuer.Id,"editor");var administrator=Guid.NewGuid();var dbRole=Guid.NewGuid();
        using var policy=StoredAuthorization.Initialize(store,new(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"Server"),new(first,store.InstanceId,SecurityResourceKind.Database,"First"),new(second,store.InstanceId,SecurityResourceKind.Database,"Second")],
            [new(administrator,"Admin",[new(store.InstanceId,SecurityPermission.All)]),new(dbRole,"DbAdmin",[new(first,SecurityPermission.ManagePermissions)])],[new(admin.Subject,SecurityPrincipalKind.User,administrator),new(editor.Subject,SecurityPrincipalKind.User,dbRole)]),admin,issuer.Settings);
        using var verifier=new OidcTokenVerifier(issuer.Settings);await using var server=Seamless.Management.Server.ManagementServer.ForAuthorization(policy,verifier,certificate,new(System.Net.IPAddress.Loopback,0));server.Start();
        using var client=new ManagementClient("localhost",server.Port,certificate);using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));var cancellation=deadline.Token;
        var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,admin.Subject.Value,scope:"seamless.database");var limited=issuer.CreateToken(OidcDevelopmentIssuer.Audience,editor.Subject.Value,scope:"seamless.database");
        Assert.Contains("AuthorizationScopesV1",(await client.InfoAsync(cancellation)).Capabilities);
        Assert.Equal("NotGranted",(await Assert.ThrowsAsync<ManagementException>(()=>client.AuthorizationScopeAsync(limited,new(store.InstanceId),cancellation))).Code);
        var before=await client.AuthorizationScopeAsync(token,new(store.InstanceId),cancellation);var role=Guid.NewGuid();
        var edit=new AuthorizationScopeEdit(Guid.NewGuid(),store.InstanceId,before.Version){UpsertRoles=[new(role,"AcrossDatabases",[new(first,SecurityPermission.Read),new(second,SecurityPermission.Read)])]};
        Assert.Equal("NotGranted",(await Assert.ThrowsAsync<ManagementException>(()=>client.ChangeAuthorizationScopeAsync(limited,edit,cancellation))).Code);
        Assert.Equal(before.Version,(await client.AuthorizationScopeAsync(token,new(store.InstanceId),cancellation)).Version);
        var saved=await client.ChangeAuthorizationScopeAsync(token,edit,cancellation);Assert.Null(saved.DatabaseId);
        Assert.Contains((await client.AuthorizationScopeAsync(token,new(store.InstanceId),cancellation)).Items,item=>item.Role?.Id==role);
        Assert.Equal(saved,await client.AuthorizationChangeResultAsync(token,edit.OperationId,cancellation));
        Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>client.DatabaseAuthorizationAsync(token,store.InstanceId,cancellationToken:cancellation))).Code);
    });

    [Fact]public void ScopeChangesEnforceServerAuthorityAndDatabaseBoundaries(){
        var server=Guid.NewGuid();var first=Guid.NewGuid();var second=Guid.NewGuid();var authority=Guid.NewGuid();var admin=Identity(authority,"admin");var editor=Identity(authority,"editor");
        var adminRole=Guid.NewGuid();var editorRole=Guid.NewGuid();var resources=new SecurityResource[]{new(server,null,SecurityResourceKind.Server,"Server"),new(first,server,SecurityResourceKind.Database,"First"),new(second,server,SecurityResourceKind.Database,"Second")};
        var policy=new Policy(1,resources,[new(adminRole,"Admin",[new(server,SecurityPermission.All)]),new(editorRole,"DbEditor",[new(first,SecurityPermission.ManagePermissions)])],[new(admin.Subject,SecurityPrincipalKind.User,adminRole),new(editor.Subject,SecurityPrincipalKind.User,editorRole)]);
        using var manager=new AuthorizationManager(policy);using var adminSession=manager.CreateSession(admin);using var adminOperation=adminSession.BeginOperation();using var editorSession=manager.CreateSession(editor);using var editorOperation=editorSession.BeginOperation();
        var role=Guid.NewGuid();var cross=new SecurityRole(role,"CrossDatabase",[new(first,SecurityPermission.Read),new(second,SecurityPermission.Update)]);
        Assert.Equal("NotGranted",Assert.Throws<AuthorizationException>(()=>manager.ApplyRoleChange(editorOperation,new(server,1,[cross]))).Code);
        Assert.Equal("OutsideDatabaseScope",Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(adminOperation,new(first,1,[cross]))).Code);
        Assert.Equal("UnknownTargetOrOperation",Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(adminOperation,new(server,1,[cross]))).Code);
        var next=manager.ApplyRoleChange(adminOperation,new(server,1,[cross],assign:[new(editor.Subject,SecurityPrincipalKind.User,role)]));Assert.Equal(2,next.Version);
        using(var updated=editorSession.BeginOperation()){updated.Demand(first,SecurityPermission.Read);updated.Demand(second,SecurityPermission.Update);Assert.Throws<AuthorizationException>(()=>updated.Demand(server,SecurityPermission.ManageServer));}
        Assert.Equal("Conflict",Assert.Throws<AuthorizationException>(()=>manager.ApplyRoleChange(adminOperation,new(server,1,[cross]))).Code);
        Assert.Throws<ArgumentException>(()=>manager.ApplyRoleChange(adminOperation,new(server,2,[new(Guid.NewGuid(),"Invalid",[new(first,SecurityPermission.ManageServer)])])));Assert.Equal(2,manager.Policy.Version);
        manager.ApplyRoleChange(adminOperation,new(server,2,[new(Guid.NewGuid(),"Empty",[])]));
        manager.ApplyRoleChange(adminOperation,new(server,3,removeRoles:[role]));using var removed=editorSession.BeginOperation();Assert.Throws<AuthorizationException>(()=>removed.Demand(second,SecurityPermission.Update));
    }

    [Fact]public async Task SharedEntriesPersistServerAndCrossDatabaseRoles(){
        var root=Root();var first=Guid.NewGuid();var second=Guid.NewGuid();var retained=Guid.NewGuid();Guid instance;AuthorizationChangeResult retainedResult;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            instance=store.InstanceId;Initialize(store,first,second);
            await using var operations=new ConcurrentManagementOperations(service);await Check(operations,instance,first,second);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);await Check(ipc,instance,first,second);
            using var sessions=new LocalBrowserSessions(instance);sessions.BindOrigin(new("http://127.0.0.1:24508/"));var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24508");
            var browser=new BrowserManagementOperations(sessions,operations,()=>Task.FromResult<string?>(session.Id));await Check(browser,instance,first,second);
            sessions.RevokeAll();Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.AuthorizationScopeAsync(new(instance)))).Code);
            var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);service.ChangeUser(admin,new("editor","TestPassword-123",[new("*",ManagementPermission.All)]));
            using var users=new UserBrowserSessions(service);var user=users.SignIn(new("editor","TestPassword-123"));var authenticated=new AuthenticatedManagementOperations(operations,users,()=>Task.FromResult<string?>(user.Id));await Check(authenticated,instance,first,second);
            service.ChangeUser(admin,new("editor",null,[new("First",ManagementPermission.Manage)]));
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.AuthorizationScopeAsync(new(instance)))).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.ChangeAuthorizationScopeAsync(new(Guid.NewGuid(),instance,1)))).Code);
            var beforeClose=await operations.AuthorizationScopeAsync(new(instance));
            retainedResult=await operations.ChangeAuthorizationScopeAsync(new(Guid.NewGuid(),instance,beforeClose.Version){UpsertRoles=[new(retained,"RetainedAcrossDatabases",[new(first,SecurityPermission.Read),new(second,SecurityPermission.Update,SecurityPermission.Delete)])]});
        }
        using(var embedded=InProcessManagementHost.Open(root)){
            var recovered=await embedded.AuthorizationScopeAsync(new(instance));Assert.Equal(retainedResult.Version,recovered.Version);Assert.Contains(recovered.Items,item=>item.Role?.Id==retained&&item.Role.Name=="RetainedAcrossDatabases");
            Assert.Contains(recovered.Items,item=>item.Rule?.RoleId==retained&&item.Rule.Rule.ResourceId==second&&item.Rule.Rule.Allow==SecurityPermission.Update&&item.Rule.Rule.Deny==SecurityPermission.Delete);
            Assert.Equal(retainedResult,await embedded.AuthorizationChangeResultAsync(retainedResult.OperationId));await Check(embedded,instance,first,second);
        }
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(root)!,"server-roles-fixture.json"),JsonSerializer.Serialize(new{Root=root,Instance=instance,First=first,Second=second},ManagementJson.Options));
        static async Task Check(IManagementOperations operations,Guid server,Guid first,Guid second){
            Assert.Contains("AuthorizationScopesV1",(await operations.InfoAsync()).Capabilities);var original=await operations.AuthorizationScopeAsync(new(server));Assert.Contains(original.Items,item=>item.Resource?.Id==second);
            var role=Guid.NewGuid();var subject=new SecuritySubjectId(Guid.NewGuid(),"cross-user");
            var edit=new AuthorizationScopeEdit(Guid.NewGuid(),server,original.Version){UpsertRoles=[new(role,"CrossDatabase",[new(first,SecurityPermission.Read),new(second,SecurityPermission.Update,SecurityPermission.Delete)])],Assign=[new(subject,SecurityPrincipalKind.Group,role)]};
            var saved=await operations.ChangeAuthorizationScopeAsync(edit);Assert.Null(saved.DatabaseId);Assert.Equal(saved,await operations.AuthorizationChangeResultAsync(edit.OperationId));
            var after=await operations.AuthorizationScopeAsync(new(server));Assert.Contains(after.Items,item=>item.Role?.Id==role);Assert.Contains(after.Items,item=>item.Binding?.Subject==subject);
            Assert.DoesNotContain((await operations.DatabaseAuthorizationAsync(new(first))).Items,item=>item.Role?.Id==role);
            Assert.Equal("OutsideDatabaseScope",(await Assert.ThrowsAsync<ManagementException>(()=>operations.ChangeDatabaseAuthorizationAsync(new(Guid.NewGuid(),first,saved.Version){RemoveRoles=[role]}))).Code);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.ChangeAuthorizationScopeAsync(edit with{OperationId=Guid.NewGuid()}))).Code);
            await operations.ChangeAuthorizationScopeAsync(new(Guid.NewGuid(),server,saved.Version){UpsertRoles=[new(role,"ServerOperator",[new(server,SecurityPermission.ManageServer)])],Unassign=edit.Assign});
            var current=await operations.AuthorizationScopeAsync(new(server));Assert.DoesNotContain(current.Items,item=>item.Binding?.RoleId==role);
            await operations.ChangeAuthorizationScopeAsync(new(Guid.NewGuid(),server,current.Version){RemoveRoles=[role]});
        }
    }

    [Fact]public async Task ServerEditorUsesSharedControlsForMultipleDatabasesAndServerPermissions(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");var first=Guid.NewGuid();var second=Guid.NewGuid();Initialize(store,first,second);
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);await using var operations=new ConcurrentManagementOperations(service);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(operations).BuildServiceProvider();await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            RoleManagementOperations.TestView? view=null;var rendered=await renderer.RenderComponentAsync<RoleManagementOperations.TestView>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"表示中か",true},{"Capture",(Action<RoleManagementOperations.TestView>)(value=>view=value)}}));
            await Invoke(view!,"DB変更時Async",new ChangeEventArgs{Value=store.InstanceId.ToString()});Assert.Contains("サーバー全体",Html(rendered.ToHtmlString()));
            await Invoke(view!,"新規時Async");Set(view!,"名前","AcrossDatabases");var rules=(IList)Get(view!,"規則")!;var firstRule=rules[0]!;SetRule(firstRule,"資源ID",first);SetRule(firstRule,"許可",SecurityPermission.Read);
            await Invoke(view!,"規則追加時Async");var secondRule=rules[1]!;SetRule(secondRule,"資源ID",second);SetRule(secondRule,"許可",SecurityPermission.Update);
            Set(view!,"主体認証元",Guid.NewGuid().ToString());Set(view!,"主体名","operators");await Invoke(view!,"割当追加時Async");await view!.保存Async();
            Assert.Contains("ロールを保存しました",Html(rendered.ToHtmlString()));var saved=await operations.AuthorizationScopeAsync(new(store.InstanceId));var role=Assert.Single(saved.Items,item=>item.Role?.Name=="AcrossDatabases").Role!;
            Assert.Equal(2,saved.Items.Count(item=>item.Rule?.RoleId==role.Id));await Invoke(view,"選択時Async",role);
            SetRule(((IList)Get(view,"規則")!)[0]!,"資源ID",store.InstanceId);await Invoke(view,"変更時Async");await view.SetParametersAsync(ParameterView.Empty);Assert.Contains("サーバー管理を許可",Html(rendered.ToHtmlString()));
            Set(view,"名前","Unsaved");await Invoke(view,"変更時Async");await Invoke(view,"DB変更時Async",new ChangeEventArgs{Value=first.ToString()});Assert.Equal(store.InstanceId,Get(view,"DBID"));await Invoke(view,"取消時Async");
            await Invoke(view,"削除時Async");Assert.DoesNotContain((await operations.AuthorizationScopeAsync(new(store.InstanceId))).Items,item=>item.Role?.Id==role.Id);
        });
    }

    private static void Initialize(ManagementStore store,Guid first,Guid second){
        var identity=Identity(Guid.NewGuid(),"bootstrap");var role=Guid.NewGuid();
        using var policy=StoredAuthorization.Initialize(store,new(1,[new(store.InstanceId,null,SecurityResourceKind.Server,"Server"),new(first,store.InstanceId,SecurityResourceKind.Database,"First"),new(second,store.InstanceId,SecurityResourceKind.Database,"Second")],[new(role,"ServerAdministrator",[new(store.InstanceId,SecurityPermission.All)])],[new(identity.Subject,SecurityPrincipalKind.User,role)]),identity);
    }
    private static AuthenticatedIdentity Identity(Guid authority,string subject){var now=DateTimeOffset.UtcNow;return new(new(authority,subject),subject,[],now.AddHours(1),now);}
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"server-roles-"+Guid.NewGuid().ToString("N"));
    private static string Html(string value)=>System.Net.WebUtility.HtmlDecode(value);
    private static object? Get(RolesView target,string name)=>typeof(RolesView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(RolesView target,string name,object value)=>typeof(RolesView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static void SetRule(object rule,string name,object value)=>rule.GetType().GetField(name)!.SetValue(rule,value);
    private static Task Invoke(RolesView target,string name,params object[] args)=>(Task)typeof(RolesView).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args)!;
}
