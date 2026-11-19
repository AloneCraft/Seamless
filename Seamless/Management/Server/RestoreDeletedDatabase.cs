using System.Text.Json;

namespace Seamless.Management.Server;

internal static class RestoreDeletedDatabase{
    internal static DefinitionVersion Run(string root,Guid deletion,CancellationToken cancellation){
        var key=deletion.ToString("N");
        using var owner=Program.LockOffline(root);
        using var store=new ManagementStore(root,owner,leaveOwnerOpen:true);
        var archive=store.Read<DeletedDatabase>("deletedDatabase",key)??throw new ManagementException("NotFound","削除済みDBのバックアップ情報がありません。");
        var previous=archive.Database;
        var id=previous.Definition.Id;
        if(store.Read<string>("retiredDatabase",id)!=key||store.Read<DefinitionVersion>("active",id) is not null||store.FindDatabase(previous.Database) is not null
            ||archive.Queries.Any(query=>store.Read<DefinitionVersion>("active",query.Definition.Id) is not null)){
            throw new ManagementException("Conflict","同じDB名や定義が稼働中か、対象に別の削除操作があります。");
        }
        var backup=Path.Combine(ManagementPlacement.DirectoryPath(Path.Combine(store.Root,"backups"),allowShortcut:true),key);
        SourceWorkspace.CheckLinks(backup);
        using var runtime=new NativeRuntime(store,startAuditWorker:false);
        var restored=Recover(previous,Path.Combine(backup,"database"));
        var queries=archive.Queries.Select((query,index)=>Recover(query,Path.Combine(backup,"query-"+index))).ToArray();
        AuditOrigin? recoveredOrigin=null;
        using(var instance=new DatabaseInstance(restored.Definition,Path.Combine(store.Root,restored.StoragePath,"data"),runtime.ReadArtifact(restored))){
            instance.Open();
            using var read=instance.Database.BeginReadSession(cancellation);
            instance.Database.ValidateRelations();
            if(instance.AuditDestination is not null){
                var journal=instance.Database.MetadataJournal!;
                var origin=store.Read<AuditOrigin>("auditOrigin",journal.Device.WalId.ToString("N"));
                if(origin is null||origin.StoragePath!=previous.StoragePath||origin.ArtifactHash!=previous.ArtifactHash||origin.Options!=journal.Options){
                    throw new ManagementException("RecoveryRequired","削除前の監査転記元を確認できません。");
                }
                recoveredOrigin=origin with{StoragePath=restored.StoragePath,Drained=false,RecoveredSession=null};
            }
        }
        cancellation.ThrowIfCancellationRequested();
        store.Write(()=>{
            foreach(var definition in queries.Prepend(restored)){
                store.Put("version",definition.Definition.Id+":"+definition.Version,definition);
                store.Put("active",definition.Definition.Id,definition);
            }
            foreach(var (name,grants) in archive.Grants){
                if(store.Read<UserAccount>("user",name) is{} account&&!account.Grants.Any(grant=>grant.Database==previous.Database)){
                    store.Put("user",name,account with{Grants=[..account.Grants,..grants]});
                }
            }
            if(recoveredOrigin is not null){store.Put("auditOrigin",recoveredOrigin.WalId.ToString("N"),recoveredOrigin);}
            store.Put<string?>("retiredDatabase",id,null);
            store.AddHistory("offline-recovery",restored.Database,"DatabaseRestored",id,restored.Version);
        });
        return restored;

        DefinitionVersion Recover(DefinitionVersion version,string source){
            SourceWorkspace.CheckLinks(source);
            var metadata=Path.Combine(source,"definition-version.json");SourceWorkspace.CheckLinks(metadata);
            using(var file=File.OpenRead(metadata)){
                var saved=JsonSerializer.Deserialize<DefinitionVersion>(file,ManagementJson.Options);
                if(saved is null||saved.Definition.Hash()!=version.Definition.Hash()||saved.Version!=version.Version||
                    saved.ArtifactHash!=version.ArtifactHash||saved.StoragePath!=version.StoragePath||saved.Database!=version.Database){
                    throw new ManagementException("BuildMismatch","バックアップが削除前の公開版と一致しません。");
                }
            }
            var storage=Path.Combine("generations",Guid.NewGuid().ToString("N"));
            var generation=Path.Combine(store.Root,storage);
            NativeRuntime.CopyGenerationDirectory(source,generation,cancellation);
            var next=version with{Version=checked(version.Version+1),StoragePath=storage,CreatedAt=DateTimeOffset.UtcNow};
            if(version.Definition.Kind==DefinitionKind.Database){
                var manifestPath=Path.Combine(generation,"manifest.json");
                var manifest=JsonSerializer.Deserialize<ArtifactManifest>(File.ReadAllText(manifestPath),ManagementJson.Options)
                    ??throw new InvalidDataException("バックアップに公開世代の記録がありません。");
                SourceWorkspace.Atomic(manifestPath,JsonSerializer.Serialize(manifest with{PublishedVersion=next.Version},ManagementJson.Options));
                ManagementPlacement.SealData(generation);
            }
            using var artifact=runtime.ReadArtifact(next);
            return next;
        }
    }
}
