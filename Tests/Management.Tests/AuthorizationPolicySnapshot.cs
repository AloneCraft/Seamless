using Seamless.Security;

namespace Management.Tests;

public sealed class AuthorizationPolicySnapshot{
    private static readonly Guid Authority=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Server=Guid.Parse("00000000-0000-0000-0000-000000000001"),Database=Guid.Parse("00000000-0000-0000-0000-000000000002"),Schema=Guid.Parse("00000000-0000-0000-0000-000000000003"),Table=Guid.Parse("00000000-0000-0000-0000-000000000004"),Other=Guid.Parse("00000000-0000-0000-0000-000000000005"),Query=Guid.Parse("00000000-0000-0000-0000-000000000006");
    private static readonly SecuritySubjectId User=new(Authority,"user"),Group=new(Authority,"readers");
    private static SecurityResource[] Resources(string name="Payroll")=>[new(Server,null,SecurityResourceKind.Server,"server"),new(Database,Server,SecurityResourceKind.Database,"db"),new(Schema,Database,SecurityResourceKind.Schema,"schema"),new(Table,Schema,SecurityResourceKind.Table,name),new(Other,Server,SecurityResourceKind.Database,"other"),new(Query,Database,SecurityResourceKind.Query,"summary")];
    private static AuthenticatedIdentity Identity(DateTimeOffset now,params SecuritySubjectId[] groups)=>new(User,"same display",groups,now.AddHours(1),now);
    private static Seamless.Security.AuthorizationPolicySnapshot Policy(long version,params PermissionRule[] rules){var role=new SecurityRole(Guid.NewGuid(),"role",rules);return new(version,Resources(),[role],[new(User,SecurityPrincipalKind.User,role.Id)]);}

    [Fact]public void Resolve(){
        var role=new SecurityRole(Guid.NewGuid(),"reader",[new(Database,SecurityPermission.Discover|SecurityPermission.Read),new(Table,SecurityPermission.None,SecurityPermission.Read),new(Query,SecurityPermission.Execute)]);
        var policy=new Seamless.Security.AuthorizationPolicySnapshot(1,Resources(),[role],[new(Group,SecurityPrincipalKind.Group,role.Id)]);
        var snapshot=policy.Resolve(Identity(DateTimeOffset.UtcNow,Group));
        Assert.True(snapshot.Evaluate(Schema,SecurityPermission.Read).Allowed);Assert.True(snapshot.Evaluate(Table,SecurityPermission.Discover).Allowed);
        Assert.Equal("ExplicitDeny",snapshot.Evaluate(Table,SecurityPermission.Read).Code);Assert.True(snapshot.Evaluate(Query,SecurityPermission.Execute).Allowed);
        Assert.False(snapshot.Evaluate(Query,SecurityPermission.ReadSource).Allowed);Assert.False(snapshot.Evaluate(Other,SecurityPermission.Read).Allowed);Assert.False(snapshot.Evaluate(Guid.NewGuid(),SecurityPermission.Read).Allowed);Assert.False(snapshot.Evaluate(Database,SecurityPermission.None).Allowed);
        Assert.False(policy.Resolve(Identity(DateTimeOffset.UtcNow)).Evaluate(Database,SecurityPermission.Read).Allowed);
        var outsider=new AuthenticatedIdentity(new(Guid.NewGuid(),User.Value),"same display",[],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow);Assert.False(policy.Resolve(outsider).Evaluate(Database,SecurityPermission.Read).Allowed);
        var changedCase=new AuthenticatedIdentity(new(Authority,"USER"),"same display",[],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow);Assert.False(Policy(1,new PermissionRule(Database,SecurityPermission.Read)).Resolve(changedCase).Evaluate(Database,SecurityPermission.Read).Allowed);
    }
    [Fact]public void DenyWinsAcrossRolesAndNamesAreNotKeys(){
        var reader=new SecurityRole(Guid.NewGuid(),"reader",[new(Database,SecurityPermission.Read)]);var denied=new SecurityRole(Guid.NewGuid(),"denied",[new(Schema,SecurityPermission.None,SecurityPermission.Read)]);
        var bindings=new[]{new RoleBinding(User,SecurityPrincipalKind.User,reader.Id),new RoleBinding(Group,SecurityPrincipalKind.Group,denied.Id)};
        var original=new Seamless.Security.AuthorizationPolicySnapshot(1,Resources(),[reader,denied],bindings);var renamed=new Seamless.Security.AuthorizationPolicySnapshot(2,Resources("renamed"),[reader,denied],bindings);
        foreach(var policy in new[]{original,renamed}){Assert.Equal("ExplicitDeny",policy.Resolve(Identity(DateTimeOffset.UtcNow,Group)).Evaluate(Table,SecurityPermission.Read).Code);Assert.True(policy.IsWithin(Table,Database));Assert.False(policy.IsWithin(Table,Other));}
    }
    [Fact]public void InvalidResourceAndServerGrantsAreRejected(){
        Assert.Throws<ArgumentException>(()=>new Seamless.Security.AuthorizationPolicySnapshot(1,Resources().Select(item=>{if(item.Id==Schema){return item with{ParentId=Table};}return item;}),[],[]));
        Assert.Throws<ArgumentException>(()=>Policy(1,new PermissionRule(Database,SecurityPermission.ManageServer)));
        Assert.Throws<ArgumentException>(()=>new AuthenticatedIdentity(User,"name",[new(Guid.NewGuid(),"readers")],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow));
        var rules=new List<PermissionRule>{new(Database,SecurityPermission.Read)};var role=new SecurityRole(Guid.NewGuid(),"reader",rules);var bindings=new[]{new RoleBinding(User,SecurityPrincipalKind.User,role.Id)};var policy=new Seamless.Security.AuthorizationPolicySnapshot(1,Resources(),[role],bindings);
        rules.Clear();bindings[0]=new(new(Authority,"other"),SecurityPrincipalKind.User,role.Id);Assert.True(policy.Resolve(Identity(DateTimeOffset.UtcNow)).Evaluate(Table,SecurityPermission.Read).Allowed);
    }
    [Fact]public void BeginningAnOperationPinsItsPermissions(){
        var clock=new Clock();using var manager=new AuthorizationManager(Policy(1,new PermissionRule(Database,SecurityPermission.Read)),clock);var session=manager.CreateSession(Identity(clock.GetUtcNow()));
        using var old=session.BeginOperation();manager.PublishPolicy(Policy(2,new PermissionRule(Database,SecurityPermission.None,SecurityPermission.Read)));old.Demand(Table,SecurityPermission.Read);
        using var next=session.BeginOperation();Assert.Equal("ExplicitDeny",Assert.Throws<AuthorizationException>(()=>next.Demand(Table,SecurityPermission.Read)).Code);Assert.Equal(1,old.Audit.PolicyVersion);Assert.Equal(2,next.Audit.PolicyVersion);
        Assert.Throws<AuthorizationException>(()=>manager.PublishPolicy(Policy(2)));
    }
    [Fact]public void MembershipFailureAndExpirationStopNewOperations(){
        var clock=new Clock();using var manager=new AuthorizationManager(Policy(1,new PermissionRule(Database,SecurityPermission.Read)),clock);var session=manager.CreateSession(Identity(clock.GetUtcNow()));using var running=session.BeginOperation();
        clock.Advance(TimeSpan.FromMinutes(5));Assert.Equal("MembershipRefreshRequired",Assert.Throws<AuthorizationException>(()=>session.BeginOperation()).Code);running.Demand(Table,SecurityPermission.Read);
        session.UpdateValidatedIdentity(Identity(clock.GetUtcNow()));using(var fresh=session.BeginOperation()){fresh.Demand(Table,SecurityPermission.Read);}
        session.MarkMembershipUnavailable();Assert.Throws<AuthorizationException>(()=>session.BeginOperation());running.Demand(Table,SecurityPermission.Read);
        session.UpdateValidatedIdentity(Identity(clock.GetUtcNow()));clock.Advance(TimeSpan.FromHours(1));Assert.Equal("AuthenticationExpired",Assert.Throws<AuthorizationException>(()=>session.BeginOperation()).Code);
        Assert.Throws<AuthorizationException>(()=>session.UpdateValidatedIdentity(new(new(Authority,"another"),"same display",[],clock.GetUtcNow().AddHours(1),clock.GetUtcNow())));
    }
    [Fact]public void TransactionSurvivesSignInExpiryAndStopsAtItsDeadline(){
        var clock=new Clock();using var manager=new AuthorizationManager(Policy(1,new PermissionRule(Database,SecurityPermission.Read)),clock);var identity=new AuthenticatedIdentity(User,"name",[],clock.GetUtcNow().AddMinutes(1),clock.GetUtcNow());var session=manager.CreateSession(identity);using var transaction=session.BeginTransaction();
        clock.Advance(TimeSpan.FromMinutes(29));transaction.Demand(Table,SecurityPermission.Read);Assert.False(transaction.CancellationToken.IsCancellationRequested);Assert.Equal("AuthenticationExpired",Assert.Throws<AuthorizationException>(()=>session.BeginOperation()).Code);
        clock.Advance(TimeSpan.FromMinutes(1));Assert.True(transaction.CancellationToken.IsCancellationRequested);Assert.Throws<OperationCanceledException>(()=>transaction.Demand(Table,SecurityPermission.Read));
    }
    [Fact]public void DisabledAccountCancelsAllConnectionsAndReenableDoesNotReviveOldWork(){
        var clock=new Clock();using var manager=new AuthorizationManager(Policy(1,new PermissionRule(Database,SecurityPermission.Read)),clock);var first=manager.CreateSession(Identity(clock.GetUtcNow()));var second=manager.CreateSession(Identity(clock.GetUtcNow()));using var one=first.BeginOperation();using var two=second.BeginTransaction();
        manager.SetDisabled(User,true);Assert.True(one.CancellationToken.IsCancellationRequested);Assert.True(two.CancellationToken.IsCancellationRequested);Assert.Equal("AccountDisabled",Assert.Throws<AuthorizationException>(()=>first.BeginOperation()).Code);
        manager.SetDisabled(User,false);using var next=first.BeginOperation();next.Demand(Table,SecurityPermission.Read);Assert.True(one.CancellationToken.IsCancellationRequested);Assert.False(next.CancellationToken.IsCancellationRequested);
        using var another=new AuthorizationManager(Policy(1));Assert.True(next.BelongsTo(manager));Assert.False(next.BelongsTo(another));manager.Dispose();Assert.Throws<ObjectDisposedException>(()=>first.BeginOperation());
    }
    [Fact]public void DisposalCancelsEverySubjectEvenWhenACallbackThrows(){
        var clock=new Clock();var manager=new AuthorizationManager(Policy(1),clock);var first=manager.CreateSession(Identity(clock.GetUtcNow()));var second=manager.CreateSession(new(new(Authority,"second"),"second",[],clock.GetUtcNow().AddHours(1),clock.GetUtcNow()));
        using var one=first.BeginTransaction();using var two=second.BeginTransaction();using var callback=one.CancellationToken.Register(()=>throw new InvalidOperationException("callback failure"));
        Assert.Throws<AggregateException>(manager.Dispose);Assert.True(one.CancellationToken.IsCancellationRequested);Assert.True(two.CancellationToken.IsCancellationRequested);Assert.Throws<ObjectDisposedException>(()=>second.BeginOperation());manager.Dispose();
    }
    private sealed class Clock:TimeProvider{
        private DateTimeOffset now=new(2026,11,3,0,0,0,TimeSpan.Zero);
        private readonly List<Timer> timers=[];
        public override DateTimeOffset GetUtcNow()=>this.now;
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period){var timer=new Timer(this,callback,state);this.timers.Add(timer);timer.Change(dueTime,period);return timer;}
        public void Advance(TimeSpan duration){this.now+=duration;foreach(var timer in this.timers.ToArray()){timer.Fire();}}
        private sealed class Timer(Clock clock,TimerCallback callback,object? state):ITimer{
            private DateTimeOffset due=DateTimeOffset.MaxValue;
            private TimeSpan period;
            private bool disposed;
            public bool Change(TimeSpan dueTime,TimeSpan period){if(this.disposed){return false;}this.period=period;this.due=DateTimeOffset.MaxValue;if(dueTime!=Timeout.InfiniteTimeSpan){this.due=clock.now+dueTime;}return true;}
            public void Fire(){if(this.disposed||clock.now<this.due){return;}this.due=DateTimeOffset.MaxValue;if(this.period>TimeSpan.Zero){this.due=clock.now+this.period;}callback(state);}
            public void Dispose()=>this.disposed=true;
            public ValueTask DisposeAsync(){this.Dispose();return ValueTask.CompletedTask;}
        }
    }
}
