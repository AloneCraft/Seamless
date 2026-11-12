using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Seamless.Security;

/// <summary>操作の開始と監査に使用する不変のポリシー版。</summary>
public sealed class AuthorizationPolicySnapshot{
    private readonly FrozenDictionary<Guid,SecurityResource> resources;
    private readonly FrozenDictionary<Guid,SecurityRole> roles;
    private readonly FrozenDictionary<(SecuritySubjectId,SecurityPrincipalKind),ImmutableArray<Guid>> bindings;
    private readonly FrozenDictionary<Guid,Guid[]> paths;
    public long Version{get;}
    public Guid ServerId{get;}
    internal IEnumerable<SecurityResource> Resources=>this.resources.Values;
    internal IEnumerable<SecurityRole> Roles=>this.roles.Values;
    internal IEnumerable<RoleBinding> Bindings=>this.bindings.SelectMany(static entry=>entry.Value.Select(id=>new RoleBinding(entry.Key.Item1,entry.Key.Item2,id)));
    public AuthorizationPolicySnapshot(long version,IEnumerable<SecurityResource> resources,IEnumerable<SecurityRole> roles,IEnumerable<RoleBinding> bindings){
        if(version<0){throw new ArgumentOutOfRangeException(nameof(version));}this.Version=version;
        this.resources=resources.ToFrozenDictionary(static item=>item.Id);this.roles=roles.ToFrozenDictionary(static item=>item.Id);
        if(this.resources.Count is 0 or >100000||this.roles.Count>100000){throw new ArgumentException("The authorization metadata exceeds its supported bounds.");}
        var roots=this.resources.Values.Where(static item=>item.Kind==SecurityResourceKind.Server).ToArray();if(roots.Length!=1){throw new ArgumentException("A single server resource is required.");}this.ServerId=roots[0].Id;
        var paths=new Dictionary<Guid,Guid[]>();
        foreach(var resource in this.resources.Values){
            if(resource.Id==Guid.Empty||string.IsNullOrWhiteSpace(resource.Name)||!Enum.IsDefined(resource.Kind)){throw new ArgumentException("A resource ID, kind and name are required.");}
            var path=new List<Guid>();var current=resource;
            while(true){
                if(path.Count>=4||path.Contains(current.Id)){throw new ArgumentException("Invalid resource ancestry.");}path.Add(current.Id);
                if(current.Kind==SecurityResourceKind.Server){if(current.ParentId is not null){throw new ArgumentException("The server cannot have a parent.");}break;}
                if(current.ParentId is not{} parentId||!this.resources.TryGetValue(parentId,out var parent)){throw new ArgumentException("A resource parent is missing.");}
                var valid=current.Kind switch{SecurityResourceKind.Database=>parent.Kind==SecurityResourceKind.Server,SecurityResourceKind.Schema=>parent.Kind==SecurityResourceKind.Database,_=>parent.Kind is SecurityResourceKind.Database or SecurityResourceKind.Schema};
                if(!valid){throw new ArgumentException("Invalid resource scope.");}current=parent;
            }
            paths.Add(resource.Id,path.ToArray());
        }
        this.paths=paths.ToFrozenDictionary();
        foreach(var role in this.roles.Values){foreach(var rule in role.Rules){
            if(!this.resources.TryGetValue(rule.ResourceId,out var resource)||((rule.Allow|rule.Deny)&~SecurityPermission.All)!=0){throw new ArgumentException("A permission rule has an unknown resource or operation.");}
            if(resource.Kind!=SecurityResourceKind.Server&&((rule.Allow|rule.Deny)&SecurityPermission.ManageServer)!=0){throw new ArgumentException("Server administration cannot be granted at a database scope.");}
        }}
        var assignments=bindings.ToArray();if(assignments.Length>100000){throw new ArgumentException("Too many role assignments.");}
        foreach(var binding in assignments){binding.Subject.Validate();if(!Enum.IsDefined(binding.Kind)||!this.roles.ContainsKey(binding.RoleId)){throw new ArgumentException("An assignment refers to an unknown role or principal kind.");}}
        this.bindings=assignments.GroupBy(static item=>(item.Subject,item.Kind)).ToFrozenDictionary(static group=>group.Key,static group=>group.Select(static item=>item.RoleId).Distinct().ToImmutableArray());
    }
    public AuthorizationSnapshot Resolve(AuthenticatedIdentity identity){
        var assigned=new HashSet<Guid>();
        void Assign(SecuritySubjectId subject,SecurityPrincipalKind kind){if(this.bindings.TryGetValue((subject,kind),out var roles)){assigned.UnionWith(roles);}}
        Assign(identity.Subject,SecurityPrincipalKind.User);foreach(var group in identity.Groups){Assign(group,SecurityPrincipalKind.Group);}
        var permissions=new Dictionary<Guid,(SecurityPermission Allow,SecurityPermission Deny)>();
        foreach(var id in assigned){foreach(var rule in this.roles[id].Rules){var existing=permissions.GetValueOrDefault(rule.ResourceId);permissions[rule.ResourceId]=(existing.Allow|rule.Allow,existing.Deny|rule.Deny);}}
        return new(this,identity.Subject,identity.MembershipVerifiedAt,assigned.ToImmutableArray(),permissions.ToFrozenDictionary());
    }
    internal ReadOnlySpan<Guid> Path(Guid resource){if(this.paths.TryGetValue(resource,out var path)){return path;}return[];}
    public SecurityResource? Resource(Guid id)=>this.resources.GetValueOrDefault(id);
    public bool IsWithin(Guid resource,Guid ancestor)=>this.Path(resource).Contains(ancestor);
}

public sealed class AuthorizationSnapshot{
    private readonly AuthorizationPolicySnapshot policy;
    private readonly FrozenDictionary<Guid,(SecurityPermission Allow,SecurityPermission Deny)> permissions;
    public long PolicyVersion=>this.policy.Version;
    public SecuritySubjectId Subject{get;}
    public DateTimeOffset MembershipVerifiedAt{get;}
    public ImmutableArray<Guid> Roles{get;}
    internal AuthorizationSnapshot(AuthorizationPolicySnapshot policy,SecuritySubjectId subject,DateTimeOffset verified,ImmutableArray<Guid> roles,FrozenDictionary<Guid,(SecurityPermission,SecurityPermission)> permissions){this.policy=policy;this.Subject=subject;this.MembershipVerifiedAt=verified;this.Roles=roles;this.permissions=permissions;}
    public SecurityResource? Resource(Guid id)=>this.policy.Resource(id);
    public PermissionDecision Evaluate(Guid resource,SecurityPermission required){
        var path=this.policy.Path(resource);if(path.IsEmpty||required==SecurityPermission.None||(required&~SecurityPermission.All)!=0){return new(false,"UnknownTargetOrOperation",SecurityPermission.None,SecurityPermission.None);}
        var allow=SecurityPermission.None;var deny=SecurityPermission.None;
        foreach(var id in path){if(this.permissions.TryGetValue(id,out var rule)){allow|=rule.Allow;deny|=rule.Deny;}}
        if((deny&required)!=0){return new(false,"ExplicitDeny",allow&~deny,deny);}
        if((allow&required)!=required){return new(false,"NotGranted",allow&~deny,deny);}
        return new(true,"Allowed",allow&~deny,deny);
    }
    public void Demand(Guid resource,SecurityPermission required){var result=this.Evaluate(resource,required);if(!result.Allowed){throw new AuthorizationException(result.Code,"The identity is not permitted to perform the operation on this resource.");}}
}
