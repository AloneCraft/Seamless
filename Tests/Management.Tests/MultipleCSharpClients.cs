using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Host=Seamless.Management.Server.ManagementApplicationHost;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class MultipleCSharpClients{
    [Fact]public async Task IndependentClientsCommitToTheManagersDatabaseAndDisconnectRollsBack(){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"clients-"+Guid.NewGuid().ToString("N"));
        var root=Path.Combine(directory,"db");var control=Path.Combine(directory,"control");Directory.CreateDirectory(control);
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","ClientTestPassword-123",Path.Combine(directory,"launch.json"));
        using(var store=new ManagementStore(root))using(var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false)){
            var user=service.Authenticate(service.Login(new("admin","ClientTestPassword-123")).Token);
            NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
            NativeWorkflow.Publish(service,user,Query("insert","int","context.GetDatabase<Db>().Rows.Add(new Records.Row(id,id*10));return context.GetDatabase<Db>().Rows.Count;",true));
            NativeWorkflow.Publish(service,user,Query("count","int","return context.GetDatabase<Db>().Rows.Count;",false));
            NativeWorkflow.Publish(service,user,Query("disconnect","System.Collections.Generic.IEnumerable<string>","context.GetDatabase<Db>().Rows.Add(new Records.Row(id,id*10));for(var index=0;index<10000;index++){context.CancellationToken.ThrowIfCancellationRequested();yield return new string('x',32768);}",true));
        }
        var options=new ManagementHostOptions(root,ManagementExecutionMode.Server){Certificate=Path.Combine(root,"server.pfx"),Defaults=new(){Port=0},ConcurrentCapacity=4};
        var children=new List<Child>();
        await using(var application=await Host.OpenAsync(options)){
            try{
                Assert.Equal(Environment.ProcessId,application.Manager.ProcessId);
                var instance=(await application.Operations.InfoAsync()).InstanceId;
                for(var worker=0;worker<4;worker++){children.Add(StartClient(root,application.TcpPort,control,worker,"write"));}
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
                foreach(var child in children){await WaitReady(child,control,deadline.Token);}
                var ready=children.Select(child=>ManagementJson.Read<ClientReady>(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(control,child.Process.Id+".ready"))))).ToArray();
                Assert.Equal(4,ready.Select(item=>item.ProcessId).Distinct().Count());
                Assert.All(ready,item=>{Assert.NotEqual(application.ProcessId,item.ProcessId);Assert.Equal(instance,item.InstanceId);Assert.InRange(System.Numerics.BitOperations.PopCount(unchecked((ulong)item.Affinity)),1,8);});
                Assert.Single(ready.Select(item=>item.Affinity).Distinct());
                var launch=application.Manager.Sessions.CreateLaunch();var session=application.Manager.Sessions.Redeem(new Uri(launch.Url).Fragment[1..],application.Manager.Origin.GetLeftPart(UriPartial.Authority));
                IManagementOperations browser=new BrowserManagementOperations(application.Manager.Sessions,application.Operations,()=>Task.FromResult<string?>(session.Id));
                File.WriteAllText(Path.Combine(control,"go"),"");
                var during=browser.ReadRowsAsync(new("Native","Rows"),deadline.Token);
                var results=new List<ClientResult>();
                foreach(var child in children){results.Add(await Finish(child,deadline.Token));}
                Assert.Equal(Enumerable.Range(1,20),results.SelectMany(item=>item.Counts).Order());
                await during;
                var rows=await browser.ReadRowsAsync(new("Native","Rows"),deadline.Token);Assert.Equal(20,rows.Rows.Length);
                Assert.Equal(Enumerable.Range(0,4).SelectMany(worker=>Enumerable.Range(1,5).Select(id=>worker*100+id)).Order(),rows.Rows.Select(row=>row.Values.GetProperty("id").GetInt32()).Order());
                var interrupted=StartClient(root,application.TcpPort,control,9,"disconnect");children.Add(interrupted);
                await WaitReady(interrupted,control,deadline.Token);var cancelled=await Finish(interrupted,deadline.Token);Assert.Equal(1,cancelled.ReceivedRows);
                using var pin=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
                using var client=new ManagementClient("localhost",application.TcpPort,pin);await client.LoginAsync("admin","ClientTestPassword-123",deadline.Token);
                var count=await Execute(client,"count",0,deadline.Token);Assert.Equal(20,count);
                Assert.Equal(20,(await browser.ReadRowsAsync(new("Native","Rows"),deadline.Token)).Rows.Length);
                await File.WriteAllTextAsync(Path.Combine(directory,"clients-evidence.json"),JsonSerializer.Serialize(new{ServerProcessId=application.ProcessId,ManagerProcessId=application.Manager.ProcessId,InstanceId=instance,Clients=ready,Results=results,Interrupted=cancelled,FinalCount=count},ManagementJson.Options));
            }finally{
                foreach(var child in children){
                    try{if(!child.Process.HasExited){Assert.Equal(child.Created,child.Process.StartTime.ToUniversalTime());child.Process.Kill(entireProcessTree:true);await child.Process.WaitForExitAsync();}}
                    finally{child.Process.Dispose();}
                }
            }
        }
        await using var reopened=await Host.OpenAsync(options);
        Assert.Equal(20,(await reopened.Operations.ReadRowsAsync(new("Native","Rows"))).Rows.Length);
    }

    private static Definition Query(string id,string result,string body,bool writes)=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static "+result+" Run(ManagementExecutionContext context,int id){"+body+"}}")],[]),writes);
    private static async Task<int> Execute(ManagementClient client,string query,int id,CancellationToken cancellation){
        var count=0;var result=0;
        await foreach(var row in client.ExecuteAsync("Native",new(query,null,ManagementJson.Element(new{id})),cancellation)){result=row.Value.GetInt32();count++;}
        Assert.Equal(1,count);return result;
    }
    private sealed record ClientReady(int ProcessId,Guid InstanceId,long Affinity);
    private sealed record ClientResult(int ProcessId,int[] Counts,int ReceivedRows);
    private sealed record Child(Process Process,DateTime Created,Task<string> Output,Task<string> Error);
    private static Child StartClient(string root,int port,string control,int worker,string mode){
        var executable=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--business-client-probe",root,port.ToString(System.Globalization.CultureInfo.InvariantCulture),control,worker.ToString(System.Globalization.CultureInfo.InvariantCulture),mode}){start.ArgumentList.Add(argument);}
        var process=Process.Start(start)!;return new(process,process.StartTime.ToUniversalTime(),process.StandardOutput.ReadToEndAsync(),process.StandardError.ReadToEndAsync());
    }
    private static async Task WaitReady(Child child,string control,CancellationToken cancellation){
        while(!File.Exists(Path.Combine(control,child.Process.Id+".ready"))){
            Assert.False(child.Process.HasExited,"接続準備前に子プロセスが終了しました。");await Task.Delay(20,cancellation);
        }
    }
    private static async Task<ClientResult> Finish(Child child,CancellationToken cancellation){
        await child.Process.WaitForExitAsync(cancellation);var output=await child.Output;var error=await child.Error;
        Assert.True(child.Process.ExitCode==0,"クライアント失敗\n"+error+"\n"+output);
        return JsonSerializer.Deserialize<ClientResult>(output,ManagementJson.Options)!;
    }
    internal static async Task<int> RunClientAsync(string root,int port,string control,int worker,string mode){
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var certificate=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));
        using var client=new ManagementClient("localhost",port,certificate);await client.LoginAsync("admin","ClientTestPassword-123",deadline.Token);
        var instance=(await client.InfoAsync(deadline.Token)).InstanceId;using var process=Process.GetCurrentProcess();long affinity;
        if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}
        else{throw new PlatformNotSupportedException("CPU affinityを確認できる環境で実行してください。");}
        var marker=Path.Combine(control,process.Id+".ready");var ready=new ClientReady(process.Id,instance,affinity);
        await File.WriteAllTextAsync(marker+".tmp",JsonSerializer.Serialize(ready,ManagementJson.Options),deadline.Token);File.Move(marker+".tmp",marker);
        var counts=new List<int>();var received=0;
        if(mode=="write"){
            while(!File.Exists(Path.Combine(control,"go"))){await Task.Delay(20,deadline.Token);}
            for(var index=1;index<=5;index++){counts.Add(await Execute(client,"insert",worker*100+index,deadline.Token));}
        }else if(mode=="disconnect"){
            await using var rows=client.ExecuteAsync("Native",new("disconnect",null,ManagementJson.Element(new{id=9999})),deadline.Token).GetAsyncEnumerator(deadline.Token);
            Assert.True(await rows.MoveNextAsync());Assert.Equal(32768,rows.Current.Value.GetString()!.Length);received=1;
        }else{throw new ArgumentException("未知のクライアントモードです。",nameof(mode));}
        Console.WriteLine(JsonSerializer.Serialize(new ClientResult(process.Id,counts.ToArray(),received),ManagementJson.Options));return 0;
    }
}
