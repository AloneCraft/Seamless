using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Server=Seamless.Database.Gateway.Http.HttpServer;

namespace Seamless.Database.Gateway.Tests;

public sealed class HttpServerTests{
    [Fact,Trait("Category","Integration")]
    public async Task DefaultListenerKeepsIPv4AndIPv6Reachability(){
        using var database=new RuntimeSqlGeneratedRoot(new(){Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var adapter=new InProcessDatabaseAdapter(database);using var server=new Server(0,adapter,new SqlExecutionEngine(adapter));server.Start();using var client=new HttpClient(new SocketsHttpHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(10)};using var ipv4=await client.GetAsync($"http://127.0.0.1:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,ipv4.StatusCode);
        if(global::System.Net.Sockets.Socket.OSSupportsIPv6){using var ipv6=await client.GetAsync($"http://[::1]:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,ipv6.StatusCode);}
    }
    [Fact,Trait("Category","Integration")]
    public async Task HttpsDynamicPortAndHostNameValidation(){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");request.CertificateExtensions.Add(names.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
        // Windows Schannel用の一時ユーザーキーは利用者の許可済み範囲。試験後に削除する。
        var certificate=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=certificate.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{
        using var database=new RuntimeSqlGeneratedRoot(new(){Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var adapter=new InProcessDatabaseAdapter(database);using var server=new Server(0,adapter,new SqlExecutionEngine(adapter),new(){ListenAddress=IPAddress.Loopback,Certificate=certificate});
        using var handler=new SocketsHttpHandler{SslOptions=new SslClientAuthenticationOptions{CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck}}};handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(certificate);using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(10)};
        for(var pass=0;pass<2;pass++){
            server.Start();Assert.InRange(server.Port,1,65535);using var response=await client.GetAsync($"https://localhost:{server.Port}/api/tables");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.StartsWith("[",await response.Content.ReadAsStringAsync());await Assert.ThrowsAsync<HttpRequestException>(async()=>{using var mismatch=await client.GetAsync($"https://127.0.0.1:{server.Port}/api/tables");});await server.StopAsync();
        }
        }finally{certificate.Dispose();if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}Assert.False(CngKey.Exists(keyName,provider));}}
    }
}
