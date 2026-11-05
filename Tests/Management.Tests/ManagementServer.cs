using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementServer{
    [Fact]public async Task Connect(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        // 利用者が2026-11-02に許可した、一時的なWindowsユーザーキー。証明書ストアには登録しない。
        var cert=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);
        string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=cert.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{
            using var store=ManagementStore.Initialize(path,"admin","TestPassword-123");
            using var service=new Seamless.Management.Server.ManagementService(store);
            await using var server=new Seamless.Management.Server.ManagementServer(service,cert,new IPEndPoint(IPAddress.Loopback,0),"admin");
            var failures=new System.Collections.Concurrent.ConcurrentQueue<Exception>();server.ConnectionFailed+=failures.Enqueue;server.Start();
            var client=new ManagementClient("localhost",server.Port,cert);
            ServerInfo info;try{info=await client.InfoAsync();}catch(Exception error){throw new InvalidOperationException(string.Join("\n",failures.Select(item=>item.ToString())),error);}
            Assert.Equal(store.InstanceId,info.InstanceId);
            var localClient=ManagementClient.ForLocalPipe(server.PipeName!);Assert.Equal("admin",(await localClient.LocalIdentityAsync()).Name);Assert.Null(localClient.Token);Assert.Single((await localClient.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{})).Items);
            await Assert.ThrowsAsync<ManagementException>(()=>client.CallAsync<Page<DefinitionSummary>>("definitions","",new{}));
            await client.LoginAsync("admin","TestPassword-123");
            Assert.Equal("Unsupported",(await Assert.ThrowsAsync<ManagementException>(()=>client.LocalIdentityAsync())).Code);
            Assert.Empty((await client.CallAsync<Page<DefinitionSummary>>("definitions","",new{})).Items);
            Assert.Contains("NativeCatalogV1",info.Capabilities);
            var catalog=await client.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{});Assert.True(Assert.Single(catalog.Items).IsSystem);
            var system=await client.CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",new CatalogRequest("system",catalog.VersionToken));Assert.Contains(system.Items,item=>item.Name=="Entries");
            await Assert.ThrowsAsync<ManagementException>(()=>new ManagementClient("localhost",server.Port,cert).LoginAsync("admin","WrongPassword-123"));
            await Assert.ThrowsAsync<AuthenticationException>(()=>new ManagementClient("localhost",server.Port).InfoAsync());
            await Assert.ThrowsAsync<AuthenticationException>(()=>new ManagementClient("127.0.0.1",server.Port,cert).InfoAsync());
            if(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_EXE") is{} executable){
                var actor=service.Authenticate((await client.LoginAsync("admin","TestPassword-123")).Token);NativeWorkflow.Publish(service,actor,NativeWorkflow.Database());
                var query=new Definition("ui-query","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","""
                    using System.Linq;using Seamless.FLinq;using Seamless.Management.Server;
                    namespace Model;
                    public sealed class Query{
                        // preserved
                        [LinqIntercept(true)] public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Where(row=>row.Value>0).Sum(row=>row.Value);
                    }
                    """)],[]));NativeWorkflow.Publish(service,actor,query);
                Seamless.Management.LocalServerConnection.Publish(path,store.InstanceId,server.Port,cert,server.PipeName);var publicCertificate=Path.Combine(path,Seamless.Management.LocalServerConnection.CertificateFileName);
                var config=Path.Combine(path,"ui-test.json");File.WriteAllText(config,System.Text.Json.JsonSerializer.Serialize(new{ホスト="localhost",ポート=server.Port,利用者="admin",証明書=publicCertificate,定義ID="ui-query",自動接続か=true},ManagementJson.Options));
                var output=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG")!,"ui-plan.png");
                var start=new System.Diagnostics.ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};start.ArgumentList.Add("--smoke");start.ArgumentList.Add(config);start.ArgumentList.Add(output);start.Environment["SEAMLESS_MANAGEMENT_UI_TEST_PASSWORD"]="TestPassword-123";start.Environment["SEAMLESS_MANAGEMENT_HOME"]=Path.Combine(path,"ui-settings");
                start.Environment["SEAMLESS_MANAGEMENT_ROOT"]=path;
                using var process=System.Diagnostics.Process.Start(start)!;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(entireProcessTree:true);throw;}
                var failure=output+".error.txt";var message="GUI exit="+process.ExitCode;if(File.Exists(failure)){message=File.ReadAllText(failure);}Assert.True(process.ExitCode==0,message);Assert.True(File.Exists(output+".result.json"));
            }
        }finally{
            cert.Dispose();
            if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){
                if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}
                Assert.False(CngKey.Exists(keyName,provider));
            }
        }
    }
}
