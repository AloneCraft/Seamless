using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Connection=Seamless.Management.LocalServerConnection;

namespace Management.Tests;

public sealed class LocalServerConnection{
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
