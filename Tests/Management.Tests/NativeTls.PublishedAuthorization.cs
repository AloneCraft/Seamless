using System.Text.Json;
using Seamless.Authentication;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using Seamless.Database.Server;
using Seamless.Security;
using PolicySnapshot=Seamless.Security.AuthorizationPolicySnapshot;

namespace Management.Tests;

public sealed partial class NativeTls{
    [Fact]public Task ProtectedRpcChecksTheCurrentSubjectBeforeEveryExecution()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var policy=new TableQueryPolicy(issuer.Id);
        using var manager=new AuthorizationManager(policy.Build(1,allowWrite:true));
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        using var fixture=new Fixture(certificate,authorization:policy.Authenticate(verifier,manager));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        NativeQueryClient Client(string subject)=>NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:subject,scope:"seamless.database")),port:fixture.Server.Port,trustedRoot:certificate);
        await using var writer=Client("dev-user");
        await using var reader=Client("other-user");
        Task<int> Insert(NativeQueryClient client,int id)=>client.ExecuteScalarAsync<int,int>("tls","insert","fixture",Fingerprint,id,timeout.Token);
        await Assert.ThrowsAsync<NativeQueryException>(()=>Insert(reader,7));
        Assert.Equal(0,fixture.Module.Calls);
        Assert.Equal(1,await Insert(writer,7));
        await Assert.ThrowsAsync<NativeQueryException>(()=>Insert(reader,8));
        Assert.Equal(1,fixture.Module.Calls);
        Assert.Equal(1,await reader.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        Assert.Equal(0,await reader.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,8,timeout.Token));
        var calls=fixture.Module.Calls;
        manager.PublishPolicy(policy.Build(2,allowWrite:true,foreignTable:true));
        await Assert.ThrowsAsync<NativeQueryException>(()=>Insert(writer,8));
        Assert.Equal(calls,fixture.Module.Calls);
    });

    [Fact]public Task ProtectedRpcUsesTheTransactionPolicyUntilCommit()=>ManagementTransport.WithServerCertificate(async certificate=>{
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();
        var policy=new TableQueryPolicy(issuer.Id);
        using var manager=new AuthorizationManager(policy.Build(1,allowWrite:true));
        using var verifier=new OidcTokenVerifier(issuer.Settings);
        using var fixture=new Fixture(certificate,authorization:policy.Authenticate(verifier,manager));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=NativeQueryClient.ForAuthenticatedTls(_=>ValueTask.FromResult(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")),port:fixture.Server.Port,trustedRoot:certificate);
        await using(var transaction=await client.BeginTransactionAsync("fixture",timeout.Token)){
            manager.PublishPolicy(policy.Build(2,allowWrite:false));
            var rows=0;
            await foreach(var result in transaction.QueryAsync<int,int>("tls","insert",Fingerprint,9,timeout.Token)){Assert.Equal(1,result);rows++;}
            Assert.Equal(1,rows);
            await transaction.CommitAsync(timeout.Token);
        }
        var calls=fixture.Module.Calls;
        await Assert.ThrowsAsync<NativeQueryException>(()=>client.ExecuteScalarAsync<int,int>("tls","insert","fixture",Fingerprint,10,timeout.Token));
        Assert.Equal(calls,fixture.Module.Calls);
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,9,timeout.Token));
        Assert.Equal(0,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,10,timeout.Token));
    });

    [Theory][InlineData("Lambdas")][InlineData("UnknownPermissionOption")]
    public void UnsupportedNativeAuthorizationSettingsAreRejected(string property){
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<GatewayNativeAuthorizationConfiguration>("{\"Queries\":[],\""+property+"\":[]}"));
    }

    private sealed class TableQueryPolicy(Guid authority){
        private readonly Guid server=Guid.NewGuid(),database=Guid.NewGuid(),schema=Guid.NewGuid(),table=Guid.NewGuid(),writer=Guid.NewGuid(),reader=Guid.NewGuid(),outside=Guid.NewGuid();
        public PolicySnapshot Build(long version,bool allowWrite,bool foreignTable=false){
            var writerPermissions=SecurityPermission.Read;
            if(allowWrite){writerPermissions|=SecurityPermission.Insert;}
            var tableParent=this.schema;
            if(foreignTable){tableParent=this.outside;}
            return new(version,[
                new(this.server,null,SecurityResourceKind.Server,"server"),new(this.database,this.server,SecurityResourceKind.Database,"fixture"),
                new(this.schema,this.database,SecurityResourceKind.Schema,"dbo"),new(this.outside,this.server,SecurityResourceKind.Database,"outside"),
                new(this.table,tableParent,SecurityResourceKind.Table,"Rows")
            ],[
                new(this.writer,"writer",[new(this.database,SecurityPermission.Discover),new(this.table,writerPermissions)]),
                new(this.reader,"reader",[new(this.database,SecurityPermission.Discover|SecurityPermission.Insert),new(this.table,SecurityPermission.Read,SecurityPermission.Insert)])
            ],[
                new(new(authority,"dev-user"),SecurityPrincipalKind.User,this.writer),new(new(authority,"other-user"),SecurityPrincipalKind.User,this.reader)
            ]);
        }
        public NativeGatewayAuthorization Authenticate(OidcTokenVerifier verifier,AuthorizationManager manager)=>new(verifier,
            (identity,_)=>ValueTask.FromResult<IAuthorizationSession>(manager.CreateSession(identity)),
            (lease,_)=>lease.Demand(this.database,SecurityPermission.Discover),
            (lease,_,_,definition)=>{
                if(lease.Snapshot.Resource(this.table)?.ParentId!=this.schema){throw new AuthorizationException("ForeignTable","The table belongs to another database.");}
                var permission=SecurityPermission.Read;
                if(definition.Id=="insert"){permission=SecurityPermission.Insert;}
                lease.Demand(this.table,permission);
            },static(_,_)=>{});
    }
}
