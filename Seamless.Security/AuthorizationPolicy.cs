using System.Collections.Immutable;

namespace Seamless.Security;

[Flags]public enum SecurityPermission:ulong{
    None=0,Discover=1,Read=2,Insert=4,Update=8,Delete=16,Execute=32,ReadSource=64,Edit=128,Validate=256,Apply=512,ManagePermissions=1024,ManageServer=2048,
    AllDatabase=Discover|Read|Insert|Update|Delete|Execute|ReadSource|Edit|Validate|Apply|ManagePermissions,
    All=AllDatabase|ManageServer
}
public enum SecurityResourceKind{Server,Database,Schema,Table,View,ScalarFunction,TableFunction,Procedure,Query}
public enum SecurityPrincipalKind{User,Group}
public sealed record SecurityResource(Guid Id,Guid? ParentId,SecurityResourceKind Kind,string Name);
public sealed record PermissionRule(Guid ResourceId,SecurityPermission Allow,SecurityPermission Deny=SecurityPermission.None);
public sealed record RoleBinding(SecuritySubjectId Subject,SecurityPrincipalKind Kind,Guid RoleId);
public sealed class SecurityRole{
    public Guid Id{get;}
    public string Name{get;}
    public ImmutableArray<PermissionRule> Rules{get;}
    public SecurityRole(Guid id,string name,IEnumerable<PermissionRule> rules){if(id==Guid.Empty||string.IsNullOrWhiteSpace(name)){throw new ArgumentException("A role ID and name are required.");}this.Id=id;this.Name=name;this.Rules=rules.ToImmutableArray();}
}
public sealed record PermissionDecision(bool Allowed,string Code,SecurityPermission Granted,SecurityPermission Denied);
public sealed class AuthorizationException(string code,string message):InvalidOperationException(message){public string Code{get;}=code;}
