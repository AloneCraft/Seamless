using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

internal static class AuditUpgradeProbe{
    internal static int Run(string[] args){
        ManagementStoreUpgrade.Checkpoint=phase=>{if(phase!=args[2]){return;}using var process=Process.GetCurrentProcess();long affinity=0;if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}else{throw new PlatformNotSupportedException("この停止試験のaffinity確認はWindows/Linuxでのみ対応しています。");}File.WriteAllText(args[0]+".ready.tmp",JsonSerializer.Serialize(new{Pid=process.Id,CreationUtc=process.StartTime.ToUniversalTime(),Phase=phase,Affinity=affinity}));File.Move(args[0]+".ready.tmp",args[0]+".ready");Thread.Sleep(Timeout.Infinite);};
        ManagementStoreUpgrade.Upgrade(args[0],args[1]);return 0;
    }
}
[Collection("AuditStorage")]
public sealed class AuditUpgradeCrash{
    [Theory][InlineData("OldMoved",false)][InlineData("NewInstalled",false)][InlineData("Committed",false)][InlineData("Published",true)]
    public async Task RestartRequiresExplicitRecoveryBeforePublication(string phase,bool published){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"audit-crash-"+Guid.NewGuid().ToString("N"));Guid instance;using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123")){instance=store.InstanceId;store.Write(()=>store.Put("probe","value",new SourceFile("f.cs","preserved")));}
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--audit-upgrade-probe",root,root+"-backup",phase}){start.ArgumentList.Add(argument);}using var child=Process.Start(start)!;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{
            while(!File.Exists(root+".ready")){Assert.False(child.HasExited,"更新試験プロセスが準備前に終了しました。");await Task.Delay(25,timeout.Token);}var ready=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(root+".ready"));Assert.Equal(child.Id,ready.GetProperty("Pid").GetInt32());Assert.Equal(child.StartTime.ToUniversalTime(),ready.GetProperty("CreationUtc").GetDateTime());Assert.Equal(255,ready.GetProperty("Affinity").GetInt64());child.Kill(entireProcessTree:true);await child.WaitForExitAsync(timeout.Token);
        }finally{if(!child.HasExited){child.Kill(entireProcessTree:true);await child.WaitForExitAsync();}}
        if(!published){Assert.Equal("RecoveryRequired",Assert.Throws<ManagementException>(()=>new ManagementStore(root)).Code);ManagementStoreUpgrade.Rollback(root);using(var restored=new ManagementStore(root)){Assert.Equal(3,restored.Format);Assert.Equal(instance,restored.InstanceId);Assert.Equal("preserved",restored.Read<SourceFile>("probe","value")!.Text);}ManagementStoreUpgrade.Upgrade(root,root+"-backup2");}
        else{Assert.Equal("NoPendingUpgrade",Assert.Throws<ManagementException>(()=>ManagementStoreUpgrade.Rollback(root)).Code);}
        using var final=new ManagementStore(root);Assert.Equal(4,final.Format);Assert.Equal(instance,final.InstanceId);Assert.Equal("preserved",final.Read<SourceFile>("probe","value")!.Text);
    }
}
