namespace Seamless.Management.Server;

internal sealed record ConfirmedQueryBuild(string Id,long Version,SourceProjectBuild Build,QueryAuditMetadata? Audit);
internal sealed record ConfirmedDatabaseBuild(SourceProjectBuild Build,ConfirmedQueryBuild[] Queries);

public sealed partial class ManagementStore{
    internal Dictionary<string,ConfirmedDatabaseBuild> ConfirmedSourceBuilds{get;}=new(StringComparer.Ordinal);
    internal Dictionary<string,ImportedSourceBuild> ImportedSourceBuilds{get;}=new(StringComparer.Ordinal);
    internal object SourceBuildFileGate{get;}=new();
    internal string BuildWorkRoot{get;}
    internal string SdkBuildWorkRoot=>Path.Combine(this.BuildWorkRoot,"sdk-drafts");
    private SourceBuildArtifactStore? sourceBuilds;
    internal SourceBuildArtifactStore SourceBuilds{get{lock(this.SourceBuildFileGate){return this.sourceBuilds??=new(this.BuildWorkRoot,this.SourceBuildFileGate);}}}

    private static string ResolveBuildWorkRoot(string managementRoot){
        const string variable="SEAMLESS_BUILD_WORK_ROOT";
        var configured=Environment.GetEnvironmentVariable(variable);
        if(string.IsNullOrWhiteSpace(configured)){return managementRoot;}
        if(!Path.IsPathFullyQualified(configured)||!Directory.Exists(configured)){
            throw new ManagementException("Unavailable",variable+"に利用可能なディレクトリの絶対パスを指定してください: "+configured);
        }
        var parent=Path.GetFullPath(configured);
        var root=Path.TrimEndingDirectorySeparator(managementRoot);
        var identity=OperatingSystem.IsWindows()?root.ToUpperInvariant():root;
        var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)))[..24];
        var workspace=Path.Combine(parent,"seamless-build-"+key);
        if(IsInside(root,workspace)||new[]{"drafts","generations","system","sources","backups"}.Any(name=>IsInside(workspace,Path.Combine(root,name)))){
            throw new ManagementException("InvalidRequest","ビルド作業先が管理データの領域と重なっています: "+workspace);
        }
        SourceWorkspace.CheckLinks(workspace);
        return workspace;

        static bool IsInside(string path,string directory){
            var relative=Path.GetRelativePath(directory,path);
            return !Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
        }
    }

    private void RecoverSourceBuilds(){
        this.SourceBuilds.CleanSession();
        if(this.BuildWorkRoot!=this.Root){new SourceBuildArtifactStore(this.Root,this.SourceBuildFileGate).CleanSession();}
    }
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
            var subject=DraftSubject(user.Name,draft.Definition.Id);
            this.SourceBuilds.Retain("confirmation",subject);
            this.store.ConfirmedSourceBuilds[subject]=build;
        }
    }

    private ConfirmedDatabaseBuild? FindConfirmedDatabaseBuild(UserAccount user,Draft draft,Definition candidate){
        lock(this.store.Gate){
            if(!this.store.ConfirmedSourceBuilds.TryGetValue(DraftSubject(user.Name,draft.Definition.Id),out var confirmed)){return null;}
            var build=confirmed.Build;
            return build.DraftRevision==draft.Revision&&build.BaseVersion==draft.BaseVersion&&
                build.DefinitionHash==candidate.Hash()&&build.SourceHash==candidate.Source.Hash()?confirmed:null;
        }
    }

    private void ReleaseConfirmation(string subject){
        if(this.store.ConfirmedSourceBuilds.Remove(subject)){this.SourceBuilds.Release("confirmation",subject);}
    }
    private void ReleaseImportedBuild(string subject){
        if(this.store.ImportedSourceBuilds.Remove(subject)){this.SourceBuilds.Release("import",subject);}
    }
    private void ReleaseSourceBuilds(string id,long throughVersion){
        lock(this.store.Gate){
            foreach(var subject in this.store.ConfirmedSourceBuilds.Where(item=>item.Value.Build.DefinitionId==id&&item.Value.Build.BaseVersion<=throughVersion).Select(item=>item.Key).ToArray()){
                this.ReleaseConfirmation(subject);
            }
            foreach(var subject in this.store.ImportedSourceBuilds.Where(item=>item.Value.Build.DefinitionId==id&&item.Value.Build.BaseVersion<=throughVersion).Select(item=>item.Key).ToArray()){
                this.ReleaseImportedBuild(subject);
            }
        }
    }
    internal bool CancelConfirmation(UserAccount user,DraftKey key){
        lock(this.store.Gate){
            var draft=this.FindDraft(user,key.Id);
            if(draft?.Revision!=key.Revision){return false;}
            Require(this.CurrentActor(user),this.store.CurrentDatabase(draft.Definition),ManagementPermission.Apply);
            var subject=DraftSubject(user.Name,key.Id);
            if(this.store.ConfirmedSourceBuilds.TryGetValue(subject,out var confirmed)&&confirmed.Build.BundleHash==key.BundleHash){
                this.ReleaseConfirmation(subject);
            }
            return true;
        }
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
            this.store.ImportedSourceBuilds.TryGetValue(key,out var previous);
            if(previous is not null&&previous.Build.DraftRevision==build.DraftRevision&&previous.Build.DefinitionHash==build.DefinitionHash&&previous.Build.BundleHash==build.BundleHash){return previous;}
            var imported=new ImportedSourceBuild(build,DateTimeOffset.UtcNow);
            this.store.Write(()=>this.store.AddHistory(user.Name,this.store.CurrentDatabase(draft.Definition),"SourceBuildImported",build.DefinitionId,draft.Revision));
            this.SourceBuilds.Retain("import",key);
            this.store.ImportedSourceBuilds[key]=imported;
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
        this.store.ImportedSourceBuilds.TryGetValue(DraftSubject(user.Name,draft.Definition.Id),out var imported);
        if(imported is null||imported.Build.DraftRevision!=draft.Revision||imported.Build.BaseVersion!=draft.BaseVersion||imported.Build.DefinitionHash!=draft.Definition.Hash()){return null;}
        return imported;
    }
}
