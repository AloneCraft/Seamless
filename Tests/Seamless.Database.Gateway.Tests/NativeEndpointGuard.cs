using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tests;

public sealed class NativeEndpointGuard{
    [Fact]public void NonDefaultAddressCannotBypassTheFinalAuthenticationGuard(){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        NativeQueryServer Create(IPAddress address)=>new(new DatabaseRegistry(),0,1,1,TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),new FrameBufferQuota(1024),new FrameBufferQuota(1024),new FrameBufferQuota(1024),certificate,authorization:null,listenAddress:address,maximumConcurrentHandshakes:1);
        using var local=Create(IPAddress.Loopback);Assert.Equal(IPAddress.Loopback,local.ListenAddress);
        var denied=Assert.Throws<ArgumentException>(()=>Create(IPAddress.Any));Assert.Equal("listenAddress",denied.ParamName);
    }
}
