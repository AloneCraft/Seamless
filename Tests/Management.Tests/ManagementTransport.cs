using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class ManagementTransport{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FragmentedJsonRetainsWholeDocumentValidation(bool trailing){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");request.CertificateExtensions.Add(names.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));var certificate=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=certificate.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{
            var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"framing-"+Guid.NewGuid().ToString("N"));using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));server.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));using var socket=new TcpClient{NoDelay=true};await socket.ConnectAsync(IPAddress.Loopback,server.Port,timeout.Token);await using var stream=new SslStream(socket.GetStream());var policy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};policy.CustomTrustStore.Add(certificate);await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=policy},timeout.Token);
            var id=Guid.NewGuid();var text=JsonSerializer.Serialize(new RpcRequest(id,"info",null,"",ManagementJson.Element(new string('x',200000))),ManagementJson.Options);if(trailing){text+="{}";}var bytes=Encoding.UTF8.GetBytes(text);var prefix=new byte[4];BinaryPrimitives.WriteInt32BigEndian(prefix,bytes.Length);foreach(var value in prefix){await stream.WriteAsync(new[]{value},timeout.Token);}for(var offset=0;offset<bytes.Length;offset+=8192){await stream.WriteAsync(bytes.AsMemory(offset,Math.Min(8192,bytes.Length-offset)),timeout.Token);}await stream.FlushAsync(timeout.Token);
            if(trailing){try{Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));}catch(IOException){}}
            else{var response=await ManagementWire.ReadAsync<RpcResponse>(stream,timeout.Token);Assert.Equal(id,response.Id);Assert.Equal("result",response.Kind);Assert.Equal(store.InstanceId,ManagementJson.Read<ServerInfo>(response.Payload).InstanceId);}
        }finally{certificate.Dispose();if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}Assert.False(CngKey.Exists(keyName,provider));}}
    }
}
