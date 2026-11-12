using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

internal static class AuditUpgradeProbe{
    internal static int Run(string[] args,bool preparedWal=false,bool managedTypes=false){
        ManagementStoreUpgrade.Checkpoint=phase=>{
            if(phase!=args[2]){return;}
            using var process=Process.GetCurrentProcess();long affinity=0;
            if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}
            else{throw new PlatformNotSupportedException("この停止試験のaffinity確認はWindows/Linuxでのみ対応しています。");}
            var created=process.StartTime.ToUniversalTime();
            var identity=global::Seamless.Management.LocalProcessIdentity.Capture(process.Id,created);
            File.WriteAllText(args[0]+".ready.tmp",JsonSerializer.Serialize(new{Pid=process.Id,CreationUtc=created,ProcessIdentity=identity,Phase=phase,Affinity=affinity}));
            File.Move(args[0]+".ready.tmp",args[0]+".ready");Thread.Sleep(Timeout.Infinite);
        };
        if(managedTypes){ManagementStoreUpgrade.UpgradeManagedTypes(args[0],args[1]);}
        else if(preparedWal){ManagementStoreUpgrade.UpgradePreparedWal(args[0],args[1]);}
        else{ManagementStoreUpgrade.Upgrade(args[0],args[1]);}
        return 0;
    }
}
[Collection("AuditStorage")]
public sealed class AuditUpgradeCrash{
    [Theory][InlineData("OldMoved",false,3)][InlineData("NewInstalled",false,3)][InlineData("Committed",false,3)][InlineData("Published",true,3)][InlineData("OldMoved",false,4)][InlineData("NewInstalled",false,4)][InlineData("Committed",false,4)][InlineData("Published",true,4)]
    [InlineData("Marked",false,5)]
    [InlineData("OldMoved",false,5)]
    [InlineData("NewInstalled",false,5)]
    [InlineData("Committed",false,5)]
    [InlineData("Published",true,5)]
    public async Task RestartRequiresExplicitRecoveryBeforePublication(string phase,bool published,int originalFormat){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"audit-crash-"+Guid.NewGuid().ToString("N"));
        var preparedWal=originalFormat==5;
        ManagementStore Create(string path){
            if(preparedWal){return ManagementStore.InitializeReadAuditLegacy(path,"admin","TestPassword-123");}
            if(originalFormat==4){return ManagementStore.InitializeAuditLegacy(path,"admin","TestPassword-123");}
            return ManagementStore.InitializeLegacy(path,"admin","TestPassword-123");
        }
        void Upgrade(string backup){
            if(preparedWal){ManagementStoreUpgrade.UpgradePreparedWal(root,backup);}
            else{ManagementStoreUpgrade.Upgrade(root,backup);}
        }
        void Rollback(){
            if(preparedWal){ManagementStoreUpgrade.RollbackPreparedWal(root);}
            else{ManagementStoreUpgrade.Rollback(root);}
        }
        Guid instance;using(var store=Create(root)){instance=store.InstanceId;store.Write(()=>store.Put("probe","value",new SourceFile("f.cs","preserved")));}
        var probe="--audit-upgrade-probe";
        if(preparedWal){probe="--prepared-wal-upgrade-probe";}
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,probe,root,root+"-backup",phase}){start.ArgumentList.Add(argument);}using var child=Process.Start(start)!;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{
            while(!File.Exists(root+".ready")){Assert.False(child.HasExited,"更新試験プロセスが準備前に終了しました。");await Task.Delay(25,timeout.Token);}
            var ready=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(root+".ready"));
            Assert.Equal(child.Id,ready.GetProperty("Pid").GetInt32());
            Assert.True(global::Seamless.Management.LocalProcessIdentity.Matches(child.Id,ready.GetProperty("CreationUtc").GetDateTime(),ready.GetProperty("ProcessIdentity").GetString()));
            Assert.Equal(255,ready.GetProperty("Affinity").GetInt64());child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(60000));
        }finally{if(!child.HasExited){child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(60000));}}
        if(!published){Assert.Equal("RecoveryRequired",Assert.Throws<ManagementException>(()=>new ManagementStore(root)).Code);Rollback();using(var restored=new ManagementStore(root)){Assert.Equal(originalFormat,restored.Format);Assert.Equal(instance,restored.InstanceId);Assert.Equal("preserved",restored.Read<SourceFile>("probe","value")!.Text);}Upgrade(root+"-backup2");}
        else{Assert.Equal("NoPendingUpgrade",Assert.Throws<ManagementException>(Rollback).Code);}
        var expectedFormat=5;
        if(preparedWal){expectedFormat=6;}
        using var final=new ManagementStore(root);Assert.Equal(expectedFormat,final.Format);Assert.Equal(instance,final.InstanceId);Assert.Equal("preserved",final.Read<SourceFile>("probe","value")!.Text);
    }
}
