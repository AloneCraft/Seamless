using System.Collections.Immutable;

namespace Seamless.Security;

/// <summary>一つのDBのロール編集。管理権限を含むロールの編集には別途サーバー管理権限を要する。</summary>
public sealed class DatabaseAuthorizationChange{
    public Guid DatabaseId{get;}
    public long ExpectedVersion{get;}
    public ImmutableArray<SecurityRole> UpsertRoles{get;}
    public ImmutableArray<Guid> RemoveRoles{get;}
    public ImmutableArray<RoleBinding> Assign{get;}
    public ImmutableArray<RoleBinding> Unassign{get;}
    public DatabaseAuthorizationChange(Guid databaseId,long expectedVersion,IEnumerable<SecurityRole>? upsertRoles=null,IEnumerable<Guid>? removeRoles=null,IEnumerable<RoleBinding>? assign=null,IEnumerable<RoleBinding>? unassign=null){
        this.DatabaseId=databaseId;this.ExpectedVersion=expectedVersion;this.UpsertRoles=(upsertRoles??[]).ToImmutableArray();this.RemoveRoles=(removeRoles??[]).ToImmutableArray();this.Assign=(assign??[]).ToImmutableArray();this.Unassign=(unassign??[]).ToImmutableArray();
        ValidateEdits(this.UpsertRoles,this.RemoveRoles,this.Assign,this.Unassign);
    }
    internal static void ValidateEdits(ImmutableArray<SecurityRole> roles,ImmutableArray<Guid> remove,ImmutableArray<RoleBinding> assign,ImmutableArray<RoleBinding> unassign){
        if(roles.Length+remove.Length+assign.Length+unassign.Length>100000){throw new ArgumentException("The authorization change exceeds the supported limit.");}
        if(roles.Select(static role=>role.Id).Distinct().Count()!=roles.Length||remove.Distinct().Count()!=remove.Length||roles.Any(role=>remove.Contains(role.Id))||assign.Intersect(unassign).Any()){throw new ArgumentException("The authorization change contains duplicate or contradictory edits.");}
    }
}
