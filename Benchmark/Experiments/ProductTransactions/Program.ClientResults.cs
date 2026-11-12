using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MemoryPack;
using ProductTransactions;
using Seamless.Collections.Persisted;
using Seamless.Collections.Tsavorite;
using Seamless.Database.Client;
using Seamless.Execution;
using ExecutionStore=Seamless.Execution.Store;

internal sealed partial class Program{
    private static async Task CompareClientResults(string root,string report,string sourceHash,int count,string mode){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||count is not(4096 or 16384)
            ||mode is not("Stream" or "Memory" or "Persisted" or "Raw" or "RawBuffered" or "RawValidated" or "Adaptive" or "TsavoriteMemory" or "TsavoriteTemporary")){
            throw new ArgumentException("Invalid client result comparison profile.");
        }
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Use a fresh private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var (expected,wire)=await CreateClientResultInput(count);
        var samples=new List<object>();
        var validator=default(NativeWireValidator);
        if(mode is "RawValidated" or "Adaptive"){
            validator=NativeWireValidation.For<ClientResultRow>();
            var firstRow=MemoryPackSerializer.Serialize(expected[0]);
            try{
                validator.Validate(firstRow.AsSpan(0,firstRow.Length-1));
                throw new InvalidOperationException("The wire validator accepted a truncated result row.");
            }catch(InvalidDataException){
            }
        }
        for(var repeat=-2;repeat<10;repeat++){
            var budget=new ResourceBudget(32L<<20,16L<<20);
            using(var scope=new ExecutionScope(new(){ParentBudget=budget,MemoryLimitBytes=32L<<20,TemporaryStorageLimitBytes=16L<<20,TemporaryDirectory=root})){
                using var source=new MemoryStream(wire,writable:false);
                var start=Stopwatch.GetTimestamp();var allocated=GC.GetTotalAllocatedBytes(true);
                using var memory=mode=="Memory"?new QueryResultBuffer<ClientResultRow>(scope):null;
                using var persisted=mode=="Persisted"?new ScopedStoredSequence<ClientResultRow>(scope):null;
                using var spool=mode=="Adaptive"?new ClientResultSpool<ClientResultRow>(scope,128<<10):null;
                var limits=new StoreLimits{MemoryBytes=budget.MemoryLimitBytes,DiskBytes=budget.TemporaryStorageLimitBytes};
                using var tsavoriteOwner=mode switch{
                    "TsavoriteMemory"=>ExecutionStore.CreateMemory(limits,parentBudget:budget),
                    "TsavoriteTemporary"=>ExecutionStore.CreateTemporary(limits,root,parentBudget:budget),
                    _=>null
                };
                var tsavoriteStore=tsavoriteOwner?.CreateTsavorite<int,ClientResultRow>("ordinal",new TsavoriteTuning{
                    IndexSize=count*32L,PageSize=16<<10,SegmentSize=256<<10,LogMemorySize=mode=="TsavoriteMemory"?4L<<20:256L<<10
                });
                // MemoryStream の受信は同期完了するため、辞書アクセサのスレッド所有権を維持できる。
                using var tsavorite=tsavoriteStore?.Create();
                using var rawMemory=mode is "RawBuffered" or "RawValidated"?scope.Budget.ReserveMemory(64<<10):null;
                using Stream? raw=mode switch{
                    "Raw"=>scope.CreateTemporaryFile(),
                    "RawBuffered" or "RawValidated"=>new BufferedStream(scope.CreateTemporaryFile(),64<<10),
                    _=>null
                };
                var index=0;
                while(true){
                    var frame=await NativeFrame.ReadAsync(source)??throw new EndOfStreamException("Completed frame is required.");
                    if(frame.Kind==NativeFrame.Completed){break;}
                    if(frame.Kind!=NativeFrame.Row){throw new InvalidDataException("Unexpected frame kind.");}
                    if(spool is not null){
                        validator!.Validate(frame.Payload);
                        spool.Append(frame.Payload);
                    }else if(raw is not null){
                        validator?.Validate(frame.Payload);
                        AppendClientPayload(raw,frame.Payload);
                    }else{
                        var row=MemoryPackSerializer.Deserialize<ClientResultRow>(frame.Payload);
                        if(memory is not null){memory.Add(row);}
                        else if(persisted is not null){persisted.Append(row);}
                        else if(tsavorite is not null){tsavorite.Add(index,row);}
                        else{ValidateClientRow(expected,index,row);}
                    }
                    index++;
                }
                if(index!=count){throw new InvalidDataException("Unexpected result count.");}
                var memoryRows=memory?.ToArray();
                persisted?.Seal();
                raw?.Flush();
                spool?.Complete();
                if(spool is {Spilled:false}){throw new InvalidDataException("The fixture must switch to file storage.");}
                var receiveMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var receiveAllocated=GC.GetTotalAllocatedBytes(true)-allocated;
                source.Dispose();
                var replay=default(ClientResultReplay?);
                if(mode!="Stream"){
                    IEnumerable<ClientResultRow> rows=memoryRows??(IEnumerable<ClientResultRow>?)persisted
                        ??spool?.ReadRows()??(tsavorite is not null?ReadClientTsavorite(tsavorite,index):ReadClientPayloads(scope,raw!));
                    replay=MeasureClientReplay(rows,expected);
                    // 接続から受け取った行順のまま、結果をもう一度最初から読めることを確認する。
                    _=MeasureClientReplay(rows,expected);
                }
                if(repeat>=0){
                    samples.Add(new{Repeat=repeat,ReceiveMs=receiveMs,ReceiveAllocatedBytes=receiveAllocated,Replay=replay,
                        PeakReservedMemoryBytes=budget.PeakMemoryBytes,PeakTemporaryStorageBytes=budget.PeakTemporaryStorageBytes});
                }
            }
            if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0){throw new InvalidDataException("Client result resources leaked.");}
        }
        if(Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any()){throw new InvalidDataException("Temporary result files leaked.");}
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
            Format="client-result-comparison-v1",Rows=count,Mode=mode,Affinity=ComparisonAffinity(),WireBytes=wire.LongLength,
            SourceManifestSha256=sourceHash,AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
            Measurement="NativeFrame over prebuilt MemoryStream; receive includes decode/consume for Stream, replay excludes connection",
            Records=samples
        },new JsonSerializerOptions{WriteIndented=true}));
    }

    private static IEnumerable<ClientResultRow> ReadClientTsavorite(Seamless.Collections.Tsavorite.IDictionary<int,ClientResultRow> rows,int count){
        // 行の業務キーやハッシュ表の列挙順に依存せず、受信時の序数を引く。
        for(var ordinal=0;ordinal<count;ordinal++){
            if(!rows.TryGetValue(ordinal,out var row)){throw new InvalidDataException("Missing result ordinal.");}
            yield return row;
        }
    }
    private static async Task<(ClientResultRow[] Rows,byte[] Wire)> CreateClientResultInput(int count){
        var expected=new ClientResultRow[count];
        using var source=new MemoryStream();
        for(var index=0;index<count;index++){
            var id=(index*4051)&(count-1);
            var row=new ClientResultRow(id,InitialBalance(id),new string((char)('a'+id%26),32+id%64));
            expected[index]=row;
            await NativeFrame.WriteAsync(source,NativeFrame.Row,MemoryPackSerializer.Serialize(row));
        }
        await NativeFrame.WriteAsync(source,NativeFrame.Completed,ReadOnlyMemory<byte>.Empty);
        return(expected,source.ToArray());
    }

    private static void AppendClientPayload(Stream raw,byte[] payload){
        Span<byte> prefix=stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix,payload.Length);
        raw.Write(prefix);
        raw.Write(payload);
    }
    private static IEnumerable<ClientResultRow> ReadClientPayloads(ExecutionScope scope,Stream raw){
        raw.Position=0;
        while(raw.Position<raw.Length){
            Span<byte> prefix=stackalloc byte[4];
            raw.ReadExactly(prefix);
            var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if(length<0||length>=NativeFrame.MaximumBytes||length>raw.Length-raw.Position){throw new InvalidDataException("Invalid stored result frame.");}
            ClientResultRow row;
            using(var buffer=scope.Rent(length)){
                raw.ReadExactly(buffer.Memory.Span);
                row=MemoryPackSerializer.Deserialize<ClientResultRow>(buffer.Memory.Span);
            }
            yield return row;
        }
    }
    private static ClientResultReplay MeasureClientReplay(IEnumerable<ClientResultRow> rows,ClientResultRow[] expected){
        var allocated=GC.GetTotalAllocatedBytes(true);var start=Stopwatch.GetTimestamp();
        var first=0.0;var index=0;
        foreach(var row in rows){
            if(index==0){first=Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
            ValidateClientRow(expected,index++,row);
        }
        if(index!=expected.Length){throw new InvalidDataException("Client replay count differs.");}
        return new(first,Stopwatch.GetElapsedTime(start).TotalMilliseconds,GC.GetTotalAllocatedBytes(true)-allocated);
    }
    private static void ValidateClientRow(ClientResultRow[] expected,int index,ClientResultRow row){
        if((uint)index>=(uint)expected.Length||row!=expected[index]){throw new InvalidDataException("Client result order or value differs.");}
    }
    private readonly record struct ClientResultReplay(double FirstRowMs,double Milliseconds,long AllocatedBytes);
}

namespace ProductTransactions{
    [MemoryPackable]
    internal readonly partial record struct ClientResultRow(int Id,long Balance,string Label);
}
