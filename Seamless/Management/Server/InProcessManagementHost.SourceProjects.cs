namespace Seamless.Management.Server;

public sealed partial class InProcessManagementHost:IManagementSourceOperations{
    private readonly SourceProjectService sourceProjects=new();
    public Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default)=>this.sourceProjects.BrowseSourceDirectoryAsync(path,cancellationToken);
    public Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default)=>this.sourceProjects.InspectSourceConflictAsync(request,cancellationToken);
    public Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default)=>this.sourceProjects.ResolveSourceConflictAsync(request,cancellationToken);
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.sourceProjects.OpenSourceLocationAsync(path,cancellationToken);
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.sourceProjects.OpenSourceProjectAsync(request,cancellationToken);
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default)=>this.sourceProjects.SynchronizeSourceProjectAsync(request,cancellationToken);
    public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default)=>this.sourceProjects.CreateSourceProjectAsync(request,this.GetDraftAsync,cancellationToken);
    public Task<Draft> SaveSourceProjectDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.sourceProjects.SaveDatabaseDraftAsync(id,this,cancellationToken);
    public Task<SourceProjectBuild> InspectSourceProjectBuildAsync(SourceProjectBuildRequest request,CancellationToken cancellationToken=default)=>
        this.sourceProjects.InspectBuildAsync(request,this.GetDraftAsync,SourceBuildArtifactInspector.Inspect,cancellationToken);
    public async Task<ImportedSourceBuild> ImportSourceProjectBuildAsync(SourceProjectBuildImportRequest request,CancellationToken cancellationToken=default){
        var artifacts=this.service!.SourceBuilds;
        using var use=artifacts.Use();
        var build=await this.sourceProjects.InspectBuildAsync(request.Build,this.GetDraftAsync,
            (context,path,token)=>artifacts.Capture(context,path,request.ExpectedBundleHash,token),cancellationToken).ConfigureAwait(false);
        return await this.InvokeAsync((service,user)=>service.BindImportedSourceBuild(user,build),cancellationToken).ConfigureAwait(false);
    }
    public Task<ImportedSourceBuild> GetImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.GetImportedSourceBuild(user,key),cancellationToken).AsTask();
    public Task<ImportedSourceBuild?> FindImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.InvokeAsync((service,user)=>service.FindImportedSourceBuild(user,key),cancellationToken).AsTask();
}
