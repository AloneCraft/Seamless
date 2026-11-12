namespace Seamless.Management;

public sealed partial class ManagementClient:IManagementSourceOperations{
    public Task<SourceDirectory> BrowseSourceDirectoryAsync(string path,CancellationToken cancellationToken=default)=>this.CallAsync<SourceDirectory>("sourceDirectory","",new{path},cancellationToken);
    public Task<SourceProjectConflict> InspectSourceConflictAsync(SourceProjectConflictRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectConflict>("sourceInspectConflict","",request,cancellationToken);
    public Task<SourceProjectConflictResult> ResolveSourceConflictAsync(SourceProjectConflictResolution request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectConflictResult>("sourceResolveConflict","",request,cancellationToken);
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.CallAsync<SourceProjectLocation>("sourceLocation","",new{path},cancellationToken);
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<SourceProjectDocument>("sourceProject","",request,cancellationToken);
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectDocument>("sourceSynchronize","",request with{Baseline=request.Baseline.Freeze(),Source=request.Source.Freeze()},cancellationToken);
    public Task<SourceProjectDocument> CreateSourceProjectAsync(SourceProjectCreateRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectDocument>("sourceCreate","",request,cancellationToken);
    public Task<Draft> SaveSourceProjectDraftAsync(string id,CancellationToken cancellationToken=default)=>
        this.CallAsync<Draft>("sourceSaveDraft","",new{id},cancellationToken);
    public Task<SourceProjectBuild> InspectSourceProjectBuildAsync(SourceProjectBuildRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectBuild>("sourceInspectBuild","",request,cancellationToken);
    public Task<ImportedSourceBuild> ImportSourceProjectBuildAsync(SourceProjectBuildImportRequest request,CancellationToken cancellationToken=default)=>
        this.CallAsync<ImportedSourceBuild>("sourceImportBuild","",request,cancellationToken);
    public Task<ImportedSourceBuild> GetImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        this.CallAsync<ImportedSourceBuild>("sourceImportedBuild","",key,cancellationToken);
    public async Task<ImportedSourceBuild?> FindImportedSourceBuildAsync(DraftKey key,CancellationToken cancellationToken=default)=>
        (await this.CallAsync<SourceBuildLookup>("sourceFindImportedBuild","",key,cancellationToken).ConfigureAwait(false)).Build;
}
