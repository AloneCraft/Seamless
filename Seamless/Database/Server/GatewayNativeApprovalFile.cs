using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Seamless.Database.Client;
using Seamless.Database.Query;
using Seamless.Management;
using Seamless.Security;

namespace Seamless.Database.Server;

public sealed record GatewayNativeApprovalDocument(Guid ManagementInstanceId,Guid GatewayWalId,IReadOnlyList<GatewayNativeQueryResource> Queries);

/// <summary>ローカル管理者が置換する承認文書。検証成功まで稼働中の承認を保持する。</summary>
internal sealed class GatewayNativeApprovalFile(string path,Func<GatewayNativeApprovalDocument,bool,CancellationToken,Action> prepare,bool managed){
    private const int MaximumBytes=4*1024*1024;
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter()}};
    private byte[]? appliedHash;
    private GatewayNativeApprovalDocument? current;
    private bool rejected;
    private readonly object gate=new();

    internal void LoadInitial()=>this.Reload(true,CancellationToken.None);

    internal async Task WatchAsync(CancellationToken cancellationToken){
        if(managed){return;}
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try{
            while(await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)){
                try{
                    this.Reload(false,cancellationToken);
                    this.rejected=false;
                }catch(Exception error)when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or AuthorizationException or NativeQueryException or SqlQueryException){
                    if(!this.rejected){
                        this.rejected=true;
                        Console.Error.WriteLine("Gateway native approval file rejected; previous approvals remain active. "+error.Message);
                    }
                }
            }
        }catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){}
    }

    internal static string Export(GatewayNativeApprovalDocument document)=>JsonSerializer.Serialize(document,Json);

    internal GatewayNativeQueryResource? Append(GatewayNativeQueryResource approval,CancellationToken cancellationToken){
        if(!managed){throw new InvalidOperationException("This approval file uses local administration.");}
        if(!Monitor.TryEnter(this.gate)){throw new ManagementException("Busy","別の承認登録が処理中です。");}
        try{
            cancellationToken.ThrowIfCancellationRequested();
            var previous=this.current??throw new InvalidOperationException("The approval file has not been loaded.");
            using(var existing=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                if(existing.Length>MaximumBytes||!SHA256.HashData(existing).AsSpan().SequenceEqual(this.appliedHash)){
                    throw new ManagementException("Conflict","承認ファイルが外部で変更されました。サーバーで状態を確認してください。");
                }
            }
            var registered=previous.Queries.FirstOrDefault(item=>item.DatabaseId==approval.DatabaseId&&item.ModuleId==approval.ModuleId&&
                item.QueryId==approval.QueryId&&item.QueryGenerationId==approval.QueryGenerationId);
            if(registered is not null){return registered;}
            var next=previous with{Queries=[..previous.Queries,approval]};
            var publish=prepare(next,false,cancellationToken);
            var bytes=JsonSerializer.SerializeToUtf8Bytes(next,Json);
            if(bytes.Length>MaximumBytes){throw new ManagementException("Limit","承認ファイルの容量上限に達しました。");}
            var nextHash=SHA256.HashData(bytes);
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    output.Write(bytes);
                    output.Flush(true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary,path,overwrite:true);
                this.current=next;
                this.appliedHash=nextHash;
                publish();
            }finally{if(File.Exists(temporary)){File.Delete(temporary);}}
            return null;
        }finally{Monitor.Exit(this.gate);}
    }

    private void Reload(bool initial,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
        var length=stream.Length;
        if(length is <=0 or >MaximumBytes){throw new InvalidDataException("The native approval file must be between 1 byte and 4 MiB.");}
        var rented=ArrayPool<byte>.Shared.Rent((int)length);
        try{
            var contents=rented.AsSpan(0,(int)length);
            stream.ReadExactly(contents);
            if(stream.ReadByte()!=-1){throw new InvalidDataException("The native approval file changed while reading.");}
            Span<byte> hash=stackalloc byte[32];
            SHA256.HashData(contents,hash);
            if(this.appliedHash is{} previous&&hash.SequenceEqual(previous)){return;}
            var document=JsonSerializer.Deserialize<GatewayNativeApprovalDocument>(contents,Json)??throw new InvalidDataException("The native approval document is empty.");
            var publish=prepare(document,initial,cancellationToken);
            this.current=document;
            this.appliedHash=hash.ToArray();
            publish();
        }finally{ArrayPool<byte>.Shared.Return(rented);}
    }
}
