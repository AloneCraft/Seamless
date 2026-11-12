using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Seamless.Management;
using Seamless.Management.Server;
using Host=Seamless.Management.Server.ManagementApplicationHost;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ManagementApplicationHostTests{
    [Fact]public async Task EmbeddedSettingsAndRealReadAuditUseTheDbThreadAndSurviveReopening(){
        var root=Root("embedded-host");var threads=new ConcurrentQueue<int>();int firstThread;
        var options=new ManagementHostOptions(root,ManagementExecutionMode.Embedded){Defaults=new(){ReadAuditSaveSeconds=0.05}};
        await using(var application=await Host.CreateAsync(options)){
            var embedded=application.Embedded!;firstThread=embedded.ExecutionThreadId;Assert.Equal(Environment.ProcessId,application.ProcessId);Assert.Equal(application.ProcessId,application.Manager.ProcessId);Assert.Equal(0,application.TcpPort);Assert.Null(application.PipeName);
            var settings=await application.Operations.ServerSettingsAsync();Assert.Equal(ManagementExecutionMode.Embedded,settings.ExecutionMode);Assert.Contains(nameof(ManagementServerSettings.Port),settings.UnavailableProperties);
            Assert.Equal("ExecutionMode",(await Assert.ThrowsAsync<ManagementException>(()=>application.Operations.SaveServerSettingsAsync(new(settings.Version,settings.Next with{Port=0})))).Code);
            embedded.Invoke((service,user)=>{
                service.ReadAuditPersisting=threads.Enqueue;NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
                NativeWorkflow.Publish(service,user,new("count","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Count;}")],[])));
                Assert.Equal(0,Assert.Single(service.Execute(user,new("count",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());return true;
            });
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while((await application.Operations.ReadAuditStatusAsync(deadline.Token)).SavedCount<1){await Task.Delay(10,deadline.Token);}
            Assert.All(threads,id=>Assert.Equal(firstThread,id));Assert.NotEmpty(threads);
            var saved=await application.Operations.SaveServerSettingsAsync(new(settings.Version,settings.Next with{ReadAuditWindowSeconds=120,ReadAuditSaveSeconds=3600}));Assert.True(saved.RestartRequired);
            var launch=application.Manager.Sessions.CreateLaunch();var session=application.Manager.Sessions.Redeem(new Uri(launch.Url).Fragment[1..],application.Manager.Origin.GetLeftPart(UriPartial.Authority));
            IManagementOperations browser=new BrowserManagementOperations(application.Manager.Sessions,embedded,()=>Task.FromResult<string?>(session.Id));
            Assert.Equal(saved.Version,(await browser.ServerSettingsAsync()).Version);Assert.Equal(embedded.Info.InstanceId,(await browser.InfoAsync()).InstanceId);
        }
        var secondThreads=new ConcurrentQueue<int>();int secondThread;
        await using(var application=await Host.OpenAsync(options)){
            var embedded=application.Embedded!;secondThread=embedded.ExecutionThreadId;
            Assert.Equal(120,application.RunningSettings.ReadAuditWindowSeconds);Assert.Equal(3600,application.RunningSettings.ReadAuditSaveSeconds);
            Assert.False((await application.Operations.ServerSettingsAsync()).RestartRequired);
            embedded.Invoke((service,user)=>{service.ReadAuditPersisting=secondThreads.Enqueue;Assert.Equal(0,Assert.Single(service.Execute(user,new("count",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());return true;});
            Assert.Equal(0,(await application.Operations.ReadAuditStatusAsync()).SavedCount);
        }
        Assert.Single(secondThreads);Assert.All(secondThreads,id=>Assert.Equal(secondThread,id));
        using var reopened=new ManagementStore(root);Assert.Equal(2,reopened.ReadSummaries(new()).Items.Sum(item=>item.Count));
    }

    [Fact]public async Task ServerHostSharesTheManagerIpcTlsAndSavedStartupSettings(){
        var directory=Root("server-host");var root=Path.Combine(directory,"db");var config=Path.Combine(directory,"launch.json");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","SetupPassword-123",config);
        var options=new ManagementHostOptions(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0}};int managerPort;
        await using(var application=await Host.OpenAsync(options)){
            Assert.Null(application.Embedded);Assert.Equal(Environment.ProcessId,application.ProcessId);Assert.Equal(application.ProcessId,application.Manager.ProcessId);Assert.True(application.TcpPort>0);
            using var ipc=ManagementClient.ForLocalPipe(application.PipeName!);var info=await ipc.InfoAsync();Assert.Equal((await application.Operations.InfoAsync()).InstanceId,info.InstanceId);
            using var pin=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
            using var tcp=new ManagementClient("localhost",application.TcpPort,pin);
            await Assert.ThrowsAsync<ManagementException>(()=>tcp.DatabasesAsync());await tcp.LoginAsync("admin","SetupPassword-123");
            var requests=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>tcp.DatabasesAsync()));Assert.All(requests,page=>Assert.True(Assert.Single(page.Items).IsSystem));
            managerPort=application.Manager.Origin.Port;var original=await ipc.ServerSettingsAsync();
            await ipc.SaveServerSettingsAsync(new(original.Version,original.Next with{ManagerPort=managerPort,ReadAuditWindowSeconds=120,MaximumPendingWriteBytes=1048576}));
        }
        Assert.Null(Seamless.Management.LocalServerConnection.Read(root));Assert.Null(LocalManagerConnection.Read(root));
        await using(var application=await Host.OpenAsync(options)){
            Assert.Equal(managerPort,application.Manager.Origin.Port);Assert.Equal(1048576,application.RunningSettings.MaximumPendingWriteBytes);Assert.Equal(120,(await application.Operations.ReadAuditStatusAsync()).WindowSeconds);
        }
        using var reopened=new ManagementStore(root);Assert.Equal(1,Seamless.Management.Server.ManagementService.SavedServerSettings(reopened)!.Version);
    }

    [Fact]public async Task StartupFailureAndCancellationReleaseTheOwnedStoreAndListeners(){
        var root=Root("failed-embedded");using var occupied=new TcpListener(IPAddress.Loopback,0);occupied.Start();var port=((IPEndPoint)occupied.LocalEndpoint).Port;
        await Assert.ThrowsAnyAsync<Exception>(()=>Host.CreateAsync(new(root,ManagementExecutionMode.Embedded){Defaults=new(){ManagerPort=port}}));
        using(var reopened=new ManagementStore(root)){Assert.NotEqual(Guid.Empty,reopened.InstanceId);}
        await using(var recovered=await Host.OpenAsync(new(root,ManagementExecutionMode.Embedded))){Assert.True(recovered.Manager.Origin.Port>0);}
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Host.OpenAsync(new(root,ManagementExecutionMode.Embedded),cancellation.Token));
        using var stillOpenable=new ManagementStore(root);Assert.NotEqual(Guid.Empty,stillOpenable.InstanceId);
    }
    [Fact]public async Task AConnectionFileCleanupFailureStillClosesTheOwnedStore(){
        var directory=Root("host-cleanup");var root=Path.Combine(directory,"db");
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","SetupPassword-123",Path.Combine(directory,"launch.json"));
        var application=await Host.OpenAsync(new(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0}});
        try{
            using var locked=new FileStream(Path.Combine(root,LocalManagerConnection.FileName),FileMode.Open,FileAccess.Read,FileShare.None);
            var error=await Assert.ThrowsAsync<AggregateException>(()=>application.DisposeAsync().AsTask());Assert.Contains(error.InnerExceptions,item=>item is IOException);
            using var reopened=new ManagementStore(root);Assert.NotEqual(Guid.Empty,reopened.InstanceId);
        }finally{try{await application.DisposeAsync();}catch(AggregateException){}}
    }
    private static string Root(string prefix)=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,prefix+"-"+Guid.NewGuid().ToString("N"));
    internal static async Task<int> RunEmbeddedProbeAsync(string root,string control){
        await using var host=await Host.OpenAsync(new(root,ManagementExecutionMode.Embedded));
        var launch=host.Manager.Sessions.CreateLaunch();await File.WriteAllTextAsync(Path.Combine(control,"launch.txt"),launch.Url);
        await File.WriteAllTextAsync(Path.Combine(control,"ready.json"),System.Text.Json.JsonSerializer.Serialize(new{host.ProcessId,host.Embedded!.ExecutionThreadId,InstanceId=host.Embedded.Info.InstanceId,ManagerOrigin=host.Manager.Origin.AbsoluteUri},ManagementJson.Options));
        while(!File.Exists(Path.Combine(control,"stop"))){await Task.Delay(100);}return 0;
    }
}
