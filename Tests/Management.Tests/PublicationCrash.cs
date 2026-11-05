using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public static class CrashProbe{
    public static int Main(string[] args){
        if(args.Length==4&&args[0]=="--oidc-session-probe"){return OidcSessionProbe.RunAsync(args[1..]).GetAwaiter().GetResult();}
        if(args.Length!=4||args[0]!="--publication-probe"){return 2;}var root=args[1];var phase=args[2];var marker=args[3];
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        service.PublicationCheckpoint=(current,id)=>{if(current!=phase){return;}using var process=Process.GetCurrentProcess();var temporary=marker+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(new{Id=id,Pid=process.Id,Created=process.StartTime.ToUniversalTime(),Phase=current}));File.Move(temporary,marker);Thread.Sleep(Timeout.Infinite);};
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database());return 0;
    }
}
public sealed class PublicationCrash{
    [Theory][InlineData("Prepared",false)][InlineData("Committed",true)]public async Task Recover(string phase,bool committed){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));var marker=root+".ready";
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);foreach(var item in new[]{"--publication-probe",root,phase,marker}){start.ArgumentList.Add(item);}
        using var process=Process.Start(start)!;JsonElement ready;
        try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));while(!File.Exists(marker)){Assert.False(process.HasExited,"公開試験プロセスが準備前に終了しました。");await Task.Delay(50,timeout.Token);}ready=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(marker));
            Assert.Equal(process.Id,ready.GetProperty("Pid").GetInt32());Assert.Equal(process.StartTime.ToUniversalTime(),ready.GetProperty("Created").GetDateTime());process.Kill(entireProcessTree:true);await process.WaitForExitAsync(timeout.Token);
        }finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        using var store=new ManagementStore(root);using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);var operation=service.GetOperation(user,ready.GetProperty("Id").GetGuid());
        if(committed){Assert.Equal(OperationState.Succeeded,operation.State);Assert.Equal(1,service.GetDefinition(user,"db").Version);Assert.Contains(service.History(user).Items,item=>item.Action=="Applied");}
        else{Assert.Equal(OperationState.OutcomeUnknown,operation.State);Assert.Throws<ManagementException>(()=>service.GetDefinition(user,"db"));Assert.DoesNotContain(service.History(user).Items,item=>item.Action=="Applied");}
    }
    [Fact]public void CancelBeforeCommit(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var draft=service.SaveDraft(user,new(NativeWorkflow.Database(),0,0));var preview=service.Preview(user,new("db",draft.Revision));service.PublicationCheckpoint=(phase,id)=>{if(phase=="Prepared"){service.Cancel(user,id);}};
        var result=service.Apply(user,new("db",draft.Revision,preview.Token,Guid.NewGuid()));Assert.Equal(OperationState.Cancelled,result.State);Assert.Throws<ManagementException>(()=>service.GetDefinition(user,"db"));
    }
}
