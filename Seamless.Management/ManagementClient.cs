using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Seamless.Management;

public static class ManagementWire{
    public const int MaximumBytes=16*1024*1024;
    public static Task WriteAsync<T>(Stream stream,T value,CancellationToken cancellationToken)=>WriteAsync(stream,value,cancellationToken,null);
    public static async Task WriteAsync<T>(Stream stream,T value,CancellationToken cancellationToken,Func<int,IDisposable>? reserve){
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,ManagementJson.Options);if(bytes.Length>MaximumBytes){throw new ManagementException("Limit","要求・応答の容量上限を超えています。");}
        using var reservation=reserve?.Invoke(bytes.Length+4);
        var length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,bytes.Length);await stream.WriteAsync(length,cancellationToken);await stream.WriteAsync(bytes,cancellationToken);await stream.FlushAsync(cancellationToken);
    }
    public static Task<T> ReadAsync<T>(Stream stream,CancellationToken cancellationToken)=>ReadAsync<T>(stream,cancellationToken,null);
    public static Task<T> ReadAsync<T>(Stream stream,CancellationToken cancellationToken,Action<int>? admit)=>
        ReadCoreAsync<T>(stream,cancellationToken,admit,allowEnd:false);
    internal static Task<RpcRequest?> ReadRequestOrEndAsync(Stream stream,CancellationToken cancellationToken,Action<int>? admit)=>
        ReadCoreAsync<RpcRequest?>(stream,cancellationToken,admit,allowEnd:true);
    private static async Task<T> ReadCoreAsync<T>(Stream stream,CancellationToken cancellationToken,Action<int>? admit,bool allowEnd){
        var header=new byte[4];
        var read=await stream.ReadAtLeastAsync(header,header.Length,throwOnEndOfStream:!allowEnd,cancellationToken:cancellationToken);
        if(read==0){return default!;}
        if(read<header.Length){throw new EndOfStreamException("管理RPCのフレームヘッダーが途中で終了しました。");}
        var count=BinaryPrimitives.ReadInt32BigEndian(header);
        if(count is <1 or >MaximumBytes){throw new ManagementException("Limit","フレームの容量が不正です。");}
        admit?.Invoke(count);
        var bytes=new byte[count];await stream.ReadExactlyAsync(bytes,cancellationToken);return JsonSerializer.Deserialize<T>(bytes,ManagementJson.Options)??throw new ManagementException("Protocol","応答がありません。");
    }
}

public sealed partial class ManagementClient(string host,int port,X509Certificate2? trustedRoot=null):IDisposable,IManagementOperations{
    private string? pipeName;
    private IPAddress? connectAddress;
    public static ManagementClient ForLocalPipe(string pipeName){ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);return new("",0){pipeName=pipeName};}
    public static ManagementClient ForLocalTcp(LocalServerConnection endpoint,X509Certificate2? trustedRoot=null)=>new(endpoint.Host,endpoint.Port,trustedRoot){connectAddress=endpoint.GetLoopbackAddress()};
    public Task<UserInfo> LocalIdentityAsync(CancellationToken cancellationToken=default)=>this.CallAsync<UserInfo>("localIdentity","",new{},cancellationToken);
    public string? Token{get;private set;}
    public async Task<LoginResult> LoginAsync(string user,string password,CancellationToken cancellationToken=default){var result=await this.CallAsync<LoginResult>("login","",new LoginRequest(user,password),cancellationToken);this.Token=result.Token;return result;}
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>this.CallAsync<ServerInfo>("info","",new{},cancellationToken);
    public Task<CatalogPage<CatalogDatabase>> DatabasesAsync(long cursor=0,string versionToken="",CancellationToken cancellationToken=default)=>
        this.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{cursor,versionToken},cancellationToken);
    public Task<AuditPage> AuditEventsAsync(AuditQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<AuditPage>("auditEvents","system",query,cancellationToken);
    public Task<AuditReadPage> ReadSummariesAsync(AuditQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<AuditReadPage>("auditSummaries","system",query,cancellationToken);
    public Task<AuditDeliveryPage> AuditDeliveryStatusAsync(long cursor=0,CancellationToken cancellationToken=default)=>this.CallAsync<AuditDeliveryPage>("auditDelivery","system",new{cursor},cancellationToken);
    public Task<AuditReadBufferStatus> ReadAuditStatusAsync(CancellationToken cancellationToken=default)=>this.CallAsync<AuditReadBufferStatus>("auditReadBuffer","system",new{},cancellationToken);
    public Task<T> CallAsync<T>(string operation,string database,object payload,CancellationToken cancellationToken=default)=>this.CallCoreAsync<T>(operation,database,payload,cancellationToken);
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(string accessToken,DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>
        this.CallCoreAsync<DatabaseAuthorizationList>("authorizationDatabases","system",query,cancellationToken,accessToken);
    public Task<DatabaseAuthorizationList> AuthorizationDatabasesAsync(DatabaseAuthorizationListQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<DatabaseAuthorizationList>("authorizationDatabases","system",query,cancellationToken);
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<AuthorizationScopeInfo>("authorizationScope","system",query,cancellationToken);
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(AuthorizationScopeEdit edit,CancellationToken cancellationToken=default)=>this.CallAsync<AuthorizationChangeResult>("authorizationScopeChange","system",edit.Freeze(),cancellationToken);
    public Task<AuthorizationScopeInfo> AuthorizationScopeAsync(string accessToken,AuthorizationScopeQuery query,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationScopeInfo>("authorizationScope","system",query,cancellationToken,accessToken);
    public Task<AuthorizationChangeResult> ChangeAuthorizationScopeAsync(string accessToken,AuthorizationScopeEdit edit,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationChangeResult>("authorizationScopeChange","system",edit.Freeze(),cancellationToken,accessToken);
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(DatabaseAuthorizationQuery query,CancellationToken cancellationToken=default)=>this.CallAsync<DatabaseAuthorizationInfo>("authorizationDatabase","system",query,cancellationToken);
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(DatabaseAuthorizationEdit edit,CancellationToken cancellationToken=default)=>this.CallAsync<AuthorizationChangeResult>("authorizationChange","system",edit,cancellationToken);
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(Guid operationId,CancellationToken cancellationToken=default)=>this.CallAsync<AuthorizationChangeResult>("authorizationOperation","system",operationId,cancellationToken);
    public Task<DatabaseAuthorizationInfo> DatabaseAuthorizationAsync(string accessToken,Guid databaseId,long version=0,long cursor=0,CancellationToken cancellationToken=default)=>this.CallCoreAsync<DatabaseAuthorizationInfo>("authorizationDatabase","system",new DatabaseAuthorizationQuery(databaseId,version,cursor),cancellationToken,accessToken);
    public Task<AuthorizationChangeResult> ChangeDatabaseAuthorizationAsync(string accessToken,DatabaseAuthorizationEdit change,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationChangeResult>("authorizationChange","system",change,cancellationToken,accessToken);
    public Task<AuthorizationChangeResult> AuthorizationChangeResultAsync(string accessToken,Guid operationId,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationChangeResult>("authorizationOperation","system",operationId,cancellationToken,accessToken);
    public Task<AuthorizationSubjectChangeResult> ChangeAuthorizationSubjectAsync(string accessToken,AuthorizationSubjectChange change,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationSubjectChangeResult>("authorizationSubjectChange","system",change,cancellationToken,accessToken);
    public Task<AuthorizationSubjectChangeResult> AuthorizationSubjectChangeResultAsync(string accessToken,Guid operationId,CancellationToken cancellationToken=default)=>this.CallCoreAsync<AuthorizationSubjectChangeResult>("authorizationSubjectOperation","system",operationId,cancellationToken,accessToken);
    public Task<GatewayPublicationIdentity> GatewayPublicationAsync(string accessToken,GatewayPublicationRequest request,CancellationToken cancellationToken=default)=>
        this.CallCoreAsync<GatewayPublicationIdentity>("gatewayPublication","system",request,cancellationToken,accessToken);
    public Task<GatewayPublicationList> GatewayPublicationsAsync(string accessToken,GatewayPublicationListRequest request,CancellationToken cancellationToken=default)=>
        this.CallCoreAsync<GatewayPublicationList>("gatewayPublications","system",request,cancellationToken,accessToken);
    public Task<GatewayPublicationApprovalResult> ExportGatewayApprovalAsync(string accessToken,GatewayPublicationApprovalRequest request,CancellationToken cancellationToken=default)=>
        this.CallCoreAsync<GatewayPublicationApprovalResult>("gatewayApprovalExport","system",request,cancellationToken,accessToken);
    public Task<GatewayPublicationApprovalResult> RegisterGatewayApprovalAsync(string accessToken,GatewayPublicationApprovalRequest request,CancellationToken cancellationToken=default)=>
        this.CallCoreAsync<GatewayPublicationApprovalResult>("gatewayApprovalRegister","system",request,cancellationToken,accessToken);
    private async Task<T> CallCoreAsync<T>(string operation,string database,object payload,CancellationToken cancellationToken,string? accessToken=null){
        var request=new RpcRequest(Guid.NewGuid(),operation,accessToken??this.Token,database,ManagementJson.Element(payload));
        var connection=await this.OpenForRequestAsync(cancellationToken);
        try{
            await ManagementWire.WriteAsync(connection,request,cancellationToken);
            var response=await ManagementWire.ReadAsync<RpcResponse>(connection,cancellationToken);
            Check(request.Id,response);
            return ManagementJson.Read<T>(response.Payload);
        }finally{
            await connection.DisposeAsync();
        }
    }
    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(string database,ExecuteRequest request,[EnumeratorCancellation]CancellationToken cancellationToken=default){
        var rpc=new RpcRequest(Guid.NewGuid(),"execute",this.Token,database,ManagementJson.Element(request));
        var connection=await this.OpenForRequestAsync(cancellationToken);
        try{
            await ManagementWire.WriteAsync(connection,rpc,cancellationToken);
            while(true){
                var response=await ManagementWire.ReadAsync<RpcResponse>(connection,cancellationToken);
                Check(rpc.Id,response);
                if(response.Kind=="complete"){yield break;}
                if(response.Kind!="row"){throw new ManagementException("Protocol","行の応答ではありません。");}
                yield return ManagementJson.Read<ExecutionRow>(response.Payload);
            }
        }finally{
            await connection.DisposeAsync();
        }
    }
    private async Task<Stream> OpenAsync(CancellationToken cancellationToken){
        if(this.pipeName is{} name){var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,System.Security.Principal.TokenImpersonationLevel.Identification);try{await pipe.ConnectAsync(5000,cancellationToken);return pipe;}catch{pipe.Dispose();throw;}}
        var client=new TcpClient{NoDelay=true};try{
            if(this.connectAddress is{} address){await client.ConnectAsync(address,port,cancellationToken);}else{await client.ConnectAsync(host,port,cancellationToken);}
            var stream=new SslStream(client.GetStream(),leaveInnerStreamOpen:false);
            try{var options=new SslClientAuthenticationOptions{TargetHost=host};if(trustedRoot is not null){options.CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};options.CertificateChainPolicy.CustomTrustStore.Add(trustedRoot);}
                await stream.AuthenticateAsClientAsync(options,cancellationToken);return stream;
            }catch{await stream.DisposeAsync();throw;}
        }catch{client.Dispose();throw;}
    }
    private static void Check(Guid id,RpcResponse response){if(response.Id!=id){throw new ManagementException("Protocol","応答の要求IDが一致しません。");}if(response.ErrorCode is not null){throw new ManagementException(response.ErrorCode,response.Error??"管理操作に失敗しました。");}}
}
