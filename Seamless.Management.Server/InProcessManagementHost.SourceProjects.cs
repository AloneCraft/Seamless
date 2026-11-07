namespace Seamless.Management.Server;

public sealed partial class InProcessManagementHost:IManagementSourceOperations{
    private readonly SourceProjectService sourceProjects=new();
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.sourceProjects.OpenSourceLocationAsync(path,cancellationToken);
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.sourceProjects.OpenSourceProjectAsync(request,cancellationToken);
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default)=>this.sourceProjects.SynchronizeSourceProjectAsync(request,cancellationToken);
}
