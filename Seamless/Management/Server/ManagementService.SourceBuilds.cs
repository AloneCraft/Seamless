namespace Seamless.Management.Server;

internal sealed record ConfirmedQueryBuild(string Id,long Version,SourceProjectBuild Build,QueryAuditMetadata? Audit);
internal sealed record ConfirmedDatabaseBuild(SourceProjectBuild Build,ConfirmedQueryBuild[] Queries);

public sealed partial class ManagementStore{
    internal object SourceBuildFileGate{get;}=new();
    private SourceBuildArtifactStore? sourceBuilds;
    internal SourceBuildArtifactStore SourceBuilds{get{lock(this.SourceBuildFileGate){return this.sourceBuilds??=new(this.Root,this.SourceBuildFileGate);}}}
}

public sealed partial class ManagementService{
    internal SourceBuildArtifactStore SourceBuilds=>this.store.SourceBuilds;
    private static void RequireTableSourcePreview(string? bundleHash){
        if(bundleHash is not null){throw new ManagementException("Unsupported","選択済み成果物はDB全体へ適用してください。");}
    }
    private CompiledSource ReadDraftBuild(UserAccount user,Draft draft,DraftKey key){
        if(key.BundleHash is null){return this.runtime.Compile(draft.Definition);}
        SourceProjectBuild build;
        lock(this.store.Gate){
            var imported=this.GetImportedSourceBuild(user,key);
            var active=this.store.Read<DefinitionVersion>("active",draft.Definition.Id);
            if(imported.Build.BundleHash!=key.BundleHash||(active?.Version??0)!=draft.BaseVersion){
                throw new ManagementException("Conflict","選択した成果物または基準公開版が変更されています。");
            }
            build=imported.Build;
        }
        return NativeRuntime.FromImported(this.SourceBuilds.Read(build));
    }

    private void BindConfirmedDatabaseBuild(UserAccount user,Draft draft,ConfirmedDatabaseBuild build){
        lock(this.store.Gate){
            var latest=this.GetDraft(user,draft.Definition.Id,draft.Revision);
            if(latest.Definition.Hash()!=draft.Definition.Hash()||latest.DatabaseChange!=draft.DatabaseChange||
                (this.store.Read<DefinitionVersion>("active",draft.Definition.Id)?.Version??0)!=draft.BaseVersion){
                throw new ManagementException("Conflict","ビルド中に下書きまたは公開版が変更されました。");
            }
            this.store.Write(()=>this.store.Put("confirmedDatabaseBuild",DraftSubject(user.Name,draft.Definition.Id),build));
        }
    }

    private ConfirmedDatabaseBuild? FindConfirmedDatabaseBuild(UserAccount user,Draft draft,Definition candidate){
        var confirmed=this.store.Read<ConfirmedDatabaseBuild>("confirmedDatabaseBuild",DraftSubject(user.Name,draft.Definition.Id));
        if(confirmed?.Build is not{} build||build.DraftRevision!=draft.Revision||build.BaseVersion!=draft.BaseVersion||
            build.DefinitionHash!=candidate.Hash()||build.SourceHash!=candidate.Source.Hash()){return null;}
        return confirmed;
    }

    internal ImportedSourceBuild BindImportedSourceBuild(UserAccount user,SourceProjectBuild build){
        if(build.BundleHash!=SourceBuildArtifactStore.BundleHash(build)){throw new ManagementException("BuildMismatch","成果物の識別情報が一致しません。");}
        lock(this.store.Gate){
            var draft=this.GetDraft(user,build.DefinitionId,build.DraftRevision);
            Require(user,this.store.CurrentDatabase(draft.Definition),ManagementPermission.Edit|ManagementPermission.Validate);
            var active=this.store.Read<DefinitionVersion>("active",build.DefinitionId);
            if(draft.Definition.Kind!=DefinitionKind.Database||draft.Definition.Hash()!=build.DefinitionHash||draft.Definition.Source.Hash()!=build.SourceHash||
                draft.BaseVersion!=build.BaseVersion||(active?.Version??0)!=build.BaseVersion){
                throw new ManagementException("Conflict","成果物の保管中に下書きまたは公開版が変更されています。");
            }
            var key=DraftSubject(user.Name,build.DefinitionId);
            var previous=this.store.Read<ImportedSourceBuild>("sourceBuild",key);
            if(previous is not null&&previous.Build.DraftRevision==build.DraftRevision&&previous.Build.DefinitionHash==build.DefinitionHash&&previous.Build.BundleHash==build.BundleHash){return previous;}
            var imported=new ImportedSourceBuild(build,DateTimeOffset.UtcNow);
            this.store.Write(()=>{
                this.store.Put("sourceBuild",key,imported);
                this.store.AddHistory(user.Name,this.store.CurrentDatabase(draft.Definition),"SourceBuildImported",build.DefinitionId,draft.Revision);
            });
            return imported;
        }
    }
    internal ImportedSourceBuild GetImportedSourceBuild(UserAccount user,DraftKey key){
        lock(this.store.Gate){
            var draft=this.GetDraft(user,key.Id,key.Revision);
            return this.FindImportedSourceBuild(user,draft)??throw new ManagementException("NotFound","この下書きに対応する取り込み済み成果物がありません。");
        }
    }
    internal ImportedSourceBuild? FindImportedSourceBuild(UserAccount user,DraftKey key){
        lock(this.store.Gate){
            var draft=this.FindDraft(user,key.Id);
            if(draft is null){return null;}
            if(draft.Revision!=key.Revision){throw new ManagementException("Conflict","下書きの版が変わっています。");}
            return this.FindImportedSourceBuild(user,draft);
        }
    }
    private ImportedSourceBuild? FindImportedSourceBuild(UserAccount user,Draft draft){
        var imported=this.store.Read<ImportedSourceBuild>("sourceBuild",DraftSubject(user.Name,draft.Definition.Id));
        if(imported is null||imported.Build.DraftRevision!=draft.Revision||imported.Build.BaseVersion!=draft.BaseVersion||imported.Build.DefinitionHash!=draft.Definition.Hash()){return null;}
        return imported;
    }
}
