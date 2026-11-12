using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Collections.Persisted;
using Seamless.Database.Client;
using Seamless.Execution;

internal sealed partial class Program{
    private static async Task CompareClientResultSocket(string root,string report,string sourceHash,int count,string mode){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||count is not(4096 or 16384)
            ||mode is not("Stream" or "TypedMemory" or "Persisted" or "RawMemory" or "Adaptive")){
            throw new ArgumentException("Invalid socket result comparison profile.");
        }
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Use a fresh private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var (expected,wire)=await CreateClientResultInput(count);
        var validator=NativeWireValidation.For<ClientResultRow>();
        var samples=new List<object>();
        for(var repeat=-2;repeat<9;repeat++){
            var budget=new ResourceBudget(32L<<20,16L<<20);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            var server=SendComparisonWire(listener,wire,timeout.Token);
            var sent=false;
            try{
                using var scope=new ExecutionScope(new(){
                    ParentBudget=budget,MemoryLimitBytes=budget.MemoryLimitBytes,TemporaryStorageLimitBytes=budget.TemporaryStorageLimitBytes,TemporaryDirectory=root
                });
                await using var client=new NativeQueryClient(IPAddress.Loopback.ToString(),((IPEndPoint)listener.LocalEndpoint).Port);
                var allocated=GC.GetTotalAllocatedBytes(true);
                var start=Stopwatch.GetTimestamp();
                using var memory=mode=="TypedMemory"?new QueryResultBuffer<ClientResultRow>(scope):null;
                using var persisted=mode=="Persisted"?new ScopedStoredSequence<ClientResultRow>(scope):null;
                using var raw=mode is "RawMemory" or "Adaptive"?new ClientResultSpool<ClientResultRow>(scope,mode=="Adaptive"?128<<10:8<<20):null;
                if(raw is not null){
                    await ReceiveClientResult(client,null,validator,raw,null,timeout.Token);
                    if(raw.Spilled!=(mode=="Adaptive")){throw new InvalidDataException("Unexpected raw result storage.");}
                }else{
                    var index=0;
                    await foreach(var row in client.QueryAsync<int,ClientResultRow>("fixture","rows","fixture","v1",0,timeout.Token)){
                        if(memory is not null){memory.Add(row);}
                        else if(persisted is not null){persisted.Append(row);}
                        else{ValidateClientRow(expected,index,row);}
                        index++;
                    }
                    if(index!=count){throw new InvalidDataException("Unexpected socket result count.");}
                }
                var typedRows=memory?.ToArray();
                persisted?.Seal();
                var elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var receiveAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
                await client.DisposeAsync();
                var replay=default(ClientResultReplay?);
                if(mode!="Stream"){
                    var rows=typedRows??(IEnumerable<ClientResultRow>?)persisted??raw!.ReadRows();
                    replay=MeasureClientReplay(rows,expected);
                    _=MeasureClientReplay(rows,expected);
                }
                if(repeat>=0){
                    samples.Add(new{Repeat=repeat,ReceiveMs=elapsed,ReceiveAllocatedBytes=receiveAllocated,Replay=replay,
                        PeakReservedMemoryBytes=budget.PeakMemoryBytes,PeakTemporaryStorageBytes=budget.PeakTemporaryStorageBytes});
                }
                await server;
                sent=true;
            }finally{
                listener.Stop();
                timeout.Cancel();
                if(!sent){
                    try{await server;}
                    catch(OperationCanceledException)when(timeout.IsCancellationRequested){}
                    catch(IOException){}
                    catch(SocketException){}
                }
            }
            if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0||Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any()){
                throw new InvalidDataException("Client result resources leaked.");
            }
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
            Format="client-result-socket-comparison-v1",Rows=count,Mode=mode,Affinity=ComparisonAffinity(),WireBytes=wire.LongLength,
            SourceManifestSha256=sourceHash,AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
            Samples=samples
        },new JsonSerializerOptions{WriteIndented=true}));
    }

    private static async Task SendComparisonWire(TcpListener listener,byte[] wire,CancellationToken cancellationToken){
        using var socket=await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream=socket.GetStream();
        await VerifyClientRequest(stream,cancellationToken);
        // 送信を事前に直列化して、同じデータと受信処理の差を比較する。
        await stream.WriteAsync(wire,cancellationToken);
    }
}
