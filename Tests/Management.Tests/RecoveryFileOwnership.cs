using System.Diagnostics;
using System.Text.Json;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class RecoveryFileOwnership{
    [RecoveryWindowsFact]public async Task ObserverIdentifiesTheProcessHoldingTheExclusiveFile(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"owner-"+Guid.NewGuid().ToString("N"));
        using(var store=ManagementStore.Initialize(root,"admin","OwnerTestPassword-123")){}
        var path=Path.Combine(root,"management.lock");var marker=root+".owner-ready";
        var executable=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--hold-recovery-lock",path,marker}){start.ArgumentList.Add(argument);}
        using var child=Process.Start(start)!;var created=child.StartTime.ToUniversalTime();
        try{
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while(!File.Exists(marker)){Assert.False(child.HasExited,"ロック保持プロセスが準備前に終了しました。");await Task.Delay(10,timeout.Token);}
            var ready=JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(marker,timeout.Token));Assert.Equal(child.Id,ready.GetProperty("Pid").GetInt32());Assert.Equal(created,ready.GetProperty("Created").GetDateTime());
            var failure=Assert.Throws<IOException>(()=>new ManagementStore(root));Assert.Equal(unchecked((int)0x80070020),failure.HResult);
            var owners=RecoveryLockDiagnostic.Owners(path);Assert.Contains(owners,owner=>owner.Pid==child.Id&&owner.Created==created);
            await File.WriteAllTextAsync(root+".owner-proof.json",JsonSerializer.Serialize(new{Path=path,ChildPid=child.Id,ChildCreated=created,Owners=owners}),timeout.Token);
            child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(15000));
            using var reopened=new ManagementStore(root);Assert.NotEqual(Guid.Empty,reopened.InstanceId);
        }finally{if(!child.HasExited){Assert.Equal(created,child.StartTime.ToUniversalTime());child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(15000));}}
    }
}
internal sealed class RecoveryWindowsFactAttribute:FactAttribute{
    public RecoveryWindowsFactAttribute(){if(!OperatingSystem.IsWindows()){this.Skip="Restart Managerの所有者照合はWindowsで実行してください。";}}
}
