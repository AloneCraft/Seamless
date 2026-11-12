using Seamless.Security;

namespace Seamless.Management;

public sealed record DatabaseAuthorizationEdit(Guid OperationId,Guid DatabaseId,long ExpectedVersion){
    public SecurityRoleDocument[] UpsertRoles{get;init;}=[];
    public Guid[] RemoveRoles{get;init;}=[];
    public RoleBinding[] Assign{get;init;}=[];
    public RoleBinding[] Unassign{get;init;}=[];
    public DatabaseAuthorizationEdit Freeze(){
        if(this.OperationId==Guid.Empty||this.UpsertRoles is null||this.RemoveRoles is null||this.Assign is null||this.Unassign is null){throw new ManagementException("Validation","認可の変更要求が不正です。");}
        return this with{UpsertRoles=this.UpsertRoles.ToArray(),RemoveRoles=this.RemoveRoles.ToArray(),Assign=this.Assign.ToArray(),Unassign=this.Unassign.ToArray()};
    }
}
public sealed record DatabaseAuthorizationQuery(Guid DatabaseId,long Version=0,long Cursor=0);
public sealed record DatabaseAuthorizationListQuery(long Version=0,long Cursor=0);
public sealed record DatabaseAuthorizationList(long Version,SecurityResource[] Items,long NextCursor,bool HasMore);
public sealed record AuthorizationRoleHeader(Guid Id,string Name);
public sealed record AuthorizationRoleRule(Guid RoleId,PermissionRule Rule);
public sealed record AuthorizationMetadataEntry(SecurityResource? Resource=null,AuthorizationRoleHeader? Role=null,AuthorizationRoleRule? Rule=null,RoleBinding? Binding=null);
public sealed record DatabaseAuthorizationInfo(long Version,Guid DatabaseId,AuthorizationMetadataEntry[] Items,long NextCursor,bool HasMore);
public sealed record AuthorizationScopeQuery(Guid ScopeId,long Version=0,long Cursor=0);
public sealed record AuthorizationScopeInfo(long Version,Guid ScopeId,AuthorizationMetadataEntry[] Items,long NextCursor,bool HasMore);
public sealed record AuthorizationScopeEdit(Guid OperationId,Guid ScopeId,long ExpectedVersion){
    public SecurityRoleDocument[] UpsertRoles{get;init;}=[];
    public Guid[] RemoveRoles{get;init;}=[];
    public RoleBinding[] Assign{get;init;}=[];
    public RoleBinding[] Unassign{get;init;}=[];
    public AuthorizationScopeEdit Freeze(){
        var edit=this.DatabaseEdit().Freeze();
        return this with{UpsertRoles=edit.UpsertRoles,RemoveRoles=edit.RemoveRoles,Assign=edit.Assign,Unassign=edit.Unassign};
    }
    public DatabaseAuthorizationEdit DatabaseEdit()=>new(this.OperationId,this.ScopeId,this.ExpectedVersion){UpsertRoles=this.UpsertRoles,RemoveRoles=this.RemoveRoles,Assign=this.Assign,Unassign=this.Unassign};
}
public sealed record AuthorizationChangeResult(Guid OperationId,long Version,string Hash,DateTimeOffset CommittedAt,Guid? DatabaseId);
public sealed record AuthorizationSubjectChange(Guid OperationId,SecuritySubjectId Subject,bool Disabled){
    public Guid? ExpectedInstanceId{get;init;}
}
public sealed record AuthorizationSubjectChangeResult(Guid OperationId,SecuritySubjectId Subject,bool Disabled,DateTimeOffset CommittedAt);
