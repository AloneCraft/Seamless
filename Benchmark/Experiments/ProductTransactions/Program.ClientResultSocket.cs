using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MemoryPack;
using ProductTransactions;
using Seamless.Database.Client;
using Seamless.Execution;

internal sealed partial class Program{
    private static async Task VerifyClientResultSocket(string root,string report,string sourceHash){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"){throw new ArgumentException("Use F for result files and evidence.");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Use a fresh private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var rows=Enumerable.Range(0,256)
            .Select(static id=>new ClientResultRow(id,InitialBalance(id),new string((char)('a'+id%26),512))).ToArray();
        var payloads=rows.Select(static row=>MemoryPackSerializer.Serialize(row)).ToArray();
        var validator=NativeWireValidation.For<ClientResultRow>();
        var results=new List<object>();
        foreach(var mode in new[]{"ClientStream","Memory","Spill","TightReplay","TightSpill","Disconnect","Cancel","Malformed","StorageLimit","Recoverable","Transaction"}){
            var budget=new ResourceBudget(mode is "TightReplay" or "TightSpill"?(64<<10)+64:1<<20,mode=="StorageLimit"?8<<10:2<<20);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var receiveStop=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            var server=SendClientResult(listener,payloads,mode,deadline.Token);
            var spillReached=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var complete=mode is "ClientStream" or "Memory" or "Spill" or "TightReplay" or "TightSpill" or "Transaction";
            var spilled=false;
            Exception? observed=null;
            try{
                var port=((IPEndPoint)listener.LocalEndpoint).Port;
                await using var client=new NativeQueryClient(IPAddress.Loopback.ToString(),port);
                if(mode=="ClientStream"){
                    await VerifyTypedClientStream(client,rows,receiveStop.Token);
                }else{
                    await using var transaction=mode=="Transaction"?await client.BeginTransactionAsync("fixture",receiveStop.Token):null;
                    using var scope=new ExecutionScope(new(){
                        ParentBudget=budget,MemoryLimitBytes=budget.MemoryLimitBytes,TemporaryStorageLimitBytes=budget.TemporaryStorageLimitBytes,TemporaryDirectory=root
                    });
                    var threshold=mode switch{"Memory"=>512<<10,"TightReplay"=>0,_=>32<<10};
                    using var result=new ClientResultSpool<ClientResultRow>(scope,threshold);
                    var receive=ReceiveClientResult(client,transaction,validator,result,spillReached,receiveStop.Token);
                    if(mode=="Cancel"){
                        await spillReached.Task.WaitAsync(deadline.Token);
                        receiveStop.Cancel();
                    }
                    if(mode=="Recoverable"){
                        try{await receive;}
                        catch(NativeQueryException error)when(error.IsRecoverable){observed=error;}
                        await VerifyTypedClientStream(client,rows,receiveStop.Token);
                    }else{
                        await receive;
                    }
                    if(transaction is not null){await transaction.CommitAsync(receiveStop.Token);}
                    if(complete){await client.DisposeAsync();}
                    spilled=result.Spilled;
                    if(complete){
                        if((mode!="Memory")!=spilled){throw new InvalidDataException("Stored result storage mode differs.");}
                        VerifyClientResultReplay(result,rows,budget);
                    }
                }
            }catch(Exception error)when(!complete){observed=error;}
            finally{
                deadline.Cancel();
                listener.Stop();
                try{await server;}
                catch(OperationCanceledException)when(!complete&&deadline.IsCancellationRequested){}
                catch(IOException)when(!complete){}
                catch(SocketException)when(!complete){}
            }
            var expected=mode switch{
                "Disconnect"=>observed is EndOfStreamException,
                "Cancel"=>observed is OperationCanceledException&&receiveStop.IsCancellationRequested&&spillReached.Task.IsCompletedSuccessfully,
                "Malformed"=>observed is InvalidDataException,
                "StorageLimit"=>observed is ResourceLimitException,
                "Recoverable"=>observed is NativeQueryException{IsRecoverable:true},
                _=>observed is null
            };
            if(!expected){throw new InvalidDataException("Unexpected socket outcome for "+mode,observed);}
            if(mode is not("ClientStream" or "Memory")&&!spillReached.Task.IsCompletedSuccessfully){throw new InvalidDataException("The case must enter file storage: "+mode);}
            if(mode=="Cancel"&&budget.PeakTemporaryStorageBytes!=0){throw new InvalidDataException("Pending rows must be discarded on cancellation.");}
            if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0||Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any()){
                throw new InvalidDataException("Socket result resources leaked.");
            }
            results.Add(new{Mode=mode,Complete=complete,Spilled=spilled||spillReached.Task.IsCompletedSuccessfully,
                Error=observed?.GetType().Name,MemoryPeak=budget.PeakMemoryBytes,StoragePeak=budget.PeakTemporaryStorageBytes});
        }
        foreach(var file in new[]{false,true}){
            results.Add(await VerifySharedClientResultBudget(root,rows,payloads,validator,file));
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
            Format="client-result-socket-v5",Affinity=ComparisonAffinity(),SourceManifestSha256=sourceHash,
            AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
            Cases=results
        },new JsonSerializerOptions{WriteIndented=true}));
    }

    private static async Task<object> VerifySharedClientResultBudget(string root,ClientResultRow[] rows,byte[][] payloads,NativeWireValidator validator,bool file){
        var budget=new ResourceBudget(file?512<<10:33<<10,160<<10);
        var options=new ExecutionOptions{ParentBudget=budget,MemoryLimitBytes=256<<10,TemporaryStorageLimitBytes=160<<10,TemporaryDirectory=root};
        var threshold=file?0:128<<10;
        var count=file?144:24;
        var expected=rows[..count];
        var firstPayloads=payloads[..count];
        if(file){
            expected[0]=expected[0] with{Label=new string('界',24<<10)};
            firstPayloads[0]=MemoryPackSerializer.Serialize(expected[0]);
            if(firstPayloads[0].Length<=64<<10){throw new InvalidDataException("Expected a row larger than the file buffer.");}
        }
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var cancellationToken=deadline.Token;
        using var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var scope=new ExecutionScope(options);
        using var result=new ClientResultSpool<ClientResultRow>(scope,threshold);
        await using var first=new NativeQueryClient(IPAddress.Loopback.ToString(),port);
        await using var second=new NativeQueryClient(IPAddress.Loopback.ToString(),port);
        var accepted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstServer=SendClientResult(listener,firstPayloads,"SharedBudgetSuccess",cancellationToken,finish.Task);
        var remaining=count;
        var firstReceive=ReceiveClientResult(first,null,validator,result,null,cancellationToken,()=>{
            if(--remaining==0){accepted.TrySetResult();}
        });
        try{
            await accepted.Task.WaitAsync(cancellationToken);
            var held=(budget.MemoryBytes,budget.TemporaryStorageBytes);
            var failedServer=SendClientResult(listener,payloads,"SharedBudgetFailure",cancellationToken);
            try{
                using var failedScope=new ExecutionScope(options);
                using var failed=new ClientResultSpool<ClientResultRow>(failedScope,threshold);
                await ReceiveClientResult(second,null,validator,failed,null,cancellationToken);
                throw new InvalidDataException("The shared budget accepted both results.");
            }catch(ResourceLimitException){}
            await failedServer;
            if(firstReceive.IsCompleted||held!=(budget.MemoryBytes,budget.TemporaryStorageBytes)){
                throw new InvalidDataException("The failing result affected the active receiver.");
            }
            var retryServer=SendClientResult(listener,payloads[..1],"Memory",cancellationToken);
            await VerifyTypedClientStream(second,rows[..1],cancellationToken);
            await retryServer;
            finish.SetResult();
            await firstReceive;
            await firstServer;
            await first.DisposeAsync();
            if(result.Spilled!=file){throw new InvalidDataException("Stored result storage mode differs.");}
            VerifyClientResultReplay(result,expected,budget);
            scope.Dispose();
            if(Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any()){
                throw new InvalidDataException("Shared-budget result files leaked.");
            }
            return new{Mode=file?"SharedFileBudget":"SharedMemoryBudget",Complete=true,Spilled=file,
                MemoryPeak=budget.PeakMemoryBytes,StoragePeak=budget.PeakTemporaryStorageBytes};
        }finally{
            deadline.Cancel();
            try{await Task.WhenAll(firstReceive,firstServer);}
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){}
        }
    }

    private static void VerifyClientResultReplay(ClientResultSpool<ClientResultRow> result,ClientResultRow[] expected,ResourceBudget budget){
        var rows=result.ReadRows();
        using(var first=rows.GetEnumerator()){
            if(!first.MoveNext()||first.Current!=expected[0]){throw new InvalidDataException("The first reader did not start at the beginning.");}
            using var overlap=rows.GetEnumerator();
            try{
                overlap.MoveNext();
                throw new InvalidDataException("Two result readers became active.");
            }catch(InvalidOperationException error)when(error.GetType()==typeof(InvalidOperationException)){}
            if(!first.MoveNext()||first.Current!=expected[1]){throw new InvalidDataException("The rejected reader moved the first reader.");}
        }
        if(!expected.SequenceEqual(rows)||!expected.SequenceEqual(rows)){throw new InvalidDataException("Stored result order or value differs.");}
        using var stopped=rows.GetEnumerator();
        if(!stopped.MoveNext()){throw new InvalidDataException("The result unexpectedly ended.");}
        result.Dispose();
        if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0){throw new InvalidDataException("A stopped reader retained resources after result disposal.");}
        try{
            stopped.MoveNext();
            throw new InvalidDataException("A disposed result still allowed reading.");
        }catch(ObjectDisposedException){}
    }

    private static async Task VerifyTypedClientStream(NativeQueryClient client,ClientResultRow[] rows,CancellationToken cancellationToken){
        var index=0;
        await foreach(var row in client.QueryAsync<int,ClientResultRow>("fixture","rows","fixture","v1",0,cancellationToken)){
            ValidateClientRow(rows,index++,row);
        }
        if(index!=rows.Length){throw new InvalidDataException("Typed client row count differs.");}
    }

    private static async Task ReceiveClientResult(NativeQueryClient client,NativeTransaction? transaction,NativeWireValidator validator,ClientResultSpool<ClientResultRow> result,
        TaskCompletionSource? spillReached,CancellationToken cancellationToken,Action? accepted=null){
        // 試験だけで内部の受信経路を呼び、製品に公開APIや実験アセンブリへの依存を追加しない。
        var method=typeof(NativeQueryClient).GetMethod("QueryProjectedAsync",BindingFlags.Instance|BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(int),typeof(ClientResultRow),typeof(bool));
        var query=method.CreateDelegate<Func<string,string,string,string,int,NativeTransaction?,Func<ReadOnlySpan<byte>,bool>,CancellationToken,IAsyncEnumerable<bool>>>(client);
        var response=query("fixture","rows","fixture","v1",0,transaction,payload=>{
            validator.Validate(payload);
            result.Append(payload);
            if(result.Spilled){spillReached?.TrySetResult();}
            accepted?.Invoke();
            return true;
        },cancellationToken);
        await foreach(var _ in response){}
        result.Complete();
    }

    private static async Task SendClientResult(TcpListener listener,byte[][] payloads,string mode,CancellationToken cancellationToken,Task? beforeComplete=null){
        using var socket=await listener.AcceptTcpClientAsync(cancellationToken);
        socket.NoDelay=true;
        using var stream=socket.GetStream();
        var transactionId=Guid.Empty;
        if(mode=="Transaction"){
            var begin=await ReadClientRequest(stream,cancellationToken);
            if(begin.ModuleId!="$transaction"||begin.QueryId!="begin"||begin.TransactionId==Guid.Empty){throw new InvalidDataException("Transaction begin required.");}
            transactionId=begin.TransactionId;
            await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellationToken);
        }
        await VerifyClientRequest(stream,cancellationToken,transactionId);
        var limit=payloads.Length;
        if(mode is "Disconnect" or "Malformed" or "Recoverable"){limit=128;}
        if(mode=="Cancel"){limit=40;}
        try{
            for(var index=0;index<limit;index++){
                await NativeFrame.WriteAsync(stream,NativeFrame.Row,payloads[index],cancellationToken);
            }
            if(mode=="SharedBudgetFailure"){
                if(await stream.ReadAsync(new byte[1],cancellationToken)!=0){throw new InvalidDataException("The failed connection remained open.");}
                return;
            }
        }catch(IOException error)when(mode=="SharedBudgetFailure"&&error.InnerException is SocketException
            {SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown}){return;}
        if(mode=="Cancel"){
            await Task.Delay(Timeout.Infinite,cancellationToken);
        }else if(mode=="Malformed"){
            await NativeFrame.WriteAsync(stream,NativeFrame.Row,payloads[0].AsMemory(0,payloads[0].Length-1),cancellationToken);
        }else if(mode=="Recoverable"){
            await NativeFrame.WriteAsync(stream,NativeFrame.RecoverableError,"recoverable"u8.ToArray(),cancellationToken);
            await VerifyClientRequest(stream,cancellationToken);
            foreach(var payload in payloads){await NativeFrame.WriteAsync(stream,NativeFrame.Row,payload,cancellationToken);}
            await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellationToken);
        }else if(mode!="Disconnect"){
            if(beforeComplete is not null){await beforeComplete.WaitAsync(cancellationToken);}
            await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellationToken);
            if(mode=="Transaction"){
                var commit=await ReadClientRequest(stream,cancellationToken);
                if(commit.ModuleId!="$transaction"||commit.QueryId!="commit"||commit.TransactionId!=transactionId){throw new InvalidDataException("Transaction commit required.");}
                await NativeFrame.WriteAsync(stream,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty,cancellationToken);
            }
        }
    }

    private static async Task VerifyClientRequest(Stream stream,CancellationToken cancellationToken,Guid transactionId=default){
        var request=await ReadClientRequest(stream,cancellationToken);
        if(request is not{ModuleId:"fixture",QueryId:"rows",Database:"fixture",SchemaFingerprint:"v1"}
            ||request.TransactionId!=transactionId||request.ArgumentsContract!=NativeWireContract.For<int>()
            ||request.ResultContract!=NativeWireContract.For<ClientResultRow>()||MemoryPackSerializer.Deserialize<int>(request.Arguments)!=0){
            throw new InvalidDataException("Query request contract differs.");
        }
    }

    private static async Task<NativeQueryRequest> ReadClientRequest(Stream stream,CancellationToken cancellationToken){
        var frame=await NativeFrame.ReadAsync(stream,cancellationToken)??throw new EndOfStreamException();
        if(frame.Kind!=NativeFrame.Request){throw new InvalidDataException("A query request is required.");}
        return MemoryPackSerializer.Deserialize<NativeQueryRequest>(frame.Payload)??throw new InvalidDataException("Request missing.");
    }
}
