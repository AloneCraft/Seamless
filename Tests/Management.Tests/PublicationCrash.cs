using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public static class CrashProbe{
    public static int Main(string[] args){
        if(args.Length is >=1 and <=4&&args[0]=="--native-policy-server"){
            var maximum=new Seamless.Database.Gateway.Native.NativeServerLimits().MaximumConnections;
            var identities=1;
            if(args.Length>=2&&(!int.TryParse(args[1],out maximum)||maximum is <1 or >10000)){return 2;}
            if(args.Length>=3&&(!int.TryParse(args[2],out identities)||identities is <1 or >100||identities>maximum)){return 2;}
            var protectTds=args.Length==4&&args[3]=="https-tds";
            var mixed=protectTds||args.Length==4&&args[3]=="https";
            if(args.Length==4&&!mixed){return 2;}
            return NativeTls.ServePolicyProbeAsync(maximum,identities,mixed,protectTds).GetAwaiter().GetResult();
        }
        try{
            return Run(args);
        }catch(Exception error){
            Console.Error.WriteLine(error);
            return 1;
        }
    }
    private static int Run(string[] args){
        if(args.Length==2&&args[0]=="--manager-launch-probe"){
            _=ManagerBrowserLauncher.CreateLaunchAsync(args[1]).GetAwaiter().GetResult();
            Console.WriteLine("launched");return 0;
        }
        if(args.Length==3&&args[0]=="--read-local-connections"){return LocalServerConnection.ReadConnections(args[1],Guid.Parse(args[2]));}
        if(args.Length==2&&args[0]=="--publication-recovery-comparison"){return PublicationCrash.RunRecoveryComparisonAsync(int.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture)).GetAwaiter().GetResult();}
        if(args.Length==2&&args[0]=="--publication-recovery-stress"){return PublicationCrash.RunRecoveryProbeAsync(int.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture)).GetAwaiter().GetResult();}
        if(args.Length==3&&args[0]=="--hold-recovery-lock"){
            using var owner=new FileStream(args[1],FileMode.Open,FileAccess.ReadWrite,FileShare.None);using var process=Process.GetCurrentProcess();
            File.WriteAllText(args[2]+".tmp",JsonSerializer.Serialize(new{Pid=process.Id,Created=process.StartTime.ToUniversalTime()}));File.Move(args[2]+".tmp",args[2]);Thread.Sleep(Timeout.Infinite);return 0;
        }
        if(args.Length==6&&args[0]=="--business-client-probe"){return MultipleCSharpClients.RunClientAsync(args[1],int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3],int.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture),args[5]).GetAwaiter().GetResult();}
        if(args.Length==2&&args[0]=="--embedded-business-probe"){return EmbeddedBusinessOperations.RunDirectProbe(args[1]);}
        if(args.Length==2&&args[0]=="--embedded-stream-probe"){return EmbeddedBusinessOperations.RunStreamProbe(args[1]);}
        if(args.Length==3&&args[0]=="--embedded-manager-probe"){return ManagementApplicationHostTests.RunEmbeddedProbeAsync(args[1],args[2]).GetAwaiter().GetResult();}
        if(args.Length==1&&args[0]=="--management-tls-address-probe"){return ManagementTransport.RunTlsAddressProbeAsync().GetAwaiter().GetResult();}
        if(args.Length>0&&args[0]=="--management-tls-probe"){
            var count=10000;
            if(args.Length>2||(args.Length==2&&(!int.TryParse(args[1],out count)||count is<1 or>10000))){
                Console.Error.WriteLine("Usage: --management-tls-probe [catalogue requests 1..10000]");
                return 2;
            }
            return ManagementTransport.RunTlsProbeAsync(count).GetAwaiter().GetResult();
        }
        if(args.Length==3&&args[0]=="--authorization-policy-probe"){return AuthorizationPolicyStorage.RunProbe(args[1],args[2]);}
        if(args.Length==2&&args[0]=="--external-table-storage-crash"){return ExternalTableStorage.CrashAfterCommit(args[1]).GetAwaiter().GetResult();}
        if(args.Length>0&&args[0]=="--management-cli"){return Seamless.Management.Server.Program.Main(args[1..]).GetAwaiter().GetResult();}
        if(args.Length>0&&args[0]=="--gateway-cli"){Seamless.Database.Server.Program.Main(args[1..]).GetAwaiter().GetResult();return Environment.ExitCode;}
        if(args.Length==4&&args[0]=="--update-audit-upgrade-probe"){return UpdateAuditUpgrade.RunStopProbe(args[1],args[2],Guid.Parse(args[3]));}
        if(args.Length==2&&args[0]=="--native-update-audit-probe"){return NativeUpdateAudit.RunStopProbe(args[1]);}
        if(args.Length==4&&args[0]=="--audit-delivery-probe"){return AuditDeliveryTests.RunStopProbeAsync(args[1],args[2],args[3]).GetAwaiter().GetResult();}
        if(args.Length==4&&args[0]=="--oidc-session-probe"){return OidcSessionProbe.RunAsync(args[1..]).GetAwaiter().GetResult();}
        if(args.Length==4&&args[0]=="--audit-upgrade-probe"){return AuditUpgradeProbe.Run(args[1..]);}
        if(args.Length==4&&args[0]=="--prepared-wal-upgrade-probe"){return AuditUpgradeProbe.Run(args[1..],preparedWal:true);}
        if(args.Length==4&&args[0]=="--managed-types-upgrade-probe"){return AuditUpgradeProbe.Run(args[1..],managedTypes:true);}
        if(args is ["--placement-publication-probe",var placementRoot,var placementPhase,var placementMarker]){
            using var placementStore=new ManagementStore(placementRoot);
            using var placementService=new Seamless.Management.Server.ManagementService(placementStore);
            var admin=placementService.Authenticate(placementService.Login(new("admin","TestPassword-123")).Token);
            placementService.PublicationCheckpoint=(current,id)=>{
                if(current!=placementPhase){return;}
                File.WriteAllText(placementMarker+".tmp",id.ToString("D"));
                File.Move(placementMarker+".tmp",placementMarker);
                Thread.Sleep(Timeout.Infinite);
            };
            var draft=placementService.GetDraft(admin,"db");
            var preview=placementService.Preview(admin,new("db",draft.Revision));
            if(!preview.CanApply){throw new InvalidOperationException(string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));}
            return placementService.Apply(admin,new("db",draft.Revision,preview.Token,Guid.NewGuid())).State==OperationState.Succeeded?0:1;
        }
        if(args.Length!=4||args[0]!="--publication-probe"){return 2;}var root=args[1];var phase=args[2];var marker=args[3];
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        service.PublicationCheckpoint=(current,id)=>{if(current!=phase){return;}using var process=Process.GetCurrentProcess();var temporary=marker+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(new{Id=id,Pid=process.Id,Created=process.StartTime.ToUniversalTime(),Phase=current,OwnerHandleFlags=RecoveryLockDiagnostic.OwnerHandleFlags(store)}));File.Move(temporary,marker);Thread.Sleep(Timeout.Infinite);};
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database());return 0;
    }
}
public sealed class PublicationCrash(Xunit.Abstractions.ITestOutputHelper output){
    private static string DotNetExecutable=>Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??(OperatingSystem.IsWindows()?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe"):"dotnet");
    [Theory][InlineData("Prepared",false)][InlineData("Committed",true)]public async Task Recover(string phase,bool committed){
        await RecoverOnce(phase,committed,output);
    }
    private static async Task RecoverOnce(string phase,bool committed,Xunit.Abstractions.ITestOutputHelper? output=null,bool kernelWait=true,bool observeSignal=true){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));var marker=root+".ready";
        var start=new ProcessStartInfo(DotNetExecutable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);foreach(var item in new[]{"--publication-probe",root,phase,marker}){start.ArgumentList.Add(item);}
        using var process=Process.Start(start)!;JsonElement ready;long waitReturned;int nativeSignal;
        try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));var elapsed=Stopwatch.StartNew();while(!File.Exists(marker)){Assert.False(process.HasExited,"公開試験プロセスが準備前に終了しました。");await Task.Delay(50,timeout.Token);}ready=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(marker));
            Assert.Equal(process.Id,ready.GetProperty("Pid").GetInt32());Assert.Equal(process.StartTime.ToUniversalTime(),ready.GetProperty("Created").GetDateTime());
            if(OperatingSystem.IsWindows()){Assert.Equal(0,ready.GetProperty("OwnerHandleFlags").GetInt32()&1);}
            process.Kill(entireProcessTree:true);
            // .NETのHasExited高速経路と区別し、保持中のOSプロセスハンドルの終了を待つ。
            if(kernelWait){Assert.True(process.WaitForExit(checked((int)Math.Max(0,60000-elapsed.ElapsedMilliseconds))),"カーネルのプロセス終了が期限内に完了しませんでした。");}
            else{await process.WaitForExitAsync(timeout.Token);}
            waitReturned=Stopwatch.GetTimestamp();nativeSignal=-2;if(observeSignal){nativeSignal=RecoveryLockDiagnostic.ProcessSignal(process);}
            if(kernelWait&&OperatingSystem.IsWindows()){Assert.Equal(0,nativeSignal);}
        }finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        var openedAt=Stopwatch.GetTimestamp();ManagementStore recovered;
        try{recovered=new ManagementStore(root);}
        catch(IOException error){try{RecoveryLockDiagnostic.Record(root,process,waitReturned,openedAt,Stopwatch.GetTimestamp(),ready,nativeSignal,error);}catch(Exception diagnostic){output?.WriteLine("Recovery diagnostic failed "+diagnostic);}throw;}
        using var store=recovered;RecoveryLockDiagnostic.Record(root,process,waitReturned,openedAt,Stopwatch.GetTimestamp(),ready,nativeSignal,null);
        using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);var operation=service.GetOperation(user,ready.GetProperty("Id").GetGuid());
        if(committed){Assert.Equal(OperationState.Succeeded,operation.State);Assert.Equal(1,service.GetDefinition(user,"db").Version);Assert.Contains(service.History(user).Items,item=>item.Action=="Applied");}
        else{Assert.Equal(OperationState.OutcomeUnknown,operation.State);Assert.Throws<ManagementException>(()=>service.GetDefinition(user,"db"));Assert.DoesNotContain(service.History(user).Items,item=>item.Action=="Applied");}
    }
    internal static async Task<int> RunRecoveryProbeAsync(int repetitions){
        ArgumentOutOfRangeException.ThrowIfLessThan(repetitions,1);ArgumentOutOfRangeException.ThrowIfGreaterThan(repetitions,32);
        for(var index=0;index<repetitions;index++){await RecoverOnce("Prepared",false);await RecoverOnce("Committed",true);Console.WriteLine(JsonSerializer.Serialize(new{Iteration=index+1,Phases=2,Result="PassedWithoutRetry"}));}return 0;
    }
    internal static async Task<int> RunRecoveryComparisonAsync(int repetitions){
        ArgumentOutOfRangeException.ThrowIfLessThan(repetitions,1);ArgumentOutOfRangeException.ThrowIfGreaterThan(repetitions,32);
        var legacyFailures=0;var legacySuccesses=0;var kernelSuccesses=0;
        for(var index=0;index<repetitions;index++){
            foreach(var phase in new[]{"Prepared","Committed"}){
                try{await RecoverOnce(phase,phase=="Committed",kernelWait:false,observeSignal:false);legacySuccesses++;}
                catch(IOException error)when(error.HResult==unchecked((int)0x80070020)){legacyFailures++;}
                await RecoverOnce(phase,phase=="Committed");kernelSuccesses++;
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new{Experiment="WaitConditionComparison",LegacySuccesses=legacySuccesses,LegacySharingFailures=legacyFailures,KernelWaitSuccesses=kernelSuccesses,Repetitions=repetitions}));return 0;
    }
    [Fact]public void CancelBeforeCommit(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var draft=service.SaveDraft(user,new(NativeWorkflow.Database(),0,0));var preview=service.Preview(user,new("db",draft.Revision));service.PublicationCheckpoint=(phase,id)=>{if(phase=="Prepared"){service.Cancel(user,id);}};
        var result=service.Apply(user,new("db",draft.Revision,preview.Token,Guid.NewGuid()));Assert.Equal(OperationState.Cancelled,result.State);Assert.Throws<ManagementException>(()=>service.GetDefinition(user,"db"));
    }
}
