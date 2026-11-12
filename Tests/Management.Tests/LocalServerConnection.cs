using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Connection=Seamless.Management.LocalServerConnection;
using System.Diagnostics;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class LocalServerConnection{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagerLaunchUsesTheServerPipeWhenClientTemporaryDirectoryDiffers(bool managerRecord){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"pipe-path-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var operations=new ConcurrentManagementOperations(service);
        await using var web=await ManagerWebHost.StartAsync(operations);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service,web.Sessions);
        server.Start();
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        if(managerRecord){LocalManagerConnection.Publish(root,store.InstanceId,server.PipeName!,server.UnixPipePath);}
        else{Connection.Publish(root,store.InstanceId,14336,certificate,server.PipeName,null,server.UnixPipePath);}
        var temporary=Path.Combine(root,"client-temporary");Directory.CreateDirectory(temporary);
        using var process=new Process{StartInfo=new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root
        }};
        process.StartInfo.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);
        process.StartInfo.ArgumentList.Add("--manager-launch-probe");process.StartInfo.ArgumentList.Add(root);
        process.StartInfo.Environment["TMPDIR"]=temporary;
        Assert.True(process.Start());var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        try{
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(deadline.Token);
            Assert.True(process.WaitForExit(30000));
            Assert.True(process.ExitCode==0,await errors);
            Assert.Equal("launched",(await output).Trim());
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);Assert.True(process.WaitForExit(30000));}
        }
    }

    [Fact]
    public void PipeEndpointValidationPreservesLegacyNamesAndRejectsMismatchedPaths(){
        var name=Connection.PipeNameFor(Guid.NewGuid());
        Assert.Equal(name,Connection.ResolvePipeEndpoint(name,null));
        foreach(var path in new[]{"relative/CoreFxPipe_"+name,"/tmp/CoreFxPipe_other","/tmp/../CoreFxPipe_"+name,"/tmp/./CoreFxPipe_"+name,"/tmp/\0/CoreFxPipe_"+name}){
            Assert.Throws<InvalidDataException>(()=>Connection.ResolvePipeEndpoint(name,path));
        }
        Assert.Throws<InvalidDataException>(()=>Connection.ResolvePipeEndpoint(null,"/tmp/CoreFxPipe_"+name));
        var unix="/tmp/CoreFxPipe_"+name;
        if(OperatingSystem.IsLinux()){Assert.Equal(unix,Connection.ResolvePipeEndpoint(name,unix));}
        else{Assert.Equal(name,Connection.ResolvePipeEndpoint(name,unix));}
    }

    [Fact]public async Task PublishedConnectionsAreVisibleToAnotherProcess(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        var id=Guid.NewGuid();var pipe=Connection.PipeNameFor(id);var info=Connection.Publish(root,id,14336,certificate,pipe);var manager=LocalManagerConnection.Publish(root,id,pipe);
        using var process=new Process{StartInfo=new("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        process.StartInfo.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);process.StartInfo.ArgumentList.Add("--read-local-connections");process.StartInfo.ArgumentList.Add(root);process.StartInfo.ArgumentList.Add(id.ToString());
        Assert.True(process.Start());var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await process.WaitForExitAsync(timeout.Token);Assert.Equal(0,process.ExitCode);Assert.Equal("matched",(await output).Trim());Assert.Empty(await errors);}
        finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        var path=Path.Combine(root,LocalManagerConnection.FileName);
        File.WriteAllText(path,JsonSerializer.Serialize(manager with{CreationUtc=DateTime.UnixEpoch},ManagementJson.Options));Assert.Null(LocalManagerConnection.Read(root));
        File.WriteAllText(path,JsonSerializer.Serialize(manager with{ProcessIdentity="invalid"},ManagementJson.Options));
        if(OperatingSystem.IsLinux()){Assert.Null(LocalManagerConnection.Read(root));}
        File.WriteAllText(path,JsonSerializer.Serialize(manager,ManagementJson.Options));manager.Remove(root);info.Remove(root);
    }
    internal static int ReadConnections(string root,Guid id){
        if(Connection.Read(root)?.InstanceId!=id||LocalManagerConnection.Read(root)?.InstanceId!=id){throw new InvalidDataException("The live process identity did not match.");}
        Console.WriteLine("matched");return 0;
    }
    [Fact]public void PublishAndRead(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Assert.Null(Connection.Read(root));
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        var info=Connection.Publish(root,Guid.NewGuid(),14336,certificate);Assert.Equal(info,Connection.Read(root));
        using(var exported=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Connection.CertificateFileName))){Assert.False(exported.HasPrivateKey);}
        var manifest=Path.Combine(root,Connection.FileName);var original=File.ReadAllText(manifest);Assert.DoesNotContain("password",original,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("private",original,StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(manifest,JsonSerializer.Serialize(info with{CreationUtc=DateTime.UnixEpoch},ManagementJson.Options));Assert.Null(Connection.Read(root));
        File.WriteAllText(manifest,JsonSerializer.Serialize(info with{Pid=int.MaxValue},ManagementJson.Options));Assert.Null(Connection.Read(root));
        File.WriteAllText(manifest,JsonSerializer.Serialize(info with{Host="remote.example"},ManagementJson.Options));Assert.Throws<InvalidDataException>(()=>Connection.Read(root));
        File.WriteAllText(manifest,original);File.WriteAllBytes(Path.Combine(root,Connection.CertificateFileName),[1,2,3]);Assert.Throws<InvalidDataException>(()=>Connection.Read(root));
        info.Remove(root);Assert.False(File.Exists(manifest));
    }
}
