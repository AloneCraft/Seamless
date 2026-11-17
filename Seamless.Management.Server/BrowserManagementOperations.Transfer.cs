namespace Seamless.Management.Server;

public sealed partial class BrowserManagementOperations:IManagementTransferOperations{
    private IManagementTransferOperations Transfers=>this.operations as IManagementTransferOperations??throw new ManagementException("Unsupported","この接続は外部DBの取り込みに対応していません。");
    public Task<ExternalConnectionInfo> ConnectExternalAsync(ExternalConnectionRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Transfers.ConnectExternalAsync(request,cancellationToken));
    public Task<ExternalImportPreview> PreviewExternalImportAsync(ExternalImportPreviewRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Transfers.PreviewExternalImportAsync(request,cancellationToken));
    public Task<ExternalTransferStatus> StartExternalImportAsync(ExternalImportRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Transfers.StartExternalImportAsync(request,cancellationToken));
    public Task<ExternalTransferStatus> ExternalTransferStatusAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Transfers.ExternalTransferStatusAsync(operationId,cancellationToken));
}
