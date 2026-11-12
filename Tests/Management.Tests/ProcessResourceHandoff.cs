using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ProcessResourceHandoff{
    [Theory][InlineData("reset-password",6)][InlineData("upgrade-managed-types",7)]
    public async Task NormalOfflineCommandsReleaseTheirCopiedStoreAndSignalBeforeFinalHandoff(string command,int expectedFormat){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"handoff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var seed=Path.Combine(directory,"seed");var root=Path.Combine(directory,"copy");var backup=Path.Combine(directory,"backup");Guid instance;
        using(var store=ManagementStore.Initialize(seed,"admin","HandoffOriginal-123")){instance=store.InstanceId;store.Write(()=>store.Put("serverSettings","active",new StoredServerSettings(1,new(){ManagerPort=12345})));}
        Assert.Equal(0,await Seamless.Management.Server.Program.Main(["backup","--root",seed,"--destination",root]));
        var physicalFiles=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToArray();Assert.NotEmpty(physicalFiles);var physicalBytes=physicalFiles.Sum(file=>new FileInfo(file).Length);
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--management-cli",command,"--root",root,"--backup",backup}){start.ArgumentList.Add(argument);}
        if(command=="reset-password"){start.ArgumentList.Add("--user");start.ArgumentList.Add("admin");start.Environment["SEAMLESS_MANAGEMENT_PASSWORD"]="HandoffChanged-123";}
        using var child=Process.Start(start)!;var created=child.StartTime.ToUniversalTime();var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var asyncSignal=-2;var finalSignal=-2;Exception? immediateFailure=null;long waitReturned=0,openedAt=0;
        try{
            await child.WaitForExitAsync(timeout.Token);waitReturned=Stopwatch.GetTimestamp();asyncSignal=RecoveryLockDiagnostic.ProcessSignal(child);openedAt=Stopwatch.GetTimestamp();
            try{using var immediate=new ManagementStore(root);Assert.Equal(instance,immediate.InstanceId);Assert.Equal(expectedFormat,immediate.Format);Assert.Equal(1,immediate.Read<StoredServerSettings>("serverSettings","active")!.Version);}
            catch(Exception exception){immediateFailure=exception;}
        }finally{
            if(!child.HasExited){Assert.Equal(created,child.StartTime.ToUniversalTime());child.Kill(entireProcessTree:true);}
            Assert.True(child.WaitForExit(30000),"The owned command did not reach its final process exit signal.");finalSignal=RecoveryLockDiagnostic.ProcessSignal(child);
        }
        Assert.True(child.ExitCode==0,(await output)+(await errors));if(OperatingSystem.IsWindows()){Assert.Equal(0,finalSignal);}
        using(var restored=new ManagementStore(root)){Assert.Equal(instance,restored.InstanceId);Assert.Equal(expectedFormat,restored.Format);Assert.NotNull(restored.Read<UserAccount>("user","ADMIN"));}
        using(var previous=new ManagementStore(backup)){Assert.Equal(instance,previous.InstanceId);Assert.Equal(6,previous.Format);}
        File.WriteAllText(root+".handoff.json",JsonSerializer.Serialize(new{Command=command,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,ParentPid=Environment.ProcessId,ChildPid=child.Id,ChildCreatedUtc=created,child.ExitCode,NativeSignalAfterAsync=asyncSignal,NativeSignalAfterKernelWait=finalSignal,StopwatchFrequency=Stopwatch.Frequency,WaitReturned=waitReturned,OpenedAt=openedAt,ImmediateOpenSucceeded=immediateFailure is null,FailureType=immediateFailure?.GetType().FullName,FailureHResult=immediateFailure?.HResult,PhysicalFileCount=physicalFiles.Length,PhysicalBytes=physicalBytes,InstanceId=instance,FinalFormat=expectedFormat}));
        Assert.Null(immediateFailure);
    }
}
