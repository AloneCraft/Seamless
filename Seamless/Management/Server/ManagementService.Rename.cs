namespace Seamless.Management.Server;

internal sealed record DatabaseRenameReceipt(RenameDatabaseRequest Request,string Owner,CatalogDatabase Result);

public sealed partial class ManagementService{
    internal Action? RenameCheckpoint{get;set;}
    public CatalogDatabase RenameDatabase(UserAccount user,RenameDatabaseRequest request,CancellationToken cancellation=default){
        if(request is null||request.OperationId==Guid.Empty||request.DatabaseId is null||!request.DatabaseId.StartsWith("db:",StringComparison.Ordinal)||request.DatabaseId.Length<=3||string.IsNullOrWhiteSpace(request.ExpectedName)||string.IsNullOrWhiteSpace(request.Name)){
            throw new ManagementException("InvalidRequest","対象DB、現在名、新名、操作IDが必要です。");
        }
        request=request with{Name=request.Name.Trim()};
        if(request.Name.Length>128||request.Name.Any(char.IsControl)||request.Name=="*"||request.Name.Equals(SystemDatabaseId,StringComparison.OrdinalIgnoreCase)){
            throw new ManagementException("Validation","DB名が不正または予約名です。");
        }
        var id=request.DatabaseId[3..];
        if(this.Published(id).Definition.Kind!=DefinitionKind.Database){throw new ManagementException("InvalidRequest","ユーザーDBを指定してください。");}
        return this.runtime.WithDatabaseIdentity(id,cancellation,()=>{
            lock(this.store.Gate){
                cancellation.ThrowIfCancellationRequested();
                var actor=this.CurrentActor(user);
                var current=this.Published(id);
                if(current.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("InvalidRequest","ユーザーDBを指定してください。");}
                Require(actor,current.Database,ManagementPermission.Manage);
                var key=request.OperationId.ToString("N");
                if(this.store.Read<DatabaseRenameReceipt>("databaseRename",key) is{} previous){
                    if(previous.Request!=request||previous.Owner!=ManagementStore.Normalize(user.Name)){throw new ManagementException("Conflict","操作IDは別の名前変更に使用されています。");}
                    return previous.Result;
                }
                if(current.Database!=request.ExpectedName){throw new ManagementException("Conflict","DB名が別の操作で変更されています。一覧を更新してください。");}
                if(current.Database!=request.Name){
                    this.CheckDatabaseNameAvailable(current,request.Name);
                    if(this.Records<OperationInfo>("operation").Any(operation=>operation.Database==current.Database&&operation.State==OperationState.Running)){
                        throw new ManagementException("Busy","DBの公開処理が実行中です。完了後に名前を変更してください。");
                    }
                }
                var result=new CatalogDatabase(request.DatabaseId,request.Name,id,current.Version,current.ArtifactHash,false);
                this.store.Write(()=>{
                    if(current.Database!=request.Name){
                        this.WriteDatabaseName(current,request.Name,cancellation);
                        this.RenameCheckpoint?.Invoke();
                    }
                    cancellation.ThrowIfCancellationRequested();
                    this.store.Put("databaseRename",key,new DatabaseRenameReceipt(request,ManagementStore.Normalize(user.Name),result));
                    this.store.AddHistory(user.Name,request.Name,"DatabaseRenamed",id,current.Version);
                });
                return result;
            }
        });
    }
    private void CheckDatabaseNameAvailable(DefinitionVersion current,string name){
        var id=current.Definition.Id;
        if(this.Records<DefinitionVersion>("active").Any(version=>version.Definition.Kind==DefinitionKind.Database&&version.Database==name&&version.Definition.Id!=id)){
            throw new ManagementException("Conflict","同じ名前のDBがあります。");
        }
        if(this.Records<UserAccount>("user").Any(account=>account.Grants.Any(grant=>grant.Database==name))){
            throw new ManagementException("Conflict","新しい名前には既存の権限設定があります。権限設定を確認してください。");
        }
    }

    // 呼び出し元の管理トランザクション内でカタログと権限を同じ名前へ切り替える。
    private void WriteDatabaseName(DefinitionVersion current,string name,CancellationToken cancellation){
        var id=current.Definition.Id;
        var cursor=0L;
        do{
            var history=this.store.History(cursor,100,entry=>entry.Database==current.Database&&entry.DatabaseDefinitionId is null,cancellation);
            foreach(var entry in history.Items){this.store.Put("history",entry.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),entry with{DatabaseDefinitionId=id});}
            if(!history.HasMore){break;}cursor=history.NextCursor;
        }while(true);
        foreach(var version in this.Records<DefinitionVersion>("active").Where(version=>version.Database==current.Database)){
            this.store.Put("active",version.Definition.Id,version with{Database=name});
        }
        foreach(var account in this.Records<UserAccount>("user").Where(account=>account.Grants.Any(grant=>grant.Database==current.Database))){
            var grants=account.Grants.Select(grant=>{if(grant.Database==current.Database){return grant with{Database=name};}return grant;}).ToArray();
            this.store.Put("user",ManagementStore.Normalize(account.Name),account with{Grants=grants});
        }
        foreach(var draft in this.Records<Draft>("draft").Where(draft=>draft.Definition.Kind==DefinitionKind.Query&&draft.Definition.Database==current.Database&&this.store.Read<DefinitionVersion>("active",draft.Definition.Id) is null)){
            var updated=draft with{Definition=draft.Definition with{Database=name},Revision=checked(draft.Revision+1),SavedAt=DateTimeOffset.UtcNow};
            this.store.Put("draft",DraftSubject(draft.Owner,draft.Definition.Id),updated);
        }
        foreach(var validation in this.Records<ValidationDatabase>("validation").Where(validation=>validation.Database==current.Database)){
            this.store.Put("validation",validation.Id,validation with{Database=name});
        }
    }
    private IEnumerable<T> Records<T>(string kind){
        var cursor=0L;
        do{
            var page=this.store.List<T>(kind,cursor);foreach(var value in page.Items){yield return value;}
            if(!page.HasMore){yield break;}cursor=page.NextCursor;
        }while(true);
    }
}
