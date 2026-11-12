using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;
using Seamless.Database.Query;
using Seamless.Database.Gateway.Tds;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsStrictLoginTests{
    [Fact,Trait("Category","Integration")]
    public async Task SqlClientSendsFedAuthOnlyAfterStrictTlsCertificateValidation(){
        const string token="seamless-tds-wire-fixture";
        using var key=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var selfSigned=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
        using var certificate=X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);
        var certificateKey=GetWindowsKey(certificate);
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");
        var pin=Path.Combine(root,Guid.NewGuid().ToString("N")+".cer");
        try{
            await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert));
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var received=new TaskCompletionSource<TdsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var applicationConnections=0;
            await using var host=new KestrelTcpHost(new(IPAddress.Loopback,0),async connection=>{
                Interlocked.Increment(ref applicationConnections);
                using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionClosed,timeout.Token);
                var input=connection.Transport.Input;
                var writer=new TdsPacketWriter(connection.Transport.Output);
                var preLogin=await TdsPacketReader.ReadMessageAsync(input,lifetime.Token);
                if(preLogin is not{PacketType:TdsConstants.PacketTypePreLogin}){return;}
                await writer.SendPreLoginResponseAsync(true,lifetime.Token);
                var login=await TdsPacketReader.ReadMessageAsync(input,lifetime.Token);
                if(login is not{} message){return;}
                received.TrySetResult(message);
                await writer.SendErrorAsync("Login failed.",lifetime.Token,18456);
            },certificate,new ConnectionQuota(2),error=>received.TrySetException(error));
            await host.StartAsync(timeout.Token);
            var settings=new SqlConnectionStringBuilder{
                DataSource=$"tcp:127.0.0.1,{host.Port}",InitialCatalog="fixture",Encrypt=SqlConnectionEncryptOption.Strict,
                Pooling=false,ConnectTimeout=5,ConnectRetryCount=0
            };
            // 信頼されていない試験証明書では LOGIN7 を送らない。
            await using(var unknown=new SqlConnection(settings.ConnectionString){AccessToken=token}){
                var untrusted=await Assert.ThrowsAsync<SqlException>(()=>unknown.OpenAsync(timeout.Token));
                Assert.NotEqual(18456,untrusted.Number);
                Assert.False(received.Task.IsCompleted);
            }
            settings["Server Certificate"]=pin;
            await using(var trusted=new SqlConnection(settings.ConnectionString){AccessToken=token}){
                var rejected=await Assert.ThrowsAsync<SqlException>(()=>trusted.OpenAsync(timeout.Token));
                Assert.True(rejected.Number==18456,rejected.Message);
            }
            var acceptedLogin=await received.Task.WaitAsync(timeout.Token);
            Assert.Equal(1,Volatile.Read(ref applicationConnections));
            Assert.Equal(0x10,acceptedLogin.PacketType);
            Assert.Equal(token,TdsFedAuthLogin.ReadToken(acceptedLogin.Payload));
            Assert.Equal(18456,Assert.Throws<SqlQueryException>(()=>TdsFedAuthLogin.ReadToken(acceptedLogin.Payload.AsSpan(0,acceptedLogin.Payload.Length-1))).Number);
            var malformed=acceptedLogin.Payload.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(56),ushort.MaxValue);
            Assert.Equal(18456,Assert.Throws<SqlQueryException>(()=>TdsFedAuthLogin.ReadToken(malformed)).Number);
        }finally{
            File.Delete(pin);
            certificate.Dispose();
            if(OperatingSystem.IsWindows()&&certificateKey is{} keyIdentity&&CngKey.Exists(keyIdentity.Name,keyIdentity.Provider)){
                using var owned=CngKey.Open(keyIdentity.Name,keyIdentity.Provider);
                owned.Delete();
            }
        }
    }

    private static (string Name,CngProvider Provider)? GetWindowsKey(X509Certificate2 certificate){
        if(!OperatingSystem.IsWindows()){return null;}
        using var privateKey=certificate.GetRSAPrivateKey();
        if(privateKey is RSACng cng&&cng.Key.KeyName is{} name&&cng.Key.Provider is{} provider){return(name,provider);}
        return null;
    }

}
