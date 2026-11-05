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
        if(this.UpsertRoles.Length+this.RemoveRoles.Length+this.Assign.Length+this.Unassign.Length>100000){throw new ArgumentException("The authorization change exceeds the supported limit.");}
        if(this.UpsertRoles.Select(static role=>role.Id).Distinct().Count()!=this.UpsertRoles.Length||this.RemoveRoles.Distinct().Count()!=this.RemoveRoles.Length||this.UpsertRoles.Any(role=>this.RemoveRoles.Contains(role.Id))||this.Assign.Intersect(this.Unassign).Any()){throw new ArgumentException("The authorization change contains duplicate or contradictory edits.");}
    }
}
