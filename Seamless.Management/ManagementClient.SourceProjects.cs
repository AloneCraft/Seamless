namespace Seamless.Management;

public sealed partial class ManagementClient:IManagementSourceOperations{
    public Task<SourceProjectLocation> OpenSourceLocationAsync(string path,CancellationToken cancellationToken=default)=>this.CallAsync<SourceProjectLocation>("sourceLocation","",new{path},cancellationToken);
    public Task<SourceProjectDocument> OpenSourceProjectAsync(SourceProjectOpenRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<SourceProjectDocument>("sourceProject","",request,cancellationToken);
    public Task<SourceProjectDocument> SynchronizeSourceProjectAsync(SourceProjectSynchronization request,CancellationToken cancellationToken=default)=>
        this.CallAsync<SourceProjectDocument>("sourceSynchronize","",request with{Baseline=request.Baseline.Freeze(),Source=request.Source.Freeze()},cancellationToken);
}
