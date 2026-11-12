using System.Text.Json;

namespace Seamless.Management;

public sealed record ServerDraftMerge(DatabaseDraftRestore Snapshot,Definition Local,Draft Remote,Definition Header,SourceConflict Comparison){
    public long TargetBaseVersion=>this.Snapshot.Published.Version;

    public Definition Resolve(IReadOnlyDictionary<string,string?> choices){
        if(!this.Comparison.Files.Select(file=>file.Path).ToHashSet(StringComparer.Ordinal).SetEquals(choices.Keys)){
            throw new ManagementException("Conflict","すべての比較項目の内容を確定してください。");
        }
        var header=this.Header;
        var files=this.Local.Source.Files.Concat(this.Remote.Definition.Source.Files)
            .DistinctBy(file=>file.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(file=>file.Path,StringComparer.OrdinalIgnoreCase);
        foreach(var item in this.Comparison.Files){
            var text=choices[item.Path];
            if(item.IsMetadata){
                if(item.Automatic&&text==item.PreferManager){continue;}
                if(text==item.Manager){header=this.Local;}
                else if(text==item.Disk){header=this.Remote.Definition;}
                else{throw new ManagementException("Validation","定義の属性と移行設定はどちらか一方を選んでください。");}
            }else if(text is null){files.Remove(item.Path);}
            else{files[item.Path]=new(item.Path,text);}
        }
        var result=header with{Source=header.Source with{Files=files.Values.ToArray(),Revision=this.Local.Source.Revision}};
        SourceEditor.Validate(result.Source);
        if(this.Snapshot.Copies.Any(copy=>copy.Name=="previous-published")){
            DatabaseDesigner.CheckRebasedMigrationTarget(this.Local,result);
        }
        if(this.Snapshot.Copies.Any(copy=>copy.Name=="interrupted-rebase")){
            if(JsonSerializer.Serialize(result.Migration,ManagementJson.Options)!=JsonSerializer.Serialize(this.Remote.Definition.Migration,ManagementJson.Options)){
                throw new ManagementException("MigrationRequired","サーバー保存時の移行設定を維持してください。");
            }
            if(result.Migration?.Tables.Any(table=>table.Columns.Any(column=>column.Expression is not null))==true&&
                (result.Source with{Revision=""}).Hash()!=(this.Remote.Definition.Source with{Revision=""}).Hash()){
                throw new ManagementException("MigrationRequired","中断後の編集が変換式に影響し得ます。移行設定を確認してください。");
            }
            DatabaseDesigner.CheckRebasedMigrationTarget(this.Remote.Definition,result);
        }
        return result;
    }
}

public sealed partial class SourceWorkspace{
    public ServerDraftMerge PreviewServerDraftMerge(DefinitionVersion published,Draft remote,Definition editor,Definition? editorBase,long editorBaseVersion,long editorRevision){
        var snapshot=this.PreviewDatabaseRestore(published,editor,editorBaseVersion,editorRevision);
        if(snapshot.ConflictId is not null){throw ConflictPending();}
        var current=snapshot.Copies.Single(copy=>copy.Name=="workspace-draft").Export;
        if(remote.Definition.Id!=current.Definition.Id||remote.Definition.Database!=current.Definition.Database||
            remote.Definition.Kind!=current.Definition.Kind||remote.BaseVersion!=published.Version||
            editorBaseVersion!=current.BaseVersion||remote.Revision<=current.DraftRevision){
            throw new ManagementException("Conflict","同じ稼働版を基準にした新しいサーバー下書きが必要です。基準版と下書きを確認してください。");
        }
        var disk=snapshot.Copies.Single(copy=>copy.Name=="external-files").Export.Definition.Source;
        var local=editorBase is null?editor:MergeDefinitions(editorBase,editor,current.Definition);
        local=local with{Source=Merge(current.Definition.Source,local.Source,disk)};
        if(current.BaseVersion!=remote.BaseVersion){
            return this.PlanInterruptedRebase(snapshot,current,local,remote);
        }
        var ancestor=this.State.SavedDraftDefinition;
        if(ancestor?.Hash()!=this.State.SavedDraftHash){ancestor=null;}
        return PlanDraftMerge(snapshot,local,remote,ancestor);
    }

    private ServerDraftMerge PlanInterruptedRebase(DatabaseDraftRestore snapshot,DefinitionExport current,Definition local,Draft remote){
        foreach(var directory in Directory.EnumerateDirectories(this.Metadata,"server-draft-merge-*")){
            CheckLinks(directory);
            if(!File.Exists(Path.Combine(directory,"previous-published","definition.seamless.json"))||
                !File.Exists(Path.Combine(directory,"merged","definition.seamless.json"))){continue;}
            var merged=ReadCopy("merged");
            if(merged.BaseVersion!=remote.BaseVersion||merged.DraftRevision!=remote.Revision-1||
                merged.Definition.Hash()!=remote.Definition.Hash()){continue;}
            var workspace=ReadCopy("workspace-draft");
            if(workspace.BaseVersion!=current.BaseVersion||workspace.DraftRevision!=current.DraftRevision||
                workspace.Definition.Id!=current.Definition.Id){continue;}
            var ancestor=ReadCopy("local");
            if(JsonSerializer.Serialize(ancestor.Definition.Migration,ManagementJson.Options)!=JsonSerializer.Serialize(local.Migration,ManagementJson.Options)){
                throw new ManagementException("MigrationRequired","中断後に移行設定が変更されています。現稼働版に合わせて移行設定を確認してください。");
            }
            return PlanDraftMerge(snapshot with{Copies=[..snapshot.Copies,new("interrupted-rebase",ancestor)]},local,remote,ancestor.Definition);

            DefinitionExport ReadCopy(string name)=>Read<DefinitionExport>(Path.Combine(directory,name,"definition.seamless.json"));
        }
        throw new ManagementException("Conflict","別の基準版の下書きです。一致する基準更新の退避記録がありません。");
    }

    public ServerDraftMerge PreviewPublishedRebase(DefinitionVersion previous,DefinitionVersion latest,Draft? remote,Definition editor,Definition? editorBase,long editorRevision){
        var snapshot=this.PreviewDatabaseRestore(latest,editor,previous.Version,editorRevision);
        if(snapshot.ConflictId is not null){throw ConflictPending();}
        var current=snapshot.Copies.Single(copy=>copy.Name=="workspace-draft").Export;
        if(current.BaseVersion!=previous.Version||current.DraftRevision!=editorRevision||
            current.DraftRevision!=(remote?.Revision??0)||remote is not null&&remote.BaseVersion!=previous.Version){
            throw new ManagementException("Conflict","ローカルとサーバーの下書き版が一致しません。先に下書きの競合を確認してください。");
        }
        var disk=snapshot.Copies.Single(copy=>copy.Name=="external-files").Export.Definition.Source;
        var local=editorBase is null?editor:MergeDefinitions(editorBase,editor,current.Definition);
        local=local with{Source=Merge(current.Definition.Source,local.Source,disk)};
        return PlanPublishedRebase(snapshot,previous,remote,local);
    }

    public static ServerDraftMerge PreviewPublishedRebase(DefinitionVersion previous,DefinitionVersion latest,Draft? remote,Definition editor,long editorRevision){
        if(editorRevision!=(remote?.Revision??0)||remote is not null&&remote.BaseVersion!=previous.Version){
            throw new ManagementException("Conflict","サーバーの下書きが進みました。開き直して内容を確認してください。");
        }
        var snapshot=new DatabaseDraftRestore(latest,[new("manager-editor",new(editor,previous.Version,editorRevision))],editor.Source.Hash(),null);
        return PlanPublishedRebase(snapshot,previous,remote,editor);
    }

    private static ServerDraftMerge PlanPublishedRebase(DatabaseDraftRestore snapshot,DefinitionVersion previous,Draft? remote,Definition local){
        var latest=snapshot.Published;
        if(previous.Version<1||previous.Version>=latest.Version||!SameIdentity(previous.Definition)||!SameIdentity(latest.Definition)||
            remote is not null&&!SameIdentity(remote.Definition)){
            throw new ManagementException("Conflict","同じ定義の旧稼働版と現稼働版が必要です。");
        }
        var ancestor=previous.Definition with{Migration=null};
        var active=latest.Definition with{Migration=null};
        DatabaseDesigner.CheckMigrationRebase(previous,latest,local);
        DraftRestoreCopy[] copies=[..snapshot.Copies,new("previous-published",new(previous.Definition,previous.Version,0))];
        if(remote is not null){copies=[..copies,new("server-draft",new(remote.Definition,remote.BaseVersion,remote.Revision))];}
        return PlanDraftMerge(snapshot with{Copies=copies},local,new(active,latest.Owner,remote?.Revision??0,latest.Version,latest.CreatedAt),ancestor);

        bool SameIdentity(Definition definition)=>definition.Id==local.Id&&definition.Database==local.Database&&definition.Kind==local.Kind;
    }

    private static ServerDraftMerge PlanDraftMerge(DatabaseDraftRestore snapshot,Definition local,Draft remote,Definition? ancestor){
        var remoteDefinition=remote.Definition;
        var localHeader=Header(local);
        var remoteHeader=Header(remoteDefinition);
        Definition? ancestorHeader=null;
        if(ancestor is not null){ancestorHeader=Header(ancestor);}
        var header=localHeader;
        var items=new List<SourceConflictFile>();
        try{
            if(ancestorHeader is not null){
                var properties=MergeValue(ancestorHeader with{Migration=null},localHeader with{Migration=null},remoteHeader with{Migration=null},"定義の属性");
                header=properties! with{Migration=MergeMigrations(ancestorHeader.Migration,localHeader.Migration,remoteHeader.Migration)};
            }else if(Json(localHeader)!=Json(remoteHeader)){
                throw new ManagementException("Conflict","比較基準がありません。");
            }
            if(Json(localHeader)!=Json(remoteHeader)){
                var merged=Json(header);
                items.Add(new("定義の属性・移行設定",Json(ancestorHeader!),Json(localHeader),Json(remoteHeader),merged,merged){IsMetadata=true,Automatic=true});
            }
        }catch(ManagementException error)when(error.Code=="Conflict"){
            var left=Json(localHeader);
            var right=Json(remoteHeader);
            items.Add(new("定義の属性・移行設定",ancestorHeader is null?null:Json(ancestorHeader),left,right,left,right){IsMetadata=true});
        }
        foreach(var path in local.Source.Files.Concat(remoteDefinition.Source.Files).Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase)){
            var left=Text(local,path);
            var right=Text(remoteDefinition,path);
            if(left==right){continue;}
            var baseline=Text(ancestor,path);
            if(ancestor is null){items.Add(new(path,null,left,right,left,right));continue;}
            try{
                var merged=MergeFile(path,baseline,left,right);
                items.Add(new(path,baseline,left,right,merged,merged){Automatic=true});
            }catch(ManagementException error)when(error.Code=="Conflict"){items.Add(ConflictFile(path,baseline,left,right));}
        }
        return new(snapshot,local,remote,header,new(Guid.NewGuid(),snapshot.DiskHash,local.Hash(),items.ToArray()));

        static Definition Header(Definition value)=>value with{Source=value.Source with{Files=[],Revision=""}};
        static string Json(Definition value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions(ManagementJson.Options){WriteIndented=true});
        static string? Text(Definition? value,string path)=>value?.Source.Files.SingleOrDefault(file=>string.Equals(file.Path,path,StringComparison.OrdinalIgnoreCase))?.Text;
    }

    public string PrepareServerDraftMerge(ServerDraftMerge plan,Definition candidate){
        using var lease=this.Lock();
        this.AssertCurrent();
        this.CheckServerDraftMergeDisk(plan,candidate);
        var directory=Path.Combine(this.Metadata,"server-draft-merge-"+Guid.NewGuid().ToString("N"));
        CheckLinks(directory);
        foreach(var copy in plan.Snapshot.Copies){SourceArchive.Export(Path.Combine(directory,copy.Name),copy.Export);}
        SourceArchive.Export(Path.Combine(directory,"local"),new(plan.Local,this.Current.BaseVersion,this.Current.DraftRevision));
        SourceArchive.Export(Path.Combine(directory,"server"),new(plan.Remote.Definition,plan.Remote.BaseVersion,plan.Remote.Revision));
        SourceArchive.Export(Path.Combine(directory,"merged"),new(candidate,plan.Remote.BaseVersion,plan.Remote.Revision));
        return directory;
    }

    public void FinishServerDraftMerge(ServerDraftMerge plan,Definition candidate,Draft saved){
        using var lease=this.Lock();
        this.AssertCurrent();
        var disk=this.CheckServerDraftMergeDisk(plan,candidate);
        if(saved.BaseVersion!=plan.Remote.BaseVersion||saved.Revision<plan.Remote.Revision||saved.Definition.Hash()!=candidate.Hash()){
            throw new ManagementException("Conflict","保存した下書きが比較結果と一致しません。");
        }
        this.Commit(this.State with{
            Current=new(saved.Definition,saved.BaseVersion,saved.Revision),Synchronized=saved.Definition.Source,
            Published=plan.Snapshot.Published.Definition.Source,
            SavedDraftHash=saved.Definition.Hash(),SavedDraftDefinition=saved.Definition.Freeze(),UndoConflict=true
        },Writes(disk,saved.Definition.Source));
    }

    private SourceSnapshot CheckServerDraftMergeDisk(ServerDraftMerge plan,Definition candidate){
        var disk=this.ReadDatabaseRestoreDisk(plan.Snapshot);
        var source=candidate.Source;
        if(!source.References.SequenceEqual(disk.References)||!(source.Defines??[]).SequenceEqual(disk.Defines??[],StringComparer.Ordinal)){
            throw new ManagementException("Unsupported","参照またはコンパイル条件の変更を含む下書きは、このC#プロジェクトへ取り込めません。");
        }
        var paths=disk.Files.ToDictionary(file=>this.FilePath(file.Path),file=>file.Path,StringComparer.OrdinalIgnoreCase);
        foreach(var file in source.Files){
            var full=this.FilePath(file.Path);
            if(paths.TryGetValue(full,out var tracked)&&tracked==file.Path){continue;}
            if(paths.ContainsKey(full)||File.Exists(full)||Directory.Exists(full)){
                throw new ManagementException("Conflict","取り込み先に同じファイルまたは管理対象外の項目があります。 "+file.Path);
            }
            paths.Add(full,file.Path);
        }
        return disk;
    }
}
