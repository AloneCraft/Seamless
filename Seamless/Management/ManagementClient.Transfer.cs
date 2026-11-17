namespace Seamless.Management;

public sealed partial class ManagementClient:IManagementTransferOperations{
    public Task<ExternalConnectionInfo> ConnectExternalAsync(ExternalConnectionRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<ExternalConnectionInfo>("externalConnect","",request,cancellationToken);
    public Task<ExternalImportPreview> PreviewExternalImportAsync(ExternalImportPreviewRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<ExternalImportPreview>("externalPreview","",request,cancellationToken);
    public Task<ExternalTransferStatus> StartExternalImportAsync(ExternalImportRequest request,CancellationToken cancellationToken=default)=>this.CallAsync<ExternalTransferStatus>("externalImport","",request,cancellationToken);
    public Task<ExternalTransferStatus> ExternalTransferStatusAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.CallAsync<ExternalTransferStatus>("externalStatus","",new{operationId},cancellationToken);
}
