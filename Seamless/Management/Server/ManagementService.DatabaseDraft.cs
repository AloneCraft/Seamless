using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Seamless.Management.Server;

internal sealed record DeletedDatabase(DefinitionVersion Database,DefinitionVersion[] Queries,Dictionary<string,DatabaseGrant[]> Grants);

public sealed partial class ManagementService{
    // 稼働用カタログには混ぜず、作成者が保管した未公開DBだけを返す。
    public Page<DraftSummary> UnpublishedDatabases(UserAccount user,long cursor=0)=>this.store.SelectPage<Draft,DraftSummary>("draft",cursor,100,
        draft=>draft.DatabaseChange is{ExpectedName:null}&&draft.BaseVersion==0&&draft.Definition.Kind==DefinitionKind.Database
            &&ManagementStore.Normalize(draft.Owner)==ManagementStore.Normalize(user.Name)&&Can(user,draft.Definition.Database,ManagementPermission.Edit)
            &&this.store.Read<DefinitionVersion>("active",draft.Definition.Id) is null&&this.store.Read<string>("retiredDatabase",draft.Definition.Id) is null,
        draft=>new(new(draft.Definition.Id,draft.Definition.Database,draft.Definition.Kind,draft.Definition.EntryType,draft.Definition.EntryMember,draft.Definition.WritesData),draft.Revision,0,draft.SavedAt));

    private void CheckDatabaseDraft(UserAccount user,Definition definition,DatabaseDraftChange? change,DefinitionVersion? active){
        if(definition.Kind==DefinitionKind.Database&&active is null&&this.store.Read<string>("retiredDatabase",definition.Id) is not null){
            throw new ManagementException("Conflict","削除済みDBはバックアップから復旧してください。");
        }
        if(change is null){return;}
        if(definition.Kind!=DefinitionKind.Database||change.Delete&&change.NewName is not null||!change.Delete&&change.NameBeforeDeletion is not null){
            throw new ManagementException("InvalidRequest","DBの変更内容が不正です。");
        }
        if(change.ExpectedName is null){
            if(active is not null||change.Delete||change.NewName is not null){throw new ManagementException("Conflict","このDBはすでに公開されています。");}
            ValidateDatabaseName(definition.Database);
            if(this.store.FindDatabase(definition.Database) is not null){throw new ManagementException("Conflict","同じ名前のDBがあります。");}
            return;
        }
        if(active is null||active.Database!=change.ExpectedName||(!change.Delete&&change.NewName is null)){
            throw new ManagementException("Conflict","稼働中のDB名が変更されました。一覧を更新してください。");
        }
        Require(user,active.Database,ManagementPermission.Manage);
        if(change.NameBeforeDeletion is{} beforeDeletion){ValidateDatabaseName(beforeDeletion);}
        if(change.NewName is{} name){
            ValidateDatabaseName(name);
            if(name!=active.Database){this.CheckDatabaseNameAvailable(active,name);}
        }
    }

    private static void ValidateDatabaseName(string name){
        if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.Trim()!=name||name.Any(char.IsControl)||name=="*"||name.Equals(SystemDatabaseId,StringComparison.OrdinalIgnoreCase)){
            throw new ManagementException("Validation","DB名が不正または予約名です。");
        }
    }

    private ChangePreview PreviewDatabaseDeletion(Draft draft,DefinitionVersion active){
        var dependencies=this.runtime.Dependencies(draft.Definition);
        var diagnostics=active.Version==draft.BaseVersion?Array.Empty<DiagnosticInfo>():[new("Conflict","共有版が進んでいます。差分を確認し基準版を更新してください。")];
        var intent=JsonSerializer.Serialize(new{Kind="DeleteDatabaseV1",draft.Revision,active.Version,active.Database,active.ArtifactHash,Dependencies=dependencies.Order(StringComparer.Ordinal)},ManagementJson.Options);
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent))),draft.Definition.Id,draft.Revision,active.Version,
            [$"データベース「{active.Database}」とその公開済みの定義・行データをバックアップし、稼働一覧から削除します。"],
            dependencies,diagnostics.Length==0,diagnostics,RequiresBackup:true);
    }

    private OperationInfo DeleteDatabase(UserAccount user,Draft draft,DefinitionVersion baseline,ChangePreview preview,OperationInfo operation,CancellationToken cancellation)=>
        this.runtime.WithDatabaseIdentity(draft.Definition.Id,cancellation,()=>{
            DefinitionVersion[] queries;
            lock(this.store.Gate){
                CheckCurrent();
                queries=this.Records<DefinitionVersion>("active").Where(item=>item.Definition.Kind==DefinitionKind.Query&&item.Database==baseline.Database).OrderBy(item=>item.Definition.Id,StringComparer.Ordinal).ToArray();
            }
            var backup=Path.Combine(ManagementPlacement.DirectoryPath(Path.Combine(this.store.Root,"backups"),allowShortcut:true),operation.Id.ToString("N"));
            if(Directory.Exists(backup)||File.Exists(backup)){throw new IOException("バックアップ先は未使用の場所を指定してください。");}
            SourceWorkspace.CheckLinks(backup);Directory.CreateDirectory(backup);
            this.runtime.BackupGeneration(baseline,Path.Combine(backup,"database"),cancellation);
            for(var index=0;index<queries.Length;index++){this.runtime.BackupGeneration(queries[index],Path.Combine(backup,"query-"+index),cancellation);}
            this.PublicationCheckpoint?.Invoke("Prepared",operation.Id);
            lock(this.store.Gate){
                CheckCurrent();
                var grants=this.Records<UserAccount>("user").Where(account=>account.Grants.Any(grant=>grant.Database==baseline.Database))
                    .ToDictionary(account=>ManagementStore.Normalize(account.Name),account=>account.Grants.Where(grant=>grant.Database==baseline.Database).ToArray(),StringComparer.Ordinal);
                var deleted=new DeletedDatabase(baseline,queries,grants);
                var finished=operation with{State=OperationState.Succeeded};
                this.store.Write(()=>{
                    this.store.AddHistory(user.Name,baseline.Database,"DatabaseDeleted",baseline.Definition.Id,baseline.Version);
                    foreach(var accountName in grants.Keys){
                        var account=this.store.Read<UserAccount>("user",accountName)!;
                        this.store.Put("user",accountName,account with{Grants=account.Grants.Where(grant=>grant.Database!=baseline.Database).ToArray()});
                    }
                    foreach(var definition in queries.Prepend(baseline)){this.store.Put<DefinitionVersion?>("active",definition.Definition.Id,null);}
                    this.store.Put("retiredDatabase",baseline.Definition.Id,operation.Id.ToString("N"));
                    this.store.RetireDatabasePlacement(baseline);
                    this.store.Put<Draft?>("draft",DraftSubject(draft.Owner,draft.Definition.Id),null);
                    this.store.Put("deletedDatabase",operation.Id.ToString("N"),deleted);
                    this.store.Put("operation",operation.Id.ToString("N"),finished);
                });
                return finished;
            }

            void CheckCurrent(){
                cancellation.ThrowIfCancellationRequested();
                var actor=this.CurrentActor(user);
                var latest=this.GetDraft(actor,draft.Definition.Id,draft.Revision);
                var active=this.store.Read<DefinitionVersion>("active",draft.Definition.Id);
                Require(actor,baseline.Database,ManagementPermission.Apply|ManagementPermission.Manage);
                if(active is null||active.Version!=baseline.Version||active.ArtifactHash!=baseline.ArtifactHash||active.Database!=baseline.Database
                    ||latest.DatabaseChange!=draft.DatabaseChange||this.PreviewDatabaseDeletion(latest,active).Token!=preview.Token){
                    throw new ManagementException("Conflict","確認後にDBまたは依存定義が変更されました。");
                }
            }
        });
}
