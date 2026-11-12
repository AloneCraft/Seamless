using System.Collections.Immutable;

namespace Seamless.Security;

public readonly record struct SecuritySubjectId(Guid AuthorityId,string Value){
    public void Validate(){if(this.AuthorityId==Guid.Empty||string.IsNullOrWhiteSpace(this.Value)||this.Value.Length>1024||this.Value.Any(char.IsControl)){throw new ArgumentException("An authority and opaque subject identifier are required.");}}
}

/// <summary>サーバーが本人確認した後の主体。表示名は認可の識別に使わない。</summary>
public sealed class AuthenticatedIdentity{
    public SecuritySubjectId Subject{get;}
    public string DisplayName{get;}
    public ImmutableArray<SecuritySubjectId> Groups{get;}
    public DateTimeOffset ExpiresAt{get;}
    public DateTimeOffset MembershipVerifiedAt{get;}
    public AuthenticatedIdentity(SecuritySubjectId subject,string displayName,IEnumerable<SecuritySubjectId> groups,DateTimeOffset expiresAt,DateTimeOffset membershipVerifiedAt){
        subject.Validate();ArgumentNullException.ThrowIfNull(displayName);ArgumentNullException.ThrowIfNull(groups);
        this.Subject=subject;this.DisplayName=displayName;this.Groups=groups.Distinct().ToImmutableArray();
        if(this.Groups.Length>4096){throw new ArgumentException("The verified group list is too large.");}
        foreach(var group in this.Groups){group.Validate();if(group.AuthorityId!=subject.AuthorityId){throw new ArgumentException("Groups must belong to the verified authority.");}}
        this.ExpiresAt=expiresAt;this.MembershipVerifiedAt=membershipVerifiedAt;
    }
}
