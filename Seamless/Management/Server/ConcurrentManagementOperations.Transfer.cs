namespace Seamless.Management.Server;

public sealed partial class ConcurrentManagementOperations:IManagementTransferOperations{
    private ExternalTransferService? transfer;
    // 同居のサーバーは OS の所有者として動く(画面と同じプロセス)。外部DBの取り込みは所有者だけに出す。
    private ExternalTransferService Transfer=>this.transfer??=new(this.service,this.sources,new WindowsCredentialStore());
    public Task<ExternalConnectionInfo> ConnectExternalAsync(ExternalConnectionRequest request,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.Transfer.ConnectAsync(request,cancellationToken),cancellationToken);
    public Task<ExternalImportPreview> PreviewExternalImportAsync(ExternalImportPreviewRequest request,CancellationToken cancellationToken=default)=>this.InvokeTaskAsync(()=>this.Transfer.PreviewAsync(this.owner,request,cancellationToken),cancellationToken);
    public Task<ExternalTransferStatus> StartExternalImportAsync(ExternalImportRequest request,CancellationToken cancellationToken=default)=>this.InvokeAsync(()=>this.Transfer.Start(this.owner,request),cancellationToken);
    public Task<ExternalTransferStatus> ExternalTransferStatusAsync(Guid operationId,CancellationToken cancellationToken=default)=>Task.FromResult(this.Transfer.Status(operationId));
}
