using System.Security.Cryptography;
using System.Text.Json;
using Seamless.Authentication;
using Seamless.Security;

namespace Seamless.Management.Server;

internal sealed record StoredAuthorizationRevision(Guid InstanceId,AuthorizationPolicyDocument Policy,string Hash,AuthorizationAudit AppliedBy,DateTimeOffset CommittedAt,Guid? DatabaseId);
internal sealed record AuthorizationRevisionInfo(long Version,string Hash,AuthorizationAudit AppliedBy,DateTimeOffset CommittedAt,Guid? DatabaseId);

/// <summary>system DBの確定後に共通認可を公開する。通信の本人確認は各ホストのアダプターで行う。</summary>
internal sealed partial class StoredAuthorization:IDisposable{
    private readonly ManagementStore store;
    private readonly AuthorizationManager manager;
    private readonly object gate=new();
    private StoredAuthorizationRevision current;
    private long cachedEpoch;
    private long reads;
    private bool synchronized=true;
    private bool disposed;
    internal Action<string>? Checkpoint{get;set;}
    internal long PolicyReadCount{get{lock(this.gate){return this.reads;}}}
    internal AuthorizationPolicySnapshot Policy{get{lock(this.gate){this.Synchronize();return this.manager.Policy;}}}
    private StoredAuthorization(ManagementStore store,TimeProvider? time,AuthorizationLimits? limits){
        this.store=store;lock(store.Gate){
            this.current=ReadCurrent(store);this.reads=1;this.cachedEpoch=store.AuthorizationEpoch;
            this.manager=new(Validate(store,this.current),time,limits);
            store.RegisterAuthorizationManager(this.manager);
        }
    }
    internal static StoredAuthorization Initialize(ManagementStore store,AuthorizationPolicySnapshot policy,AuthenticatedIdentity identity,OidcProviderSettings? provider=null){
        ArgumentNullException.ThrowIfNull(store);ArgumentNullException.ThrowIfNull(policy);ArgumentNullException.ThrowIfNull(identity);RequireAudit(store);
        if(policy.ServerId!=store.InstanceId||policy.Version!=1){throw new ManagementException("Validation","初期認可ポリシーは対象インスタンスの版1で指定してください。");}
        using var manager=new AuthorizationManager(policy);using var operation=manager.CreateSession(identity).BeginOperation();operation.Demand(policy.ServerId,SecurityPermission.ManageServer);
        if(provider is not null){provider.Validate();if(provider.Id!=identity.Subject.AuthorityId){throw new ArgumentException("初期登録の認証元が管理者の認証元と一致しません。");}}
        var record=Revision(store,policy,operation.Audit,null);_=Save(store,record,null,null,operation.ThrowIfCancellationRequested,provider);return Open(store);
    }
    internal static StoredAuthorization Open(ManagementStore store,TimeProvider? time=null,AuthorizationLimits? limits=null){ArgumentNullException.ThrowIfNull(store);RequireAudit(store);return new(store,time,limits);}
    private static void RequireAudit(ManagementStore store){if(!store.HasAudit){throw new ManagementException("MigrationRequired","認可変更の保存には監査テーブルが必要です。CLIで明示的に更新してください。");}}
    private static string VersionKey(long version)=>version.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Hash(AuthorizationPolicyDocument document)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document,ManagementJson.Options)));
    private static StoredAuthorizationRevision Revision(ManagementStore store,AuthorizationPolicySnapshot policy,AuthorizationAudit audit,Guid? database){var document=AuthorizationPolicyDocument.Capture(policy);return new(store.InstanceId,document,Hash(document),audit,DateTimeOffset.UtcNow,database);}
    private static AuthorizationPolicySnapshot Validate(ManagementStore store,StoredAuthorizationRevision record){
        if(record.InstanceId!=store.InstanceId||record.Policy is null||record.AppliedBy is null||record.AppliedBy.OperationId==Guid.Empty){throw new InvalidDataException("認可ポリシーの保存情報が不正です。");}
        var policy=record.Policy.Restore();record.AppliedBy.Subject.Validate();
        if(policy.ServerId!=store.InstanceId||policy.Version<1||record.AppliedBy.PolicyVersion<0||record.AppliedBy.PolicyVersion>policy.Version||record.Hash!=Hash(AuthorizationPolicyDocument.Capture(policy))||record.DatabaseId is{} database&&policy.Resource(database)?.Kind!=SecurityResourceKind.Database){throw new InvalidDataException("認可ポリシーと対象インスタンス・版・内容が一致しません。");}return policy;
    }
    private static StoredAuthorizationRevision ReadCurrent(ManagementStore store)=>store.Read<StoredAuthorizationRevision>("authorization","active")??throw new ManagementException("InitializationRequired","共通認可ポリシーは未設定です。起動時には自動作成しません。");
    private void Synchronize(){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.synchronized&&this.cachedEpoch==this.store.AuthorizationEpoch){return;}
        this.synchronized=false;lock(this.store.Gate){var record=ReadCurrent(this.store);this.reads++;var policy=Validate(this.store,record);var current=this.manager.Policy;
            if(policy.Version<current.Version||policy.Version==current.Version&&record.Hash!=this.current.Hash){throw new ManagementException("RecoveryRequired","保存された認可ポリシーの版が現在の状態と一致しません。明示的な復旧が必要です。");}
            if(policy.Version>current.Version){this.manager.PublishPolicy(policy);}this.current=record;this.cachedEpoch=this.store.AuthorizationEpoch;this.synchronized=true;
        }
    }
    private static long Save(ManagementStore store,StoredAuthorizationRevision record,StoredAuthorizationRevision? expected,Action<string>? checkpoint,Action beforeCommit,OidcProviderSettings? provider=null){
        _=Validate(store,record);checkpoint?.Invoke("Saving");long epoch;lock(store.Gate){if(store.WriteInProgress){throw new InvalidOperationException("認可の公開は、確定していない管理DBの書込へ入れ子にできません。");}store.Write(()=>{
            var active=store.Read<StoredAuthorizationRevision>("authorization","active");if(expected is null){if(active is not null){throw new ManagementException("Conflict","認可ポリシーは初期設定済みです。");}}
            else{if(active is null||active.Policy.Version!=expected.Policy.Version||active.Hash!=expected.Hash){throw new AuthorizationException("Conflict","The persisted authorization policy changed before this edit.");}_=Validate(store,active);}
            var key=VersionKey(record.Policy.Version);var operationKey=record.AppliedBy.OperationId.ToString("N");if(store.Read<StoredAuthorizationRevision>("authorizationVersion",key) is not null||store.Read<AuthorizationRevisionInfo>("authorizationOperation",operationKey) is not null){throw new ManagementException("Conflict","認可の版または操作IDは確定済みです。保存された結果を照会してください。");}
            var info=new AuthorizationRevisionInfo(record.Policy.Version,record.Hash,record.AppliedBy,record.CommittedAt,record.DatabaseId);store.Put("authorizationVersion",key,record);store.Put("authorizationChange",key,info);store.Put("authorizationOperation",operationKey,info);store.Put("authorization","active",record);
            if(provider is not null){store.Put("authorization","oidcProvider",provider);}
            var database="*";if(record.DatabaseId is{} id){database=record.Policy.Resources.Single(item=>item.Id==id).Name;}var code="PolicyInitialized";if(expected is not null){code="PolicyPublished";}
            store.AppendAuditInside(new(ManagementStore.Key("authorizationAudit",key),record.AppliedBy.OperationId,record.AppliedBy.StartedAt,record.CommittedAt,record.AppliedBy.Subject.AuthorityId,record.AppliedBy.Subject.Value,database,record.DatabaseId?.ToString("N"),AuditAction.PermissionChange,AuditOutcome.Succeeded,record.AppliedBy.PolicyVersion,null,null,code,"認可ポリシー v"+key+" を確定しました。"));checkpoint?.Invoke("Prepared");beforeCommit();
        });epoch=store.AuthorizationEpoch;}checkpoint?.Invoke("Committed");return epoch;
    }
    internal AuthorizationPolicySnapshot ApplyDatabaseChange(AuthorizationLease operation,DatabaseAuthorizationChange change,Guid? operationId=null){
        lock(this.gate){
            this.Synchronize();if(this.manager.Policy.Resource(change.DatabaseId)?.Kind!=SecurityResourceKind.Database){throw new AuthorizationException("UnknownTargetOrOperation","A database scope is required.");}
            return this.ApplyRoleChange(operation,new(change.DatabaseId,change.ExpectedVersion,change.UpsertRoles,change.RemoveRoles,change.Assign,change.Unassign),operationId);
        }
    }
    internal AuthorizationPolicySnapshot ApplyRoleChange(AuthorizationLease operation,AuthorizationRoleChange change,Guid? operationId=null){
        if(operationId==Guid.Empty){throw new ArgumentException("認可変更には空でない操作IDが必要です。",nameof(operationId));}
        lock(this.gate){this.Synchronize();try{
            var before=this.current;StoredAuthorizationRevision? saved=null;long epoch=0;var policy=this.manager.ApplyRoleChange(operation,change,(next,audit)=>{if(operationId is{} id){audit=audit with{OperationId=id};}Guid? database=change.ScopeId;if(database==next.ServerId){database=null;}saved=Revision(this.store,next,audit,database);epoch=Save(this.store,saved,before,this.Checkpoint,operation.ThrowIfCancellationRequested);});
            this.current=saved!;this.cachedEpoch=epoch;this.synchronized=true;this.Checkpoint?.Invoke("Published");return policy;
        }catch(Exception error){this.synchronized=false;try{this.Synchronize();}catch(Exception recovery){throw new AggregateException("認可変更の確定結果を確認できません。新しい操作には保存状態の再確認が必要です。",error,recovery);}throw;}}
    }
    internal T WithPolicy<T>(Func<T> action){lock(this.gate){this.Synchronize();return action();}}
    internal (AuthorizationPolicySnapshot Snapshot,AuthorizationPolicyDocument Document) CurrentView()=>this.WithPolicy(()=>(this.manager.Policy,this.current.Policy));
    internal StoredAuthorizationSession CreateSession(AuthenticatedIdentity identity)=>this.WithPolicy(()=>{
        lock(this.store.Gate){return new StoredAuthorizationSession(this,this.manager.CreateSession(identity,this.ReadDisabledSubject));}
    });
    internal StoredAuthorizationRevision ReadVersion(long version){lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);var record=this.store.Read<StoredAuthorizationRevision>("authorizationVersion",VersionKey(version))??throw new ManagementException("NotFound","指定した認可ポリシーの版がありません。");var policy=Validate(this.store,record);if(policy.Version!=version){throw new InvalidDataException("認可ポリシーの版が一致しません。");}return record;}}
    internal Page<AuthorizationRevisionInfo> History(long cursor=0)=>this.WithPolicy(()=>this.store.List<AuthorizationRevisionInfo>("authorizationChange",cursor));
    internal AuthorizationRevisionInfo? FindOperation(Guid id)=>this.WithPolicy(()=>this.store.Read<AuthorizationRevisionInfo>("authorizationOperation",id.ToString("N")));
    public void Dispose(){
        lock(this.gate){if(this.disposed){return;}this.disposed=true;this.store.UnregisterAuthorizationManager(this.manager);}
        this.manager.Dispose();
    }
}
internal sealed class StoredAuthorizationSession:IAuthorizationSession{
    private readonly StoredAuthorization host;
    private readonly AuthorizationSession session;
    internal StoredAuthorizationSession(StoredAuthorization host,AuthorizationSession session){this.host=host;this.session=session;}
    public SecuritySubjectId Subject=>this.session.Subject;
    public AuthorizationLease BeginOperation(CancellationToken cancellationToken=default)=>this.host.WithPolicy(()=>this.session.BeginOperation(cancellationToken));
    public AuthorizationLease BeginTransaction(CancellationToken cancellationToken=default)=>this.host.WithPolicy(()=>this.session.BeginTransaction(cancellationToken));
    public void UpdateValidatedIdentity(AuthenticatedIdentity identity)=>this.host.WithPolicy(()=>{this.session.UpdateValidatedIdentity(identity);return true;});
    public void MarkMembershipUnavailable()=>this.session.MarkMembershipUnavailable();
    public void Dispose()=>this.session.Dispose();
}
