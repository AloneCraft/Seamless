using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Seamless.Management;
using Seamless.Management.Server;
using SettingsView=Seamless.Management.Components.サーバー設定ビュー;
using ServerProgram=Seamless.Management.Server.Program;

namespace Management.Tests;

public sealed class ServerSettingsOperations{
    [Fact]public async Task SharedOperationsValidateAuthorizeRejectConflictsAndPreserveOverrides(){
        var root=Root("settings");var bootstrap=new ManagementServerSettings();var running=bootstrap with{Port=0};
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            service.AttachServerSettings(new(bootstrap,running,[nameof(ManagementServerSettings.Port)],"certificate.pfx",null,12345,"http://127.0.0.1:24503/"));
            await using var operations=new ConcurrentManagementOperations(service);await Check(operations);
            await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var ipc=ManagementClient.ForLocalPipe(server.PipeName!);await Check(ipc);
            using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:24503/"));
            var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24503");
            IManagementOperations browser=new BrowserManagementOperations(sessions,operations,()=>Task.FromResult<string?>(session.Id));await Check(browser);
            sessions.RevokeAll();Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.ServerSettingsAsync())).Code);
            var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
            service.ChangeUser(admin,new("editor","TestPassword-123",[new("*",ManagementPermission.All)]));
            using var userSessions=new UserBrowserSessions(service);var userSession=userSessions.SignIn(new("editor","TestPassword-123"));
            IManagementOperations authenticated=new AuthenticatedManagementOperations(operations,userSessions,()=>Task.FromResult<string?>(userSession.Id));await Check(authenticated);
            var current=await operations.ServerSettingsAsync();
            service.ChangeUser(admin,new("editor",null,[new("Sample",ManagementPermission.View)]));
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.ServerSettingsAsync())).Code);
            Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>authenticated.SaveServerSettingsAsync(new(current.Version,current.Next)))).Code);
            Assert.Equal(14336,Seamless.Management.Server.ManagementService.SavedServerSettings(store)!.Settings.Port);
        }
        using var embedded=InProcessManagementHost.Open(root);
        Assert.DoesNotContain("ServerSettingsV1",(await embedded.InfoAsync()).Capabilities);
        Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>embedded.ServerSettingsAsync())).Code);
        embedded.Invoke((service,_)=>{service.AttachServerSettings(new(bootstrap,running,[nameof(ManagementServerSettings.Port)],"certificate.pfx",null,12345,"http://127.0.0.1:24503/"));return true;});
        await Check(embedded);

        static async Task Check(IManagementOperations operations){
            Assert.Contains("ServerSettingsV1",(await operations.InfoAsync()).Capabilities);
            var before=await operations.ServerSettingsAsync();
            var saved=await operations.SaveServerSettingsAsync(new(before.Version,before.Next with{ReadAuditWindowSeconds=before.Next.ReadAuditWindowSeconds+1,MaximumPendingWriteBytes=1048576}));
            Assert.Equal(before.Version+1,saved.Version);Assert.Equal(before.Running,saved.Running);Assert.True(saved.RestartRequired);
            Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveServerSettingsAsync(new(before.Version,before.Next)))).Code);
            Assert.Equal("ConfigurationOverride",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveServerSettingsAsync(new(saved.Version,saved.Next with{Port=1000})))).Code);
            foreach(var invalid in new[]{saved.Next with{Address="invalid"},saved.Next with{ManagerPort=65536},saved.Next with{MaximumPendingWriteBytes=0},saved.Next with{ResponseWriteTimeout=TimeSpan.Zero},saved.Next with{ReadAuditSaveSeconds=0},saved.Next with{ExternalManagerOrigin="http://example.test/"}}){
                Assert.Equal("Validation",(await Assert.ThrowsAsync<ManagementException>(()=>operations.SaveServerSettingsAsync(new(saved.Version,invalid)))).Code);
            }
            var after=await operations.ServerSettingsAsync();Assert.Equal(saved.Version,after.Version);Assert.Equal(saved.Next,after.Next);
            var attempts=await Task.WhenAll(Save(120),Save(121));Assert.Single(attempts,item=>item is null);Assert.Equal("Conflict",Assert.Single(attempts,item=>item is not null));
            async Task<string?> Save(int seconds){try{await operations.SaveServerSettingsAsync(new(saved.Version,saved.Next with{ReadAuditWindowSeconds=seconds}));return null;}catch(ManagementException error){return error.Code;}}
        }
    }

    [Fact]public async Task EditorPreservesDirtyInputAndConflictsAndCanUndo(){
        using var store=ManagementStore.Initialize(Root("settings-ui"),"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        service.AttachServerSettings(new(new(),new(),[],"certificate.pfx",null,14336,"http://127.0.0.1:24503/"));
        await using var operations=new ConcurrentManagementOperations(service);
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IManagementOperations>(operations).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestView? view=null;var dirty=false;
            var parameters=new Dictionary<string,object?>{{"表示中か",true},{"Capture",(Action<TestView>)(component=>view=component)},{"未保存変更時",EventCallback.Factory.Create<bool>(new object(),value=>dirty=value)}};
            var rendered=await renderer.RenderComponentAsync<TestView>(ParameterView.FromDictionary(parameters));
            var initial=await operations.ServerSettingsAsync();await Invoke(view!,"値変更時Async",initial.Next with{ManagerPort=24503});Assert.True(dirty);
            parameters["表示中か"]=false;await view!.SetParametersAsync(ParameterView.FromDictionary(parameters));
            parameters["表示中か"]=true;await view.SetParametersAsync(ParameterView.FromDictionary(parameters));Assert.True(dirty);
            await view.保存Async();Assert.False(dirty);var html=Html(rendered.ToHtmlString());Assert.Contains("次回起動時に反映",html);Assert.Contains("v1 · 再起動待ち",html);Assert.DoesNotContain("v@",html);
            var saved=await operations.ServerSettingsAsync();await Invoke(view,"値変更時Async",saved.Next with{ReadAuditWindowSeconds=120});
            await operations.SaveServerSettingsAsync(new(saved.Version,saved.Next with{ReadAuditWindowSeconds=180}));
            await view.保存Async();Assert.True(dirty);Assert.Equal(120,((ManagementServerSettings)Get(view,"編集値")!).ReadAuditWindowSeconds);Assert.Contains("入力は保持",Html(rendered.ToHtmlString()));
            await view.再取得Async();Assert.True(dirty);await Invoke(view,"取消時Async");await view.再取得Async();Assert.False(dirty);
            Assert.Equal(180,((ManagementServerSettings)Get(view,"編集値")!).ReadAuditWindowSeconds);
            Set(view,"応答期限ミリ秒",-1d);await Invoke(view,"変更通知Async");await view.保存Async();Assert.True(dirty);Assert.Contains("設定範囲外",Html(rendered.ToHtmlString()));
            await Invoke(view,"取消時Async");Assert.False(dirty);
        });
    }

    [Fact]public async Task SavedSettingsAreAppliedByTheRealServerAfterRestart(){
        var directory=Root("settings-live");var root=Path.Combine(directory,"db");var config=Path.Combine(directory,"launch.json");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","SetupPassword-123",config);
        int managerPort=0;
        for(var iteration=0;iteration<2;iteration++){
            var ready=Path.Combine(directory,$"ready{iteration}.json");var stop=Path.Combine(directory,$"stop{iteration}");
            var server=ServerProgram.Main(["serve","--config",config,"--port","0","--ready-file",ready,"--stop-file",stop]);
            try{
                using var startup=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while(!File.Exists(ready)&&!server.IsCompleted){await Task.Delay(10,startup.Token);}
                Assert.True(File.Exists(ready),"設定検証用サーバーの起動が完了しませんでした。");
                var connection=Seamless.Management.LocalServerConnection.Read(root)!;using var client=ManagementClient.ForLocalPipe(connection.PipeName!);
                var current=await client.ServerSettingsAsync();var audit=await client.ReadAuditStatusAsync();
                if(iteration==0){
                    managerPort=new Uri(current.ManagerOrigin).Port;
                    var saved=await client.SaveServerSettingsAsync(new(current.Version,current.Next with{ManagerPort=managerPort,ReadAuditWindowSeconds=120,ReadAuditSaveSeconds=10,MaximumPendingWriteBytes=1048576}));
                    Assert.True(saved.RestartRequired);Assert.Equal(0,saved.Running.ManagerPort);Assert.Equal(60,audit.WindowSeconds);Assert.Equal(5,audit.SaveIntervalSeconds);
                }else{
                    Assert.False(current.RestartRequired);Assert.Equal(managerPort,current.Running.ManagerPort);Assert.Equal(managerPort,new Uri(current.ManagerOrigin).Port);
                    Assert.Equal(1048576,current.Running.MaximumPendingWriteBytes);Assert.Equal(120,audit.WindowSeconds);Assert.Equal(10,audit.SaveIntervalSeconds);
                }
            }finally{File.WriteAllText(stop,"stop");Assert.Equal(0,await server.WaitAsync(TimeSpan.FromSeconds(15)));}
        }
        using var reopened=new ManagementStore(root);var persisted=Seamless.Management.Server.ManagementService.SavedServerSettings(reopened)!;
        Assert.Equal(14336,persisted.Settings.Port);Assert.Equal(managerPort,persisted.Settings.ManagerPort);
        Assert.Contains(reopened.History(0,100,static _=>true).Items,item=>item.Action=="ServerSettingsChanged");
    }

    public sealed class TestView:SettingsView{
        [Parameter]public Action<TestView> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
    }
    private static string Root(string prefix)=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,prefix+"-"+Guid.NewGuid().ToString("N"));
    private static string Html(string value)=>System.Net.WebUtility.HtmlDecode(value);
    private static object? Get(SettingsView target,string name)=>typeof(SettingsView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(SettingsView target,string name,object? value)=>typeof(SettingsView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static Task Invoke(SettingsView target,string name,params object[] args)=>(Task)typeof(SettingsView).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args)!;
}
