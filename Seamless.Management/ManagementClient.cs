using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Seamless.Management;

public static class ManagementWire{
    public const int MaximumBytes=16*1024*1024;
    public static async Task WriteAsync<T>(Stream stream,T value,CancellationToken cancellationToken){
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,ManagementJson.Options);if(bytes.Length>MaximumBytes){throw new ManagementException("Limit","要求・応答の容量上限を超えています。");}
        var length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,bytes.Length);await stream.WriteAsync(length,cancellationToken);await stream.WriteAsync(bytes,cancellationToken);await stream.FlushAsync(cancellationToken);
    }
    public static async Task<T> ReadAsync<T>(Stream stream,CancellationToken cancellationToken){
        var length=new byte[4];await stream.ReadExactlyAsync(length,cancellationToken);var count=BinaryPrimitives.ReadInt32BigEndian(length);if(count is <1 or >MaximumBytes){throw new ManagementException("Limit","フレームの容量が不正です。");}
        var bytes=new byte[count];await stream.ReadExactlyAsync(bytes,cancellationToken);return JsonSerializer.Deserialize<T>(bytes,ManagementJson.Options)??throw new ManagementException("Protocol","応答がありません。");
    }
}

public sealed class ManagementClient(string host,int port,X509Certificate2? trustedRoot=null){
    private string? pipeName;
    public static ManagementClient ForLocalPipe(string pipeName){ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);return new("",0){pipeName=pipeName};}
    public Task<UserInfo> LocalIdentityAsync(CancellationToken cancellationToken=default)=>this.CallAsync<UserInfo>("localIdentity","",new{},cancellationToken);
    public string? Token{get;private set;}
    public async Task<LoginResult> LoginAsync(string user,string password,CancellationToken cancellationToken=default){var result=await this.CallAsync<LoginResult>("login","",new LoginRequest(user,password),cancellationToken);this.Token=result.Token;return result;}
    public Task<ServerInfo> InfoAsync(CancellationToken cancellationToken=default)=>this.CallAsync<ServerInfo>("info","",new{},cancellationToken);
    public Task<T> CallAsync<T>(string operation,string database,object payload,CancellationToken cancellationToken=default)=>this.CallCoreAsync<T>(operation,database,payload,cancellationToken);
    private async Task<T> CallCoreAsync<T>(string operation,string database,object payload,CancellationToken cancellationToken){
        await using var connection=await this.OpenAsync(cancellationToken);var request=new RpcRequest(Guid.NewGuid(),operation,this.Token,database,ManagementJson.Element(payload));
        await ManagementWire.WriteAsync(connection,request,cancellationToken);var response=await ManagementWire.ReadAsync<RpcResponse>(connection,cancellationToken);Check(request.Id,response);return ManagementJson.Read<T>(response.Payload);
    }
    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(string database,ExecuteRequest request,[EnumeratorCancellation]CancellationToken cancellationToken=default){
        await using var connection=await this.OpenAsync(cancellationToken);var rpc=new RpcRequest(Guid.NewGuid(),"execute",this.Token,database,ManagementJson.Element(request));await ManagementWire.WriteAsync(connection,rpc,cancellationToken);
        while(true){var response=await ManagementWire.ReadAsync<RpcResponse>(connection,cancellationToken);Check(rpc.Id,response);if(response.Kind=="complete"){yield break;}if(response.Kind!="row"){throw new ManagementException("Protocol","行の応答ではありません。");}yield return ManagementJson.Read<ExecutionRow>(response.Payload);}
    }
    private async Task<Stream> OpenAsync(CancellationToken cancellationToken){
        if(this.pipeName is{} name){var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);try{await pipe.ConnectAsync(5000,cancellationToken);return pipe;}catch{pipe.Dispose();throw;}}
        var client=new TcpClient{NoDelay=true};try{
            await client.ConnectAsync(host,port,cancellationToken);var stream=new SslStream(client.GetStream(),leaveInnerStreamOpen:false);
            try{var options=new SslClientAuthenticationOptions{TargetHost=host};if(trustedRoot is not null){options.CertificateChainPolicy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};options.CertificateChainPolicy.CustomTrustStore.Add(trustedRoot);}
                await stream.AuthenticateAsClientAsync(options,cancellationToken);return stream;
            }catch{await stream.DisposeAsync();throw;}
        }catch{client.Dispose();throw;}
    }
    private static void Check(Guid id,RpcResponse response){if(response.Id!=id){throw new ManagementException("Protocol","応答の要求IDが一致しません。");}if(response.ErrorCode is not null){throw new ManagementException(response.ErrorCode,response.Error??"管理操作に失敗しました。");}}
}
