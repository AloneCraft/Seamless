using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Execution;
using Seamless.Linq;

namespace Seamless.Database.Gateway.Native;

/// <summary>管理されたビルド成果物だけを読み込む型付きクエリ入口。</summary>
public sealed class NativeQueryServer:IDisposable{
    private readonly DatabaseRegistry databases;
    private readonly Dictionary<string,(NativeModuleManifest Manifest,INativeQueryModule Module)> modules=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<long,(TcpClient Client,Task Task)> connections=new();
    private readonly int port;
    private TcpListener? listener;
    private CancellationTokenSource? stop;
    private Task? accepting;
    private long nextConnection;
    public NativeQueryServer(DatabaseRegistry databases,int port=14335){this.databases=databases;this.port=port;}
    public int Port=>((IPEndPoint?)this.listener?.LocalEndpoint)?.Port??this.port;
    public void Register(NativeModuleManifest manifest,INativeQueryModule module,string productVersion){
        if(this.listener is not null){throw new InvalidOperationException("Register query modules before starting the server.");}
        var runtimeVersion=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??throw new InvalidDataException("Runtime package version is unavailable.");
        if(runtimeVersion!=productVersion){throw new InvalidDataException("The supplied product version does not match the installed runtime package.");}
        if(manifest.ProductVersion!=productVersion){throw new InvalidDataException("Query module product version does not match the server.");}
        foreach(var query in manifest.Queries){var database=this.databases.Resolve(query.Database);if(database.SchemaFingerprint!=query.SchemaFingerprint){throw new InvalidDataException($"Query module schema does not match '{query.Database}'.");}}
        if(!this.modules.TryAdd(manifest.ModuleId,(manifest,module))){throw new InvalidDataException("Duplicate native module ID.");}
    }
    public void Load(string manifestPath,string productVersion){
        var root=Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var manifest=JsonSerializer.Deserialize<NativeModuleManifest>(File.ReadAllText(manifestPath))??throw new InvalidDataException("Query manifest is empty.");
        var assemblyPath=Path.GetFullPath(Path.Combine(root,manifest.AssemblyPath));
        if(!assemblyPath.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Query module path escapes its deployment directory.");}
        using(var input=File.OpenRead(assemblyPath)){var actual=Convert.ToHexString(SHA256.HashData(input));if(!actual.Equals(manifest.AssemblySha256,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("Query module hash does not match its manifest.");}}
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var module=Activator.CreateInstance(assembly.GetType(manifest.EntryType,true)!) as INativeQueryModule??throw new InvalidDataException("Query module entry does not implement INativeQueryModule.");
        this.Register(manifest,module,productVersion);
    }
    public void Start(){
        if(this.listener is not null){return;}
        this.stop=new();this.listener=new TcpListener(IPAddress.Loopback,this.port);this.listener.Start();this.accepting=this.AcceptAsync(this.stop.Token);
    }
    public async Task StopAsync(){
        if(this.listener is null){return;}
        this.stop!.Cancel();this.listener.Stop();
        foreach(var pair in this.connections.Values){pair.Client.Dispose();}
        if(this.accepting is not null){try{await this.accepting.ConfigureAwait(false);}catch(OperationCanceledException){}catch(SocketException){}}
        await Task.WhenAll(this.connections.Values.Select(v=>v.Task)).ConfigureAwait(false);
        this.listener=null;this.stop.Dispose();this.stop=null;
    }
    private async Task AcceptAsync(CancellationToken token){
        while(!token.IsCancellationRequested){
            var client=await this.listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);client.NoDelay=true;var id=Interlocked.Increment(ref this.nextConnection);
            var task=this.ConnectionAsync(client,token);this.connections[id]=(client,task);
            _=task.ContinueWith(completed=>{this.connections.TryRemove(id,out _);},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        }
    }
    private async Task ConnectionAsync(TcpClient client,CancellationToken stop){
        using(client)
        using(var lifetime=CancellationTokenSource.CreateLinkedTokenSource(stop)){
            var stream=client.GetStream();var input=Channel.CreateBounded<NativeQueryRequest>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true});
            var receive=ReceiveAsync();DatabaseTransaction? transaction=null;string? transactionDatabase=null;Guid transactionId=default;
            try{
                await foreach(var request in input.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false)){
                    try{
                        var database=this.databases.Resolve(request.Database);
                        if(database.Adapter is not InProcessDatabaseAdapter adapter){throw new NotSupportedException("The database does not expose typed native operations.");}
                        if(request.ModuleId=="$transaction"){
                            if(database.IsReadOnly){throw new InvalidOperationException("The comparison database is read only.");}
                            if(request.QueryId=="begin"){
                                if(transaction is not null||request.TransactionId==Guid.Empty){throw new InvalidOperationException("Invalid or nested native transaction.");}
                                transaction=adapter.Database.BeginTransaction();transactionDatabase=database.Name;transactionId=request.TransactionId;
                            }else{
                                if(transaction is null||transactionId!=request.TransactionId||transactionDatabase!=database.Name){throw new InvalidOperationException("The native transaction is not active on this connection.");}
                                if(request.QueryId=="commit"){transaction.Commit();}else if(request.QueryId!="rollback"){throw new NotSupportedException("Unknown transaction operation.");}
                                transaction.Dispose();transaction=null;transactionDatabase=null;transactionId=default;
                            }
                        }else{
                            if(request.TransactionId!=transactionId||transaction is not null&&transactionDatabase!=database.Name){throw new InvalidOperationException("The native request transaction does not match the connection.");}
                            if(!this.modules.TryGetValue(request.ModuleId,out var module)){throw new NativeQueryException("The native query module is not installed.");}
                            var definition=module.Manifest.Queries.SingleOrDefault(q=>q.Id==request.QueryId&&q.Database==database.Name)??throw new NativeQueryException("The native query is not registered for this database.");
                            if(definition.SchemaFingerprint!=database.SchemaFingerprint||request.SchemaFingerprint!=database.SchemaFingerprint){throw new NativeQueryException("The native query schema fingerprint is stale.");}
                            if(database.IsReadOnly&&!definition.IsReadOnly){throw new NativeQueryException("The comparison database is read only.");}
                            IDisposable? read=null;DatabaseTransaction? atomic=null;
                            try{
                                if(transaction is null){if(definition.IsReadOnly){read=adapter.Database.BeginReadSession();}else{atomic=adapter.Database.BeginTransaction();}}
                                var context=new NativeContext(adapter.Database,database.Name,lifetime.Token);
                                var rows=module.Module.Execute(request.QueryId,request.Arguments,context);
                                if(atomic is not null){
                                    using var scope=new ExecutionScope(new(){CancellationToken=lifetime.Token});using var buffered=scope.Query(rows).ToStorage();
                                    atomic.Commit();atomic.Dispose();atomic=null;
                                    foreach(var row in buffered){await NativeFrame.WriteAsync(stream,NativeFrame.Row,row,lifetime.Token).ConfigureAwait(false);}
                                }else{foreach(var row in rows){lifetime.Token.ThrowIfCancellationRequested();await NativeFrame.WriteAsync(stream,NativeFrame.Row,row,lifetime.Token).ConfigureAwait(false);}}
                            }finally{read?.Dispose();atomic?.Dispose();}
                        }
                        await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,lifetime.Token).ConfigureAwait(false);
                    }catch(Exception ex) when(!lifetime.IsCancellationRequested){
                        var message=Encoding.UTF8.GetBytes(ex.Message);if(message.Length>32768){message=message[..32768];}
                        await NativeFrame.WriteAsync(stream,NativeFrame.Error,message,lifetime.Token).ConfigureAwait(false);
                    }
                }
            }catch(OperationCanceledException) when(lifetime.IsCancellationRequested){}catch(IOException){}catch(SocketException){}
            finally{
                lifetime.Cancel();transaction?.Dispose();try{await receive.ConfigureAwait(false);}catch(OperationCanceledException){}catch(IOException){}catch(SocketException){}
            }
            async Task ReceiveAsync(){
                try{
                    while(!lifetime.IsCancellationRequested){
                        var frame=await NativeFrame.ReadAsync(stream,lifetime.Token).ConfigureAwait(false);if(frame is null){break;}if(frame.Value.Kind!=NativeFrame.Request){throw new InvalidDataException("Expected a native query request.");}
                        var request=MemoryPackSerializer.Deserialize<NativeQueryRequest>(frame.Value.Payload)??throw new InvalidDataException("Native request is empty.");await input.Writer.WriteAsync(request,lifetime.Token).ConfigureAwait(false);
                    }
                    input.Writer.TryComplete();lifetime.Cancel();
                }catch(Exception ex){input.Writer.TryComplete(ex);lifetime.Cancel();}
            }
        }
    }
    private sealed record NativeContext(object Database,string DatabaseName,CancellationToken CancellationToken):INativeQueryContext;
    public void Dispose()=>this.StopAsync().GetAwaiter().GetResult();
}
