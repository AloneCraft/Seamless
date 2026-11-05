using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using ServerProgram=Seamless.Management.Server.Program;

namespace Management.Tests;

public sealed class LocalServerSetup{
    [Fact]public async Task Initialize(){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));var root=Path.Combine(directory,"instance");var config=Path.Combine(directory,"launch.json");
        Assert.Equal("SetupRequired",Assert.Throws<ManagementException>(()=>ManagementLaunchConfiguration.Read(config)).Code);
        foreach(var password in new[]{"",new string('a',11)}){Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin",password,config)).Code);Assert.False(Directory.Exists(root));Assert.False(File.Exists(config));}
        Seamless.Management.Server.LocalServerSetup.Initialize(root,"admin","SetupPassword-123",config);var settings=ManagementLaunchConfiguration.Read(config);Assert.Equal(root,settings.Root);Assert.Equal(14336,settings.Port);
        Assert.DoesNotContain("SetupPassword",File.ReadAllText(config));Assert.Throws<IOException>(()=>Seamless.Management.Server.LocalServerSetup.Initialize(root,"other","OtherPassword-123",config));
        using(var store=new ManagementStore(root))using(var service=new Seamless.Management.Server.ManagementService(store)){Assert.Equal("admin",service.Login(new("admin","SetupPassword-123")).User.Name);}
        var ready=Path.Combine(directory,"ready.json");var stop=Path.Combine(directory,"stop");var server=ServerProgram.Main(["serve","--config",config,"--port","0","--ready-file",ready,"--stop-file",stop]);
        try{
            Assert.True(File.Exists(ready),"初期設定した起動設定でサーバーが起動しませんでした。");var local=Seamless.Management.LocalServerConnection.Read(root);Assert.NotNull(local);
            using var certificate=X509CertificateLoader.LoadCertificateFromFile(Path.Combine(root,Seamless.Management.LocalServerConnection.CertificateFileName));Assert.False(certificate.HasPrivateKey);
            var client=new ManagementClient("localhost",local.Port,certificate);var info=await client.InfoAsync();Assert.Equal(local.InstanceId,info.InstanceId);Assert.Equal("admin",(await client.LoginAsync("admin","SetupPassword-123")).User.Name);
        }finally{File.WriteAllText(stop,"stop");Assert.Equal(0,await server.WaitAsync(TimeSpan.FromSeconds(10)));}
        Assert.Null(Seamless.Management.LocalServerConnection.Read(root));
        Assert.Equal(0,await ServerProgram.Main(["serve","--config",config,"--port","0","--owner-pid",Environment.ProcessId.ToString(),"--owner-start","0"]).WaitAsync(TimeSpan.FromSeconds(10)));Assert.Null(Seamless.Management.LocalServerConnection.Read(root));
        var second=Path.Combine(directory,"second.json");File.WriteAllText(second,JsonSerializer.Serialize(new ManagementLaunchConfiguration("instance","instance/server.pfx",LocalUser:settings.LocalUser),ManagementJson.Options));Assert.Equal(settings,ManagementLaunchConfiguration.Read(second));
        Assert.Equal(1,await ServerProgram.Main(["serve","--config",Path.Combine(directory,"missing.json")]));
    }
}
