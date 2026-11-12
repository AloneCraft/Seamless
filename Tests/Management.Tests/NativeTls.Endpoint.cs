using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Server;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Fact]public async Task NativeEndpointRequiresAuthenticationBeforeOpeningStorage(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var storage=Path.Combine(root,"db");var work=Path.Combine(root,"work");var cache=Path.Combine(root,"cache");var configuration=Path.Combine(root,"gateway.json");
        await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(new{StorageDirectory=storage,WorkingDirectory=work,CacheDirectory=cache,GeneratorAssemblyPath="not-needed-before-endpoint-validation",Endpoints=new{NativeListenAddress="::1",NativePort=0,TdsPort=0,HttpPort=0}}));
        var failure=await Record.ExceptionAsync(()=>Program.ServeGatewayAsync(["--gateway",configuration]));
        Assert.Contains("NativeListenAddress",Assert.IsAssignableFrom<ArgumentException>(failure).Message,StringComparison.Ordinal);
        Assert.False(Directory.Exists(storage));Assert.False(Directory.Exists(work));Assert.False(Directory.Exists(cache));
    }
    [Theory][InlineData("no-limits")][InlineData("zero-connections")][InlineData("zero-handshake-capacity")][InlineData("legacy-limits")][InlineData("buffer-limit")][InlineData("negative-buffer")][InlineData("zero-authentication-capacity")][InlineData("infinite-authentication-timeout")][InlineData("zero-identity-verification-capacity")][InlineData("null-address")][InlineData("invalid-port")][InlineData("no-certificate")]
    public Task NativeEndpointRejectsInvalidDeclaredProtectionBeforeOpeningStores(string kind)=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var native=new GatewayNativeAuthorizationConfiguration([]);var expected="";
        var endpoints=new Dictionary<string,object?>{{"NativeListenAddress","::1"},{"NativePort",0}};
        switch(kind){
            case "no-limits":native=native with{Limits=null!};expected="finite limits";break;
            case "zero-connections":native=native with{Limits=new(){MaximumConnections=0}};expected="MaximumConnections";break;
            case "zero-handshake-capacity":native=native with{Limits=new(){MaximumConcurrentHandshakes=0}};expected="MaximumConcurrentHandshakes";break;
            case "legacy-limits":endpoints["NativeMaximumConnections"]=1;expected="legacy native limits";break;
            case "buffer-limit":endpoints["MaximumReceiveBufferBytes"]=native.Limits.MaximumReceiveBufferBytes+1;expected="Shared buffer limits";break;
            case "negative-buffer":endpoints["MaximumPendingWriteBytes"]=-1;expected="Shared buffer limits";break;
            case "zero-authentication-capacity":native=native with{MaximumConcurrentAuthentications=0};expected="authentication";break;
            case "infinite-authentication-timeout":native=native with{AuthenticationTimeout=TimeSpan.MaxValue};expected="authentication";break;
            case "zero-identity-verification-capacity":expected="MaximumConcurrentIdentityVerifications";break;
            case "null-address":endpoints["NativeListenAddress"]=null;expected="ListenAddress";break;
            case "invalid-port":endpoints["NativePort"]=-1;expected="NativePort";break;
            case "no-certificate":expected="TLS certificate";break;
            default:throw new ArgumentException("Unknown test case.");
        }
        var storage=Path.Combine(root,"db");var work=Path.Combine(root,"work");var cache=Path.Combine(root,"cache");var management=Path.Combine(root,"management");var ready=Path.Combine(root,"ready.json");var pfx=Path.Combine(root,"native.pfx");
        var protection=new GatewayAuthorizationConfiguration(management,Guid.NewGuid(),Guid.NewGuid(),[]){ProtectHttp=false,Native=native};
        if(kind=="zero-identity-verification-capacity"){protection=protection with{MaximumConcurrentIdentityVerifications=0};}
        string? certificatePath="native.pfx";if(kind=="no-certificate"){certificatePath=null;}
        await File.WriteAllBytesAsync(pfx,certificate.Export(X509ContentType.Pfx,Environment.GetEnvironmentVariable("SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD")));
        try{
            var configuration=Path.Combine(root,"gateway.json");
            await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(new{StorageDirectory=storage,WorkingDirectory=work,CacheDirectory=cache,GeneratorAssemblyPath=Path.Combine(root,"not-needed-before-validation.dll"),NativeCertificatePath=certificatePath,Authorization=protection,Endpoints=endpoints}));
            var failure=await Record.ExceptionAsync(()=>Program.ServeGatewayAsync(["--gateway",configuration,"--ready-file",ready]));
            Assert.NotNull(failure);Assert.Contains(expected,failure.Message,StringComparison.OrdinalIgnoreCase);
            foreach(var directory in new[]{storage,work,cache,management}){Assert.False(Directory.Exists(directory));}Assert.False(File.Exists(ready));
        }finally{File.Delete(pfx);}
    });
    [Theory][InlineData("no-auth")][InlineData("no-certificate")][InlineData("no-limits")]
    public Task NativeEndpointHasNoUnprotectedOptionsEscape(string kind)=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);
        var options=new GatewayServerOptions{TdsEnabled=false,HttpEnabled=false,NativePort=0,NativeListenAddress=IPAddress.Any,NativeCertificate=certificate,NativeAuthorization=host.Authorization,NativeAuthenticatedLimits=new()};
        options.ValidateNativeTransport(true);
        switch(kind){case "no-auth":options.NativeAuthorization=null;break;case "no-certificate":options.NativeCertificate=null;break;case "no-limits":options.NativeAuthenticatedLimits=null;break;default:throw new ArgumentException("Unknown test case.");}
        Assert.ThrowsAny<ArgumentException>(()=>new GatewayServer(host.Fixture.Registry,options));
    });
    [Fact]public Task NativeEndpointCopiesAddressesAndKeepsExistingApiDefaults()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var host=new AuthenticatedFixture(certificate,issuer);
        using var legacy=NativeQueryServer.CreateAuthenticated(host.Fixture.Registry,certificate,host.Authorization,new());
        Assert.Equal(14335,legacy.Port);Assert.Equal(IPAddress.Loopback,legacy.ListenAddress);Assert.Equal(IPAddress.Loopback,host.Fixture.Server.ListenAddress);
        using var plain=new NativeQueryServer(host.Fixture.Registry,0);using var tls=NativeQueryServer.CreateTls(host.Fixture.Registry,certificate,0);
        Assert.Equal(IPAddress.Loopback,plain.ListenAddress);Assert.Equal(IPAddress.Loopback,tls.ListenAddress);
        var address=new IPAddress(IPAddress.IPv6Loopback.GetAddressBytes(),7);var endpoint=new IPEndPoint(address,0);
        using var explicitServer=NativeQueryServer.CreateAuthenticated(host.Fixture.Registry,endpoint,certificate,host.Authorization,new());
        address.ScopeId=8;endpoint.Address=IPAddress.Loopback;endpoint.Port=1;var returned=explicitServer.ListenAddress;returned.ScopeId=9;
        Assert.Equal(7,explicitServer.ListenAddress.ScopeId);Assert.Equal(0,explicitServer.Port);
    });
    [Fact]public Task NativeEndpointUsesAuthenticatedIpv6AndKeepsItAcrossRestart()=>ManagementTransport.WithServerCertificate(async certificate=>{
        Assert.True(Socket.OSSupportsIPv6,"An IPv6 loopback environment is required for this test.");
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var endpoint=new IPEndPoint(IPAddress.IPv6Loopback,0);using var host=new AuthenticatedFixture(certificate,issuer,endpoint:endpoint);
        endpoint.Address=IPAddress.Loopback;endpoint.Port=1;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for(var round=0;round<2;round++){
            Assert.Equal(IPAddress.IPv6Loopback,host.Fixture.Server.ListenAddress);
            await using(var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")),host:"::1",port:host.Fixture.Server.Port,trustedRoot:certificate)){
                Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
            }
            var calls=host.Fixture.Module.Calls;
            await using(var unauthenticated=NativeQueryClient.ForTls("::1",host.Fixture.Server.Port,certificate)){
                await Assert.ThrowsAnyAsync<Exception>(()=>unauthenticated.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
            }
            Assert.Equal(calls,host.Fixture.Module.Calls);await host.Fixture.Server.StopAsync();if(round==0){host.Fixture.Server.Start();}
        }
    },includeIPv6LoopbackAddress:true);
}
