using System.Collections.Immutable;

namespace Seamless.Security;

/// <summary>サーバーまたは一つのDBを対象とするロールと割り当ての一括変更。</summary>
public sealed class AuthorizationRoleChange{
    public Guid ScopeId{get;}
    public long ExpectedVersion{get;}
    public ImmutableArray<SecurityRole> UpsertRoles{get;}
    public ImmutableArray<Guid> RemoveRoles{get;}
    public ImmutableArray<RoleBinding> Assign{get;}
    public ImmutableArray<RoleBinding> Unassign{get;}
    public AuthorizationRoleChange(Guid scopeId,long expectedVersion,IEnumerable<SecurityRole>? upsertRoles=null,IEnumerable<Guid>? removeRoles=null,IEnumerable<RoleBinding>? assign=null,IEnumerable<RoleBinding>? unassign=null){
        this.ScopeId=scopeId;this.ExpectedVersion=expectedVersion;
        this.UpsertRoles=(upsertRoles??[]).ToImmutableArray();this.RemoveRoles=(removeRoles??[]).ToImmutableArray();this.Assign=(assign??[]).ToImmutableArray();this.Unassign=(unassign??[]).ToImmutableArray();
        DatabaseAuthorizationChange.ValidateEdits(this.UpsertRoles,this.RemoveRoles,this.Assign,this.Unassign);
    }
}
