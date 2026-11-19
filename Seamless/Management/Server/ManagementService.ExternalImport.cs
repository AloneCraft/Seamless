using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    /// <summary>
    /// 外部DBの取り込みの適用。<paramref name="candidate"/> の DB 定義を公開し、新しい世代の中身は古い世代から写さずに <paramref name="fill"/> で入れる。
    /// <paramref name="replace"/> のときは同じ DB の既存の定義を丸ごと置き換え、古い世代をバックアップに残す(外部DBの取り込みの決定7)。
    /// 失敗したら公開しない(古い世代はそのまま動く)。下書きも、公開した定義と同じものに置き直す。
    /// </summary>
    internal OperationInfo ApplyExternalImport(UserAccount user,Definition candidate,Guid operationId,bool replace,Action<DatabaseInstance,CancellationToken> fill,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(fill);
        candidate=candidate.Freeze();ValidateDefinition(candidate);
        if(candidate.Kind!=DefinitionKind.Database){throw new ManagementException("Validation","取り込みはDB定義だけです。");}
        var database=this.store.CurrentDatabase(candidate);
        Require(user,database,ManagementPermission.Edit);Require(user,database,ManagementPermission.Apply);
        var intent=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("ExternalImportV1\n"+candidate.Hash()+"\n"+replace)));
        DefinitionVersion? active;
        lock(this.store.Gate){
            if(this.store.Read<OperationInfo>("operation",operationId.ToString("N")) is not null){throw new ManagementException("Conflict","操作IDは別の要求に使用されています。");}
            active=this.store.Read<DefinitionVersion>("active",candidate.Id);
            if(replace&&active is null){throw new ManagementException("NotFound","置き換えるDBがありません。");}
            if(!replace&&active is not null){throw new ManagementException("Conflict","同じ名前のDBがあります。置き換えを選んでください。");}
            if(this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&item.Database==database&&item.Definition.Id!=candidate.Id).Items.Length>0){throw new ManagementException("Conflict","このDBには既に別のDB定義があります。");}
            if(active is not null&&(active.Definition.Database!=candidate.Database||active.Definition.Kind!=candidate.Kind)){throw new ManagementException("Conflict","既存の定義のDB・種類は変更できません。");}
            // 個人の下書きに公開していない編集が残っていれば、取り込みで黙って上書きしない。
            var draft=this.store.Read<Draft>("draft",DraftSubject(user.Name,candidate.Id));
            if(draft is not null&&active is not null&&draft.Definition.Hash()!=active.Definition.Hash()){throw new ManagementException("Conflict","このDBに未適用の下書きがあります。先に適用するか破棄してから取り込んでください。");}
        }
        string? backup=null;if(active is not null){backup=Path.Combine(this.store.Root,"backups",operationId.ToString("N"));}
        var operation=new OperationInfo(operationId,user.Name,database,candidate.Id,OperationState.Running,null,null,DateTimeOffset.UtcNow,intent,backup);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if(!this.running.TryAdd(operationId,cancellation)){throw new ManagementException("Conflict","同じ操作が実行中です。");}
        try{
            this.store.Write(()=>this.store.Put("operation",operationId.ToString("N"),operation));
            this.runtime.Publish(candidate,cancellation.Token,publication=>{
                cancellation.Token.ThrowIfCancellationRequested();
                lock(this.store.Gate){
                    var current=this.store.Read<DefinitionVersion>("active",candidate.Id);
                    if((current?.Version??0)!=(active?.Version??0)){throw new ManagementException("Conflict","取り込みの間にDBが更新されました。");}
                    foreach(var dependent in publication.Dependents){if(this.store.Read<DefinitionVersion>("active",dependent.Definition.Id)?.Version!=dependent.ExpectedVersion){throw new ManagementException("Conflict","依存する問い合わせが更新されました。");}}
                    var version=new DefinitionVersion(candidate,checked((active?.Version??0)+1),user.Name,DateTimeOffset.UtcNow,publication.Primary.ArtifactHash,publication.Primary.StoragePath){Database=publication.Primary.Database};
                    operation=operation with{State=OperationState.Succeeded,PublishedVersion=version.Version};
                    var previousDraft=this.store.Read<Draft>("draft",DraftSubject(user.Name,candidate.Id));
                    this.store.WritePublication(publication.Primary,()=>{
                        if(publication.Primary.AuditOrigin is{} origin){this.store.Put("auditOrigin",origin.WalId.ToString("N"),origin);}
                        foreach(var dependent in publication.Dependents){var rebuilt=new DefinitionVersion(dependent.Definition,checked(dependent.ExpectedVersion+1),user.Name,DateTimeOffset.UtcNow,dependent.ArtifactHash,dependent.StoragePath){Database=dependent.Database};this.store.Put("version",rebuilt.Definition.Id+":"+rebuilt.Version,rebuilt);this.store.Put("active",rebuilt.Definition.Id,rebuilt);this.store.AddHistory(user.Name,rebuilt.Database,"Rebuilt",rebuilt.Definition.Id,rebuilt.Version);}
                        this.store.Put("version",candidate.Id+":"+version.Version,version);this.store.Put("active",candidate.Id,version);this.store.Put("operation",operationId.ToString("N"),operation);
                        this.store.Put("draft",DraftSubject(user.Name,candidate.Id),new Draft(candidate,user.Name,checked((previousDraft?.Revision??0)+1),version.Version,version.CreatedAt));
                        this.store.AddHistory(user.Name,database,"Imported",candidate.Id,version.Version);
                    });
                }
            },backup,fill:fill);
        }catch(Exception error){
            var durable=this.store.Read<OperationInfo>("operation",operationId.ToString("N"));
            if(durable?.State==OperationState.Succeeded){operation=durable;}
            else{var state=OperationState.Failed;if(error is OperationCanceledException){state=OperationState.Cancelled;}operation=operation with{State=state,Error=error.GetBaseException().Message};this.store.Write(()=>this.store.Put("operation",operationId.ToString("N"),operation));}
        }finally{this.running.TryRemove(operationId,out _);}
        return operation;
    }

    /// <summary>公開中の DB 定義(DB 名で探す)。無ければ null。取り込みの置き換えで、既存の定義の ID を引き継ぐのに使う。</summary>
    internal DefinitionVersion? FindDatabaseDefinition(UserAccount user,string database){
        Require(user,database,ManagementPermission.View);
        return this.store.List<DefinitionVersion>("active",predicate:item=>item.Definition.Kind==DefinitionKind.Database&&string.Equals(item.Database,database,StringComparison.OrdinalIgnoreCase)).Items.FirstOrDefault();
    }
}
