using Seamless.Security;
using Change=Seamless.Security.DatabaseAuthorizationChange;

namespace Management.Tests;

public sealed class DatabaseAuthorizationChange{
    private static readonly Guid Authority=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),Server=Guid.Parse("20000000-0000-0000-0000-000000000001"),Database=Guid.Parse("20000000-0000-0000-0000-000000000002"),Other=Guid.Parse("20000000-0000-0000-0000-000000000003"),Table=Guid.Parse("20000000-0000-0000-0000-000000000004"),Administrator=Guid.Parse("20000000-0000-0000-0000-000000000005"),Reader=Guid.Parse("20000000-0000-0000-0000-000000000006"),Outside=Guid.Parse("20000000-0000-0000-0000-000000000007");
    private static readonly SecuritySubjectId Root=new(Authority,"root"),Admin=new(Authority,"db-admin"),User=new(Authority,"user");
    private static AuthenticatedIdentity Identity(SecuritySubjectId subject)=>new(subject,subject.Value,[],DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow);
    private static AuthorizationManager Manager(){
        var root=new SecurityRole(Guid.NewGuid(),"server administrator",[new(Server,SecurityPermission.All)]);var admin=new SecurityRole(Administrator,"database administrator",[new(Database,SecurityPermission.ManagePermissions)]);var reader=new SecurityRole(Reader,"reader",[new(Database,SecurityPermission.Read)]);var outside=new SecurityRole(Outside,"other database",[new(Other,SecurityPermission.Read)]);
        return new(new(1,[new(Server,null,SecurityResourceKind.Server,"server"),new(Database,Server,SecurityResourceKind.Database,"db"),new(Other,Server,SecurityResourceKind.Database,"other"),new(Table,Database,SecurityResourceKind.Table,"items")],[root,admin,reader,outside],[new(Root,SecurityPrincipalKind.User,root.Id),new(Admin,SecurityPrincipalKind.User,admin.Id)]));
    }
    [Fact]public void DatabaseAdministratorCanGrantAndRemoveOrdinaryPermissions(){
        using var manager=Manager();var session=manager.CreateSession(Identity(Admin));using(var operation=session.BeginOperation()){manager.ApplyDatabaseChange(operation,new(Database,1,assign:[new(User,SecurityPrincipalKind.User,Reader)]));}
        var user=manager.CreateSession(Identity(User));using(var read=user.BeginOperation()){read.Demand(Table,SecurityPermission.Read);Assert.Equal(2,read.Audit.PolicyVersion);}
        using(var operation=session.BeginOperation()){manager.ApplyDatabaseChange(operation,new(Database,2,removeRoles:[Reader]));}using var next=user.BeginOperation();Assert.Throws<AuthorizationException>(()=>next.Demand(Table,SecurityPermission.Read));
    }
    [Fact]public void DatabaseAdministratorCannotRedelegateOrChangeAdministrativeRoles(){
        using var manager=Manager();using var operation=manager.CreateSession(Identity(Admin)).BeginOperation();
        foreach(var change in new[]{
            new Change(Database,1,assign:[new(User,SecurityPrincipalKind.User,Administrator)]),
            new Change(Database,1,unassign:[new(Admin,SecurityPrincipalKind.User,Administrator)]),
            new Change(Database,1,removeRoles:[Administrator]),
            new Change(Database,1,upsertRoles:[new(Administrator,"removed administration",[new(Database,SecurityPermission.Read)])]),
            new Change(Database,1,upsertRoles:[new(Reader,"escalated",[new(Database,SecurityPermission.ManagePermissions)])]),
            new Change(Database,1,upsertRoles:[new(Guid.NewGuid(),"new administrator",[new(Database,SecurityPermission.ManagePermissions)])])
        }){Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(operation,change));Assert.Equal(1,manager.Policy.Version);}
    }
    [Fact]public void OnlyServerAdministratorCanAppointAndRemoveDatabaseAdministrators(){
        using var manager=Manager();var root=manager.CreateSession(Identity(Root));using(var operation=root.BeginOperation()){manager.ApplyDatabaseChange(operation,new(Database,1,assign:[new(User,SecurityPrincipalKind.User,Administrator)]));}
        var user=manager.CreateSession(Identity(User));using var active=user.BeginOperation();active.Demand(Database,SecurityPermission.ManagePermissions);
        using(var operation=root.BeginOperation()){manager.ApplyDatabaseChange(operation,new(Database,2,unassign:[new(User,SecurityPrincipalKind.User,Administrator)]));}
        active.Demand(Database,SecurityPermission.ManagePermissions);using var next=user.BeginOperation();Assert.Throws<AuthorizationException>(()=>next.Demand(Database,SecurityPermission.ManagePermissions));
    }
    [Fact]public void RejectedOrFailedChangesDoNotPartiallyPublish(){
        using var manager=Manager();using var operation=manager.CreateSession(Identity(Admin)).BeginOperation();var target=new RoleBinding(User,SecurityPrincipalKind.User,Reader);
        Assert.Equal("OutsideDatabaseScope",Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(operation,new(Database,1,assign:[target,new(User,SecurityPrincipalKind.User,Outside)]))).Code);Assert.Equal(1,manager.Policy.Version);
        using(var user=manager.CreateSession(Identity(User)).BeginOperation()){Assert.Throws<AuthorizationException>(()=>user.Demand(Table,SecurityPermission.Read));}
        Assert.Throws<IOException>(()=>manager.ApplyDatabaseChange(operation,new(Database,1,assign:[target]),(_,_)=>throw new IOException("backup store unavailable")));Assert.Equal(1,manager.Policy.Version);
        using var other=Manager();using var foreign=other.CreateSession(Identity(Admin)).BeginOperation();Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(foreign,new(Database,1,assign:[target])));
        AuthorizationAudit? audit=null;manager.ApplyDatabaseChange(operation,new(Database,1,assign:[target]),(policy,evidence)=>{Assert.Equal(1,manager.Policy.Version);Assert.Equal(2,policy.Version);audit=evidence;});Assert.Equal(operation.Audit,audit);
        Assert.Equal("Conflict",Assert.Throws<AuthorizationException>(()=>manager.ApplyDatabaseChange(operation,new(Database,1,assign:[target]))).Code);
    }
}
