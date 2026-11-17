namespace Seamless.Management;

public sealed partial class SourceProjectService{
    public async Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default){
        var editor=request.Editor.Freeze();SourceEditor.Validate(editor);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try{
            var project=await this.ConflictProjectAsync(request.Id,cancellation.Token).ConfigureAwait(false);
            var conflict=project.Workspace.InspectConflict();
            var additional=conflict is not null&&!project.Workspace.ConflictPreserves(project.Workspace.Current.Definition with{Source=editor});
            return new(conflict,additional);
        }finally{this.gate.Release();}
    }
    public async Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default){
        var editor=request.Editor.Freeze();SourceEditor.Validate(editor);
        var files=request.Files.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try{
            var project=await this.ConflictProjectAsync(request.Id,cancellation.Token).ConfigureAwait(false);
            var workspace=project.Workspace;var conflict=workspace.InspectConflict();
            if(conflict is null||conflict.Id!=request.ConflictId||conflict.DiskHash!=request.DiskHash||conflict.ManagerHash!=request.ManagerHash){
                throw new ManagementException("Conflict","比較後に競合か外部ファイルが変わりました。再度確認してください。");
            }
            cancellation.Token.ThrowIfCancellationRequested();
            var current=workspace.Current.Definition with{Source=editor};string? backup=null;
            if(!workspace.ConflictPreserves(current)){backup=workspace.PreserveConflictEditor(conflict.Id,current);}
            var resolved=workspace.ResolveConflict(conflict.Id,conflict.DiskHash,files);
            return new(Document(request.Id,project,resolved.Definition.Source),backup);
        }finally{this.gate.Release();}
    }
    private async Task<OpenProject> ConflictProjectAsync(string id,CancellationToken cancellation){
        if(!this.projects.TryGetValue(id,out var project)){throw new ManagementException("NotFound","ソースプロジェクトを開き直してください。");}
        await this.CheckProjectAsync(project,cancellation).ConfigureAwait(false);return project;
    }
}
