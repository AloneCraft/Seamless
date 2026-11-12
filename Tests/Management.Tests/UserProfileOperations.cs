using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Server;
using UsersView=Seamless.Management.Components.利用者管理ビュー;

namespace Management.Tests;

public sealed class UserProfileOperations{
    [Fact]public async Task EditsUseSharedOperationsRejectStaleStateAndSurviveReopening(){
        var root=Root("users");
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
            NativeWorkflow.Publish(service,admin,Seamless.Management.DatabaseDesigner.Empty("sample","Sample"));
            await using var concurrent=new ConcurrentManagementOperations(service);await Check(concurrent,"concurrent-user");
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);await Check(ipc,"ipc-user");
            using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:24502/"));
            var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24502");
            var browser=new BrowserManagementOperations(sessions,concurrent,()=>Task.FromResult<string?>(session.Id));await Check(browser,"browser-user");
            sessions.RevokeAll();Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.UserProfilesAsync())).Code);
            service.ChangeUser(admin,new("editor","TestPassword-123",[new("*",ManagementPermission.All)]));
            using var userSessions=new UserBrowserSessions(service);var userSession=userSessions.SignIn(new("editor","TestPassword-123"));
            IManagementOperations authenticated=new AuthenticatedManagementOperations(concurrent,userSessions,()=>Task.FromResult<string?>(userSession.Id));
            await Check(authenticated,"authenticated-user");
            var self=Assert.Single((await authenticated.UserProfilesAsync()).Items,item=>item.User.Name=="editor");
            Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.EditUserProfileAsync(new(new("editor",null,[],true),self.Stamp)))).Code);
            service.ChangeUser(admin,new("editor",null,[new("Sample",ManagementPermission.View)]));
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.UserProfilesAsync())).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.EditUserProfileAsync(new(new("operator","TestPassword-456",[]),CreateOnly:true)))).Code);
            service.ChangeUser(admin,new("operator","TestPassword-789",[new("Sample",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.WriteRows)]));
            var current=Assert.Single((await concurrent.UserProfilesAsync()).Items,item=>item.User.Name=="operator");
            var login=service.Login(new("operator","TestPassword-789"));
            await concurrent.EditUserProfileAsync(new(new("operator","ChangedPassword-789",current.User.Grants),current.Stamp));
            Assert.Throws<ManagementException>(()=>service.Authenticate(login.Token));
            Assert.Throws<ManagementException>(()=>service.Login(new("operator","TestPassword-789")));
            Assert.NotNull(service.Login(new("operator","ChangedPassword-789")));
        }
        using var embedded=InProcessManagementHost.Open(root);await Check(embedded,"embedded-user");
        Assert.Contains((await embedded.UserProfilesAsync()).Items,item=>item.User.Name=="operator"&&item.User.Grants.Any(grant=>(grant.Permissions&ManagementPermission.WriteRows)!=0));

        static async Task Check(IManagementOperations operations,string name){
            Assert.Contains("UserProfilesV1",(await operations.InfoAsync()).Capabilities);
            var created=await operations.EditUserProfileAsync(new(new(name,"TestPassword-123",[new("Sample",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.WriteRows)]),CreateOnly:true));
            Assert.Equal(64,created.Stamp.Length);Assert.DoesNotContain("PasswordHash",ManagementJson.Element(created).GetRawText(),StringComparison.OrdinalIgnoreCase);
            Assert.Contains((await operations.UserProfilesAsync()).Items,item=>item.User.Name==name&&item.Stamp==created.Stamp);
            var changed=await operations.EditUserProfileAsync(new(new(name,null,[new("Sample",ManagementPermission.View|ManagementPermission.ReadRows)]),created.Stamp));
            Assert.NotEqual(created.Stamp,changed.Stamp);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.EditUserProfileAsync(new(new(name,null,[],true),created.Stamp)))).Code);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.EditUserProfileAsync(new(new(name.ToUpperInvariant(),"TestPassword-456",[]),CreateOnly:true)))).Code);
            Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>operations.EditUserProfileAsync(new(new(name,null,[]))))).Code);
            var attempts=await Task.WhenAll(Save(ManagementPermission.View),Save(ManagementPermission.ReadRows));
            var winner=Assert.Single(attempts,item=>item.Profile is not null).Profile!;Assert.Equal("Conflict",Assert.Single(attempts,item=>item.Error is not null).Error);
            var final=await operations.EditUserProfileAsync(new(new(name,null,winner.User.Grants,true),winner.Stamp));Assert.True(final.User.Disabled);
            async Task<(UserProfile? Profile,string? Error)> Save(ManagementPermission permission){
                try{return(await operations.EditUserProfileAsync(new(new(name,null,[new("Sample",permission)]),changed.Stamp)),null);}
                catch(ManagementException error){return(null,error.Code);}
            }
        }
    }
    [Fact]public async Task ProfilePagesAreBoundedAndContainEveryAccountOnce(){
        using var store=ManagementStore.Initialize(Root("user-pages"),"admin","TestPassword-123");
        var account=store.Read<UserAccount>("user",ManagementStore.Normalize("admin"))!;
        store.Write(()=>{foreach(var index in Enumerable.Range(1,205)){var user=account with{Name="user"+index.ToString("D3"),Grants=[]};store.Put("user",ManagementStore.Normalize(user.Name),user);}});
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);await using var operations=new ConcurrentManagementOperations(service);
        var names=new HashSet<string>(StringComparer.Ordinal);long cursor=0;var counts=new List<int>();
        while(true){var page=await operations.UserProfilesAsync(cursor);counts.Add(page.Items.Length);foreach(var item in page.Items){Assert.True(names.Add(item.User.Name));Assert.Equal(64,item.Stamp.Length);}if(!page.HasMore){break;}cursor=page.NextCursor;}
        Assert.Equal(new[]{100,100,6},counts);Assert.Equal(206,names.Count);
    }
    [Fact]public async Task EditorCreatesUsersPreservesConflictsAndTracksUnstoredInput(){
        using var store=ManagementStore.Initialize(Root("user-ui"),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);await using var operations=new ConcurrentManagementOperations(service);
        var original=await operations.EditUserProfileAsync(new(new("operator","TestPassword-123",[new("Sample",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.WriteRows)]),CreateOnly:true));
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(operations).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestView? view=null;var dirty=false;
            var parameters=new Dictionary<string,object?>{{"表示中か",true},{"Capture",(Action<TestView>)(component=>view=component)},{"未保存変更時",EventCallback.Factory.Create<bool>(new object(),value=>dirty=value)}};
            var rendered=await renderer.RenderComponentAsync<TestView>(ParameterView.FromDictionary(parameters));
            await Invoke(view!,"編集開始時Async",original);Set(view!,"無効か",true);await Invoke(view!,"編集変更時Async");Assert.True(dirty);
            await operations.EditUserProfileAsync(new(new("operator",null,[new("Sample",ManagementPermission.View)]),original.Stamp));
            await view!.保存Async();Assert.True((bool)Get(view,"無効か")!);Assert.True(dirty);Assert.Contains("入力は保持",Html(rendered.ToHtmlString()));
            parameters["表示中か"]=false;await view.SetParametersAsync(ParameterView.FromDictionary(parameters));Assert.True((bool)Get(view,"無効か")!);
            parameters["表示中か"]=true;await view.SetParametersAsync(ParameterView.FromDictionary(parameters));Assert.True(dirty);
            await Invoke(view,"編集取消時Async");await view.一覧取得Async(false);Assert.False(dirty);
            Assert.Equal(ManagementPermission.View,Assert.Single(((UserProfile)Get(view,"原本")!).User.Grants).Permissions);
            await Invoke(view,"新規開始時Async");Set(view,"名前","created-in-ui");Set(view,"パスワード","InitialPassword-123");await Invoke(view,"編集変更時Async");await view.保存Async();
            Assert.False(dirty);Assert.Equal("",Get(view,"パスワード"));Assert.Contains("利用者を保存しました",Html(rendered.ToHtmlString()));
            Assert.Contains((await operations.UserProfilesAsync()).Items,item=>item.User.Name=="created-in-ui");
        });
    }
    public sealed class TestView:UsersView{
        [Parameter]public Action<TestView> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    private static string Root(string prefix)=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,prefix+"-"+Guid.NewGuid().ToString("N"));
    private static string Html(string value)=>System.Net.WebUtility.HtmlDecode(value);
    private static object? Get(UsersView target,string name)=>typeof(UsersView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(UsersView target,string name,object? value)=>typeof(UsersView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static Task Invoke(UsersView target,string name,params object[] args)=>(Task)typeof(UsersView).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args)!;
}
