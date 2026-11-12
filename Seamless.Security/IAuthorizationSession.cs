namespace Seamless.Security;

/// <summary>本人確認済みの主体と、操作・取引の開始時に固定する権限。ポリシーの所有はホストに残す。</summary>
public interface IAuthorizationSession:IDisposable{
    SecuritySubjectId Subject{get;}
    AuthorizationLease BeginOperation(CancellationToken cancellationToken=default);
    AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default);
    void UpdateValidatedIdentity(AuthenticatedIdentity identity);
    void MarkMembershipUnavailable();
}
