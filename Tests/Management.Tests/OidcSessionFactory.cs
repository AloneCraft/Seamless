using Seamless.Authentication;
using Seamless.Security;
using Session=Seamless.Authentication.OidcAuthorizationSession;

namespace Management.Tests;

public sealed class OidcSessionFactory{
    private static readonly Guid Server=Guid.Parse("b49a425a-cfc2-4989-bd75-d7cfd09be401"),Database=Guid.Parse("b49a425a-cfc2-4989-bd75-d7cfd09be402");
    private static Seamless.Security.AuthorizationPolicySnapshot Policy(Guid authority,long version=1,bool allow=true){
        var permission=SecurityPermission.None;if(allow){permission=SecurityPermission.Read;}
        var role=new SecurityRole(Guid.NewGuid(),"reader",[new(Database,permission)]);
        return new(version,[new(Server,null,SecurityResourceKind.Server,"server"),new(Database,Server,SecurityResourceKind.Database,"db")],[role],[new(new(authority,"developers"),SecurityPrincipalKind.Group,role.Id)]);
    }
    private static AuthenticatedIdentity Identity(Guid authority,string name="dev-user")=>new(new(authority,name),name,[new(authority,"developers")],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow);

    [Fact]public void DisposingCoreSessionPreservesItsSiblingAndExistingLease(){
        var authority=Guid.NewGuid();using var manager=new AuthorizationManager(Policy(authority));var identity=Identity(authority);
        IAuthorizationSession first=manager.CreateSession(identity);using var sibling=manager.CreateSession(identity);using var running=first.BeginTransaction();
        first.Dispose();first.Dispose();Assert.Throws<ObjectDisposedException>(()=>first.BeginOperation());Assert.Throws<ObjectDisposedException>(()=>first.UpdateValidatedIdentity(identity));
        running.Demand(Database,SecurityPermission.Read);using var next=sibling.BeginOperation();next.Demand(Database,SecurityPermission.Read);
    }

    [Fact]public async Task FactoryKeepsPolicyLeaseAndOwnsOnlyTheReturnedSession(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));
        TrackingSession? tracked=null;AuthenticatedIdentity? verified=null;
        using var session=await Session.AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>{verified=identity;tracked=new(manager.CreateSession(identity));return ValueTask.FromResult<IAuthorizationSession>(tracked);},issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        using var transaction=await session.BeginTransactionAsync();manager.PublishPolicy(Policy(issuer.Id,2,false));transaction.Demand(Database,SecurityPermission.Read);
        using(var next=await session.BeginOperationAsync()){Assert.Equal(2,next.Audit.PolicyVersion);Assert.Throws<AuthorizationException>(()=>next.Demand(Database,SecurityPermission.Read));}
        session.Dispose();session.Dispose();Assert.Equal(1,tracked!.DisposeCalls);transaction.Demand(Database,SecurityPermission.Read);
        using var sibling=manager.CreateSession(verified!);using var operation=sibling.BeginOperation();Assert.Equal(2,operation.Audit.PolicyVersion);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task FactorySubjectMustMatchBothAuthorityAndValue(bool otherAuthority){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));
        var authority=issuer.Id;var name="other-user";if(otherAuthority){authority=Guid.NewGuid();name="dev-user";}
        var tracked=new TrackingSession(manager.CreateSession(Identity(authority,name)));
        var error=await Assert.ThrowsAsync<AuthorizationException>(()=>Session.AuthenticateWithSessionFactoryAsync(verifier,(_,_)=>ValueTask.FromResult<IAuthorizationSession>(tracked),issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")));
        Assert.Equal("IdentityMismatch",error.Code);Assert.Equal(1,tracked.DisposeCalls);Assert.Null(tracked.LastLease);
    }

    [Fact]public async Task LateFactoryResultIsDisposedWhenAuthenticationWasCancelled(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));using var cancelled=new CancellationTokenSource();
        var entered=new TaskCompletionSource<AuthenticatedIdentity>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<IAuthorizationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=Session.AuthenticateWithSessionFactoryAsync(verifier,async(identity,_)=>{entered.SetResult(identity);return await release.Task;},issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"),cancelled.Token);
        var identity=await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));var tracked=new TrackingSession(manager.CreateSession(identity));cancelled.Cancel();release.SetResult(tracked);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);Assert.Equal(1,tracked.DisposeCalls);Assert.Null(tracked.LastLease);
    }

    [Fact]public async Task AdmissionLeaseForAnotherSubjectIsRejectedAndReleased(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));
        using var other=manager.CreateSession(Identity(issuer.Id,"another-user"));TrackingSession? tracked=null;
        var error=await Assert.ThrowsAsync<AuthorizationException>(()=>Session.AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>{tracked=new(manager.CreateSession(identity)){Operation=other.BeginOperation};return ValueTask.FromResult<IAuthorizationSession>(tracked);},issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")));
        Assert.Equal("IdentityMismatch",error.Code);Assert.Equal(1,tracked!.DisposeCalls);Assert.Throws<ObjectDisposedException>(()=>tracked.LastLease!.ThrowIfCancellationRequested());
    }

    [Fact]public async Task TransactionLeaseRequiresADeadline(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));TrackingSession? tracked=null;
        using var session=await Session.AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>{var inner=manager.CreateSession(identity);tracked=new(inner){Transaction=inner.BeginOperation};return ValueTask.FromResult<IAuthorizationSession>(tracked);},issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        var error=await Assert.ThrowsAsync<AuthorizationException>(()=>session.BeginTransactionAsync().AsTask());Assert.Equal("InvalidTransactionLease",error.Code);Assert.Throws<ObjectDisposedException>(()=>tracked!.LastLease!.ThrowIfCancellationRequested());
        using var operation=await session.BeginOperationAsync();operation.Demand(Database,SecurityPermission.Read);
    }

    [Fact]public async Task ReauthenticationChecksSubjectBeforeCallingAnExternalSession(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));TrackingSession? tracked=null;
        using var session=await Session.AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>{tracked=new(manager.CreateSession(identity)){IgnoreIdentityUpdate=true};return ValueTask.FromResult<IAuthorizationSession>(tracked);},issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        var error=await Assert.ThrowsAsync<AuthorizationException>(()=>session.ReauthenticateAsync(issuer.CreateToken(OidcDevelopmentIssuer.Audience,subject:"another-user",scope:"seamless.database")));
        Assert.Equal("IdentityMismatch",error.Code);Assert.Equal(0,tracked!.IdentityUpdates);Assert.Equal("dev-user",session.Subject.Value);
    }

    [Fact]public async Task DisposalDuringIdentityUpdateCannotRestoreTheAccessToken(){
        await using var issuer=await OidcDevelopmentIssuer.StartAsync();using var verifier=new OidcTokenVerifier(issuer.Settings);using var manager=new AuthorizationManager(Policy(issuer.Id));
        using var entered=new ManualResetEventSlim();using var resume=new ManualResetEventSlim();
        using var session=await Session.AuthenticateWithSessionFactoryAsync(verifier,(identity,_)=>ValueTask.FromResult<IAuthorizationSession>(new TrackingSession(manager.CreateSession(identity)){Updated=_=>{entered.Set();if(!resume.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException("The disposal test did not release the identity update.");}}}),issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database"));
        var pending=Task.Run(()=>session.ReauthenticateAsync(issuer.CreateToken(OidcDevelopmentIssuer.Audience,scope:"seamless.database")));
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)),"The identity update did not reach the controlled boundary.");session.Dispose();resume.Set();
            var error=await Record.ExceptionAsync(()=>pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var field=typeof(Session).GetField("accessToken",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            Assert.True(field.GetValue(session) is null,"The disposed session retained access credentials.");Assert.IsType<ObjectDisposedException>(error);
        }finally{resume.Set();}
    }

    private sealed class TrackingSession(IAuthorizationSession inner):IAuthorizationSession{
        private int disposed;
        public SecuritySubjectId Subject=>inner.Subject;
        public Func<CancellationToken,AuthorizationLease>? Operation{get;init;}
        public Func<CancellationToken,AuthorizationLease>? Transaction{get;init;}
        public bool IgnoreIdentityUpdate{get;init;}
        public Action<AuthenticatedIdentity>? Updated{get;init;}
        public int IdentityUpdates{get;private set;}
        public int DisposeCalls{get;private set;}
        public AuthorizationLease? LastLease{get;private set;}
        public AuthorizationLease BeginOperation(CancellationToken cancellationToken=default){if(this.Operation is{} operation){return this.LastLease=operation(cancellationToken);}return this.LastLease=inner.BeginOperation(cancellationToken);}
        public AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default){if(this.Transaction is{} transaction){return this.LastLease=transaction(cancellationToken);}return this.LastLease=inner.BeginTransaction(cancellationToken);}
        public void UpdateValidatedIdentity(AuthenticatedIdentity identity){this.IdentityUpdates++;if(!this.IgnoreIdentityUpdate){inner.UpdateValidatedIdentity(identity);}this.Updated?.Invoke(identity);}
        public void MarkMembershipUnavailable()=>inner.MarkMembershipUnavailable();
        public void Dispose(){if(Interlocked.Exchange(ref this.disposed,1)==0){this.DisposeCalls++;inner.Dispose();}}
    }
}
