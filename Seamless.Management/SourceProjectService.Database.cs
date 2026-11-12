namespace Seamless.Management;

public sealed partial class SourceProjectService{
    private async Task CheckProjectAsync(OpenProject project,CancellationToken cancellationToken){
        var evaluation=await EvaluateAsync(project.Path,project.TargetFramework,cancellationToken,project.Configuration).ConfigureAwait(false);
        if(!ReadFiles(project.Path,evaluation).SequenceEqual(project.Workspace.State.ProjectBinding!.Files)||
            !Defines(evaluation).SequenceEqual(project.Workspace.Current.Definition.Source.Defines??[])){
            throw new ManagementException("Conflict","Compile対象または条件定義が変更されました。表示中の編集を保持してプロジェクトを再評価してください。");
        }
        await RefreshReferenceTypesAsync(project,evaluation,cancellationToken).ConfigureAwait(false);
    }
    internal Task<Draft> SaveDatabaseDraftAsync(string id,IManagementOperations operations,CancellationToken cancellationToken)=>
        this.SaveDatabaseDraftAsync(id,operations.GetDefinitionAsync,operations.FindDraftAsync,operations.SaveDraftAsync,cancellationToken);

    internal async Task<Draft> SaveDatabaseDraftAsync(string id,
        Func<string,CancellationToken,Task<DefinitionVersion>> getPublished,
        Func<string,CancellationToken,Task<Draft?>> findDraft,
        Func<SaveDraftRequest,CancellationToken,Task<Draft>> saveDraft,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try{
            if(!this.projects.TryGetValue(id,out var project)){throw new ManagementException("NotFound","ソースプロジェクトを開き直してください。");}
            var workspace=project.Workspace;
            if(!workspace.IsDatabaseProject){throw new ManagementException("Unsupported","作成元DBの同期記録が必要です。");}
            await this.CheckProjectAsync(project,cancellation.Token).ConfigureAwait(false);
            var published=await getPublished(workspace.Current.Definition.Id,cancellation.Token).ConfigureAwait(false);
            workspace.AlignProjectPublication(published);
            var local=workspace.Synchronize(workspace.Current.Definition);
            var remote=await findDraft(local.Definition.Id,cancellation.Token).ConfigureAwait(false);
            var publishedDraft=remote is not null&&remote.BaseVersion<published.Version&&
                (remote.Definition with{Migration=null}).Hash()==(published.Definition with{Migration=null}).Hash();
            if(remote is not null&&((remote.BaseVersion!=local.BaseVersion&&!publishedDraft)||remote.Revision<local.DraftRevision)||remote is null&&local.DraftRevision!=0){
                throw new ManagementException("Conflict","DBの下書きと同期記録の版が一致しません。内容を保持しています。");
            }
            var candidate=local.Definition;
            if(remote is not null){
                var ancestor=workspace.State.SavedDraftDefinition;
                if(ancestor is null||ancestor.Hash()!=workspace.State.SavedDraftHash){
                    ancestor=published.Definition;
                    // 旧同期記録は、編集前の全ファイルが現在の下書きと一致する場合だけ比較基準を確認できる。
                    var original=local.Definition with{Source=remote.Definition.Source with{Files=workspace.State.WithoutManager.Files}};
                    if(original.Hash()==remote.Definition.Hash()){ancestor=remote.Definition;}
                }
                var remoteDefinition=remote.Definition;
                if(publishedDraft){remoteDefinition=remoteDefinition with{Migration=null};}
                candidate=SourceWorkspace.MergeDefinitions(ancestor,local.Definition,remoteDefinition);
            }
            if(!candidate.Source.Files.Select(file=>file.Path).Order(StringComparer.Ordinal).SequenceEqual(local.Definition.Source.Files.Select(file=>file.Path).Order(StringComparer.Ordinal))||
                !candidate.Source.References.SequenceEqual(local.Definition.Source.References)||
                !(candidate.Source.Defines??[]).SequenceEqual(local.Definition.Source.Defines??[],StringComparer.Ordinal)){
                throw new ManagementException("Conflict","DB下書きとプロジェクトのファイル構成・参照・条件が異なります。両方の内容を確認してください。");
            }
            candidate=workspace.Synchronize(candidate,local.Definition).Definition;
            var saved=remote;
            if(saved is null||publishedDraft||saved.Definition.Hash()!=candidate.Hash()){
                saved=await saveDraft(new(candidate,remote?.Revision??0,local.BaseVersion,published.Version),cancellation.Token).ConfigureAwait(false);
            }
            workspace.MarkDraftSaved(saved);
            return saved with{Definition=saved.Definition.Freeze()};
        }finally{this.gate.Release();}
    }
}
