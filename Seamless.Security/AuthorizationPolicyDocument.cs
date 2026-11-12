using System.Collections.Immutable;

namespace Seamless.Security;

public sealed record SecurityRoleDocument(Guid Id,string Name,ImmutableArray<PermissionRule> Rules);
public sealed record AuthorizationPolicyDocument(int Format,long Version,ImmutableArray<SecurityResource> Resources,ImmutableArray<SecurityRoleDocument> Roles,ImmutableArray<RoleBinding> Bindings){
    public const int CurrentFormat=1;
    public static AuthorizationPolicyDocument Capture(AuthorizationPolicySnapshot policy){
        ArgumentNullException.ThrowIfNull(policy);
        return new(CurrentFormat,policy.Version,policy.Resources.OrderBy(static item=>item.Id).ToImmutableArray(),policy.Roles.OrderBy(static role=>role.Id).Select(static role=>new SecurityRoleDocument(role.Id,role.Name,role.Rules.OrderBy(static rule=>rule.ResourceId).ThenBy(static rule=>rule.Allow).ThenBy(static rule=>rule.Deny).ToImmutableArray())).ToImmutableArray(),policy.Bindings.OrderBy(static binding=>binding.Subject.AuthorityId).ThenBy(static binding=>binding.Subject.Value,StringComparer.Ordinal).ThenBy(static binding=>binding.Kind).ThenBy(static binding=>binding.RoleId).ToImmutableArray());
    }
    public AuthorizationPolicySnapshot Restore(){
        if(this.Format!=CurrentFormat){throw new InvalidDataException("Unsupported authorization policy document format.");}
        if(this.Resources.IsDefault||this.Roles.IsDefault||this.Bindings.IsDefault||this.Resources.Any(static item=>item is null)||this.Roles.Any(static role=>role is null||role.Rules.IsDefault||role.Rules.Any(static rule=>rule is null))||this.Bindings.Any(static binding=>binding is null)){throw new InvalidDataException("The authorization policy document is incomplete.");}
        if(this.Resources.Length>100000||this.Roles.Length>100000||this.Bindings.Length>100000||this.Roles.Sum(static role=>(long)role.Rules.Length)>1000000){throw new InvalidDataException("The authorization policy document exceeds its supported bounds.");}
        return new(this.Version,this.Resources,this.Roles.Select(static role=>new SecurityRole(role.Id,role.Name,role.Rules)),this.Bindings);
    }
}
