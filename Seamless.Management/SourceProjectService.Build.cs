namespace Seamless.Management;

internal sealed record SourceBuildInspectionContext(string ProjectId,DefinitionExport Current,SourceProjectBinding Binding);

public sealed partial class SourceProjectService{
    internal async Task<SourceProjectBuild> InspectBuildAsync(SourceProjectBuildRequest request,
        Func<string,CancellationToken,Task<Draft>> getDraft,Func<SourceBuildInspectionContext,string,CancellationToken,SourceProjectBuild> inspect,CancellationToken cancellationToken){
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,this.stopping.Token);
        await this.gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try{
            if(!this.projects.TryGetValue(request.Id,out var project)){throw new ManagementException("NotFound","ソースプロジェクトを開き直してください。");}
            var workspace=project.Workspace;
            if(!workspace.IsDatabaseProject){throw new ManagementException("Unsupported","作成元DBの同期記録が必要です。");}
            await this.CheckProjectAsync(project,cancellation.Token).ConfigureAwait(false);
            var current=workspace.Synchronize(workspace.Current.Definition);
            if(current.Definition.Source.Hash()!=request.ExpectedSourceHash||workspace.NeedsDraftSave){
                throw new ManagementException("Conflict","ソースを確認し、DB下書きへ反映してから成果物を検査してください。");
            }
            var saved=await getDraft(current.Definition.Id,cancellation.Token).ConfigureAwait(false);
            if(saved.Revision!=current.DraftRevision||saved.BaseVersion!=current.BaseVersion||saved.Definition.Hash()!=current.Definition.Hash()){
                throw new ManagementException("Conflict","DB下書きが変更されています。ソースと下書きを確認してください。");
            }
            var context=new SourceBuildInspectionContext(request.Id,current with{Definition=current.Definition.Freeze()},workspace.State.ProjectBinding!);
            return await Task.Run(()=>inspect(context,request.AssemblyPath,cancellation.Token),cancellation.Token).ConfigureAwait(false);
        }finally{this.gate.Release();}
    }
}
