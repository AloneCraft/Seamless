namespace Seamless.Security;

public sealed partial class AuthorizationManager{
    /// <summary>DB内の一括編集を検査して公開する。永続化アダプターは確定後に戻るcommitを渡す。</summary>
    public AuthorizationPolicySnapshot ApplyDatabaseChange(AuthorizationLease operation,DatabaseAuthorizationChange change,Action<AuthorizationPolicySnapshot,AuthorizationAudit>? commit=null){
        ArgumentNullException.ThrowIfNull(operation);ArgumentNullException.ThrowIfNull(change);
        lock(this.gate){
            this.EnsureActive();
            if(this.policy.Resource(change.DatabaseId)?.Kind!=SecurityResourceKind.Database){throw new AuthorizationException("UnknownTargetOrOperation","A database scope is required.");}
            return this.ApplyRoleChange(operation,new(change.DatabaseId,change.ExpectedVersion,change.UpsertRoles,change.RemoveRoles,change.Assign,change.Unassign),commit);
        }
    }
    /// <summary>対象スコープの権限を検査し、既存資源のロールだけを更新する。</summary>
    public AuthorizationPolicySnapshot ApplyRoleChange(AuthorizationLease operation,AuthorizationRoleChange change,Action<AuthorizationPolicySnapshot,AuthorizationAudit>? commit=null){
        ArgumentNullException.ThrowIfNull(operation);ArgumentNullException.ThrowIfNull(change);
        lock(this.gate){
            this.EnsureActive();if(!operation.BelongsTo(this)){throw new AuthorizationException("InvalidAuthority","The operation belongs to another authorization manager.");}operation.ThrowIfCancellationRequested();
            var current=this.policy;if(change.ExpectedVersion!=current.Version){throw new AuthorizationException("Conflict","The authorization policy changed before this edit.");}
            var kind=current.Resource(change.ScopeId)?.Kind;
            if(kind is not(SecurityResourceKind.Database or SecurityResourceKind.Server)){throw new AuthorizationException("UnknownTargetOrOperation","A server or database scope is required.");}
            var server=kind==SecurityResourceKind.Server;
            if(server){operation.Demand(current.ServerId,SecurityPermission.ManageServer);}else{operation.Demand(change.ScopeId,SecurityPermission.ManagePermissions);}
            var roles=current.Roles.ToDictionary(static role=>role.Id);var assignments=current.Bindings.ToHashSet();
            void ValidateRole(SecurityRole role){
                if(!server&&(role.Rules.IsEmpty||role.Rules.Any(rule=>!current.IsWithin(rule.ResourceId,change.ScopeId)))){throw new AuthorizationException("OutsideDatabaseScope","Every edited role must belong entirely to this database.");}
                if(role.Rules.Any(static rule=>((rule.Allow|rule.Deny)&(SecurityPermission.ManagePermissions|SecurityPermission.ManageServer))!=0)){operation.Demand(current.ServerId,SecurityPermission.ManageServer);}
            }
            SecurityRole Existing(Guid id){if(!roles.TryGetValue(id,out var role)){throw new AuthorizationException("UnknownRole","The role is not registered.");}ValidateRole(role);return role;}
            foreach(var role in change.UpsertRoles){if(roles.ContainsKey(role.Id)){Existing(role.Id);}ValidateRole(role);roles[role.Id]=role;}
            foreach(var id in change.RemoveRoles){Existing(id);roles.Remove(id);assignments.RemoveWhere(binding=>binding.RoleId==id);}
            foreach(var binding in change.Unassign){Existing(binding.RoleId);assignments.Remove(binding);}
            foreach(var binding in change.Assign){Existing(binding.RoleId);assignments.Add(binding);}
            var next=new AuthorizationPolicySnapshot(checked(current.Version+1),current.Resources,roles.Values,assignments);
            operation.ThrowIfCancellationRequested();commit?.Invoke(next,operation.Audit);Volatile.Write(ref this.policy,next);return next;
        }
    }
}
