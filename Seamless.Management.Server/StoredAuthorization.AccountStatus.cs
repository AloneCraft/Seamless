using Seamless.Security;
using System.Runtime.ExceptionServices;

namespace Seamless.Management.Server;

internal sealed record StoredSubjectStatus(Guid OperationId,SecuritySubjectId Subject,bool Disabled,AuthorizationAudit AppliedBy,DateTimeOffset CommittedAt);

internal sealed partial class StoredAuthorization{
    private const string StatusKind="authorizationSubjectStatus";
    private const string OperationKind="authorizationSubjectOperation";
    private static string SubjectKey(SecuritySubjectId subject)=>subject.AuthorityId.ToString("N")+":"+subject.Value;

    private bool ReadDisabledSubject(SecuritySubjectId subject){
        var status=this.store.Read<StoredSubjectStatus>(StatusKind,SubjectKey(subject));
        if(status is null){return false;}
        if(status.Subject!=subject||status.OperationId==Guid.Empty){throw new InvalidDataException("保存されたアカウント状態が対象と一致しません。");}
        return status.Disabled;
    }

    internal async Task<StoredSubjectStatus> ApplySubjectStatusAsync(AuthorizationLease operation,SecuritySubjectId subject,bool disabled,Guid operationId){
        subject.Validate();
        if(operationId==Guid.Empty){throw new ArgumentException("アカウント状態の変更には空でない操作IDが必要です。",nameof(operationId));}
        StoredSubjectStatus result;
        var notified=Task.CompletedTask;
        Exception? failure=null;
        lock(this.gate){
            this.Synchronize();
            if(!operation.BelongsTo(this.manager)){throw new AuthorizationException("NotGranted","The operation does not belong to this authorization host.");}
            operation.Demand(this.manager.Policy.ServerId,SecurityPermission.ManageServer);
            if(disabled&&operation.Audit.Subject==subject){throw new ManagementException("Validation","サインイン中の管理者自身を無効化できません。");}
            lock(this.store.Gate){
                if(this.store.WriteInProgress){throw new InvalidOperationException("アカウント状態の公開を管理DBの書込へ入れ子にできません。");}
                var operationKey=operationId.ToString("N");
                var previous=this.store.Read<StoredSubjectStatus>(OperationKind,operationKey);
                if(previous is not null){
                    if(previous.Subject!=subject||previous.Disabled!=disabled||previous.AppliedBy.Subject!=operation.Audit.Subject){
                        throw new ManagementException("Conflict","操作IDは別のアカウント変更で使用済みです。");
                    }
                    return previous;
                }
                result=new(operationId,subject,disabled,operation.Audit with{OperationId=operationId},DateTimeOffset.UtcNow);
                var subjectKey=SubjectKey(subject);
                try{
                    this.store.Write(()=>{
                        this.store.Put(StatusKind,subjectKey,result);
                        this.store.Put(OperationKind,operationKey,result);
                        var code="SubjectEnabled";
                        if(disabled){code="SubjectDisabled";}
                        this.store.AppendAuditInside(new(ManagementStore.Key("authorizationSubjectAudit",operationKey),operationId,result.AppliedBy.StartedAt,result.CommittedAt,
                            result.AppliedBy.Subject.AuthorityId,result.AppliedBy.Subject.Value,"*",ManagementStore.Key(StatusKind,subjectKey).ToString("N"),
                            AuditAction.AccountChange,AuditOutcome.Succeeded,result.AppliedBy.PolicyVersion,null,null,code,subjectKey));
                        this.Checkpoint?.Invoke("SubjectPrepared");
                        operation.ThrowIfCancellationRequested();
                    });
                }catch(Exception error){
                    failure=error;
                    // 確定後の例外でも、保存済みなら稼働中の主体へ確定状態を反映する。
                    try{
                        if(this.store.Read<StoredSubjectStatus>(OperationKind,operationKey)==result){
                            notified=this.store.PublishSubjectStatusAsync(subject,disabled);
                        }
                    }catch(Exception recovery){
                        notified=this.store.PublishSubjectStatusAsync(subject,true);
                        failure=new AggregateException("アカウント状態の確定結果を確認できません。",error,recovery);
                    }
                }
                if(failure is null){notified=this.store.PublishSubjectStatusAsync(subject,disabled);}
            }
        }
        try{await notified.ConfigureAwait(false);}
        catch(Exception notification)when(failure is not null){
            throw new AggregateException("アカウント状態の変更と取消の通知に失敗しました。",failure,notification);
        }
        if(failure is not null){ExceptionDispatchInfo.Throw(failure);}
        this.Checkpoint?.Invoke("SubjectCommitted");
        return result;
    }

    internal StoredSubjectStatus? FindSubjectStatusOperation(Guid operationId)=>this.WithPolicy(()=>this.store.Read<StoredSubjectStatus>(OperationKind,operationId.ToString("N")));
}

public sealed partial class ManagementStore{
    private readonly List<AuthorizationManager> authorizationManagers=[];
    internal void RegisterAuthorizationManager(AuthorizationManager manager){lock(this.gate){this.authorizationManagers.Add(manager);}}
    internal void UnregisterAuthorizationManager(AuthorizationManager manager){lock(this.gate){this.authorizationManagers.Remove(manager);}}
    // Store.Gateで状態変更と新規セッションを直列化し、取消のコールバックはロックを保持せずに待つ。
    internal Task PublishSubjectStatusAsync(SecuritySubjectId subject,bool disabled){
        return Task.WhenAll(this.authorizationManagers.Select(manager=>manager.SetKnownSubjectDisabledAsync(subject,disabled)));
    }
}
