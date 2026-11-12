using Seamless.Authentication;
using Seamless.Security;
using Session=Seamless.Authentication.OidcAuthorizationSession;

namespace Management.Tests;

public sealed class OidcAuthorizationSession{
    private static readonly Guid Server=Guid.Parse("10000000-0000-0000-0000-000000000001"),Database=Guid.Parse("10000000-0000-0000-0000-000000000002"),Table=Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static Seamless.Security.AuthorizationPolicySnapshot Policy(Guid authority,long version,bool allow=true){
        SecurityPermission permission=SecurityPermission.None;if(allow){permission=SecurityPermission.Read|SecurityPermission.Update;}
        var role=new SecurityRole(Guid.NewGuid(),"developer",[new(Database,permission)]);
        return new(version,[new(Server,null,SecurityResourceKind.Server,"server"),new(Database,Server,SecurityResourceKind.Database,"db"),new(Table,Database,SecurityResourceKind.Table,"items")],[role],[new(new(authority,"developers"),SecurityPrincipalKind.Group,role.Id)]);
    }
    [Fact]public async Task MembershipRefreshIsSingleFlightAndFailureBlocksOnlyNewWork(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var clock=new Clock();using var verifier=new OidcTokenVerifier(issuer.Settings,clock);using var manager=new AuthorizationManager(Policy(issuer.Id,1),clock);
        using var session=await Session.AuthenticateAsync(verifier,manager,issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));using var transaction=await session.BeginTransactionAsync();
        for(var i=0;i<10;i++){using var operation=await session.BeginOperationAsync();operation.Demand(Table,SecurityPermission.Read);}Assert.Equal(1,issuer.MembershipChecks);
        clock.Advance(TimeSpan.FromMinutes(5));var leases=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>session.BeginOperationAsync().AsTask()));foreach(var lease in leases){lease.Demand(Table,SecurityPermission.Read);lease.Dispose();}Assert.Equal(2,issuer.MembershipChecks);
        issuer.MembershipUnavailable=true;clock.Advance(TimeSpan.FromMinutes(5));Assert.Equal("MembershipUnavailable",(await Assert.ThrowsAsync<OidcAuthenticationException>(()=>session.BeginOperationAsync().AsTask())).Code);transaction.Demand(Table,SecurityPermission.Update);
        issuer.MembershipUnavailable=false;issuer.Groups=["readers"];using(var next=await session.BeginOperationAsync()){Assert.Equal("NotGranted",Assert.Throws<AuthorizationException>(()=>next.Demand(Table,SecurityPermission.Read)).Code);}transaction.Demand(Table,SecurityPermission.Update);
        issuer.Groups=["developers"];clock.Advance(TimeSpan.FromMinutes(5));using var recovered=await session.BeginOperationAsync();recovered.Demand(Table,SecurityPermission.Read);
    }
    [Fact]public async Task ExpiredSignInAllowsOnlyExistingTransactionUntilItsDeadline(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var clock=new Clock();using var verifier=new OidcTokenVerifier(issuer.Settings,clock);using var manager=new AuthorizationManager(Policy(issuer.Id,1),clock);
        using var session=await Session.AuthenticateAsync(verifier,manager,issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database",lifetime:TimeSpan.FromMinutes(1)));using var transaction=await session.BeginTransactionAsync();
        clock.Advance(TimeSpan.FromMinutes(2));transaction.Demand(Table,SecurityPermission.Update);Assert.False(transaction.CancellationToken.IsCancellationRequested);
        Assert.Equal("AuthenticationExpired",(await Assert.ThrowsAsync<AuthorizationException>(()=>session.BeginOperationAsync().AsTask())).Code);Assert.Equal(1,issuer.MembershipChecks);
        await session.ReauthenticateAsync(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));using(var next=await session.BeginOperationAsync()){next.Demand(Table,SecurityPermission.Read);}
        clock.Advance(TimeSpan.FromMinutes(28));Assert.Throws<OperationCanceledException>(()=>transaction.Demand(Table,SecurityPermission.Update));
    }
    [Fact]public async Task PolicyChangesAndEmergencyDisableShareTheSameAuthenticatedSubject(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();var clock=new Clock();using var verifier=new OidcTokenVerifier(issuer.Settings,clock);using var manager=new AuthorizationManager(Policy(issuer.Id,1),clock);
        var token=issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database");using var first=await Session.AuthenticateAsync(verifier,manager,token);using var second=await Session.AuthenticateAsync(verifier,manager,token);using var running=await first.BeginTransactionAsync();
        manager.PublishPolicy(Policy(issuer.Id,2,false));running.Demand(Table,SecurityPermission.Update);using(var next=await second.BeginOperationAsync()){Assert.Throws<AuthorizationException>(()=>next.Demand(Table,SecurityPermission.Read));Assert.Equal(2,next.Audit.PolicyVersion);}Assert.Equal(1,running.Audit.PolicyVersion);
        manager.SetDisabled(first.Subject,true);Assert.True(running.CancellationToken.IsCancellationRequested);Assert.Equal("AccountDisabled",(await Assert.ThrowsAsync<AuthorizationException>(()=>second.BeginOperationAsync().AsTask())).Code);
        Assert.Equal("AccountDisabled",(await Assert.ThrowsAsync<AuthorizationException>(()=>Session.AuthenticateAsync(verifier,manager,token))).Code);
        manager.SetDisabled(first.Subject,false);await Assert.ThrowsAsync<AuthorizationException>(()=>first.ReauthenticateAsync(issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:"another-user",scope:"seamless.database")));Assert.Equal("dev-user",first.Subject.Value);
        first.Dispose();await Assert.ThrowsAsync<ObjectDisposedException>(()=>first.BeginOperationAsync().AsTask());
    }
    private sealed class Clock:TimeProvider{
        private DateTimeOffset now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>this.now;
        public void Advance(TimeSpan duration)=>this.now+=duration;
    }
}
