using System.Security.Cryptography;
using System.Text.Json;
using MemoryPack;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Execution.Growth;

namespace Seamless.Management.Server;

[MemoryPackable]internal sealed partial record ResourceHeatChunk(string Codec,Guid ExecutionId,int Index,int Count,int TotalBytes,byte[] Hash,byte[] Data);
internal sealed record ResourceHeatHistoryOptions(int MaximumPending=4,long MaximumPendingBytes=8L<<20,int MaximumPeriods=256,long MaximumDiskBytes=64L<<20);

/// <summary>資源履歴は1実行の全chunkを1期間で公開する。DB取引へ再入せず、非待機で受け付ける。</summary>
internal sealed class ResourceHeatHistoryStore:IAsyncDisposable{
    private const int ChunkBytes=2048,MaximumBytes=1<<20;
    private const string Codec="ResourceHeatV1Json1";
    private static readonly TsavoriteTuning Tuning=new(){IndexSize=4096,PageSize=4096,SegmentSize=16384,LogMemorySize=8192,Growth=new Policy<Fixed>(),Storage=new(FileAccess.ReadWrite,StorageAccessPattern.Random,StorageBufferingMode.Buffered)};
    private sealed record Pending(byte[] Bytes,Guid ExecutionId,ResourceReservation Memory);
    private sealed class PayloadLimitException():IOException("資源記録の保存上限を超えています。");
    private sealed class PayloadStream:MemoryStream{
        public override void Write(byte[] buffer,int offset,int count){if(count>MaximumBytes-this.Position){throw new PayloadLimitException();}base.Write(buffer,offset,count);}
        public override void Write(ReadOnlySpan<byte> buffer){if(buffer.Length>MaximumBytes-this.Position){throw new PayloadLimitException();}base.Write(buffer);}
    }
    private readonly object gate=new(),periodGate=new();
    private readonly Queue<Pending> queue=new();
    private readonly SemaphoreSlim signal=new(0);
    private readonly ResourceBudget budget;
    private readonly ResourceHeatHistoryOptions options;
    private readonly ExecutionHistoryPeriods periods;
    private readonly string directory;
    private readonly Guid session=Guid.NewGuid();
    private readonly Task worker;
    private readonly Func<byte[],CancellationToken,Task>? writeOverride;
    private long sequence,captured,saved,dropped,failures,bytes;
    private int pending;
    private string? error;
    private bool closed;
    private Task? disposal;
    internal ResourceHeatHistoryStore(string directory,ResourceBudget budget,ResourceHeatHistoryOptions? options=null,Func<byte[],CancellationToken,Task>? writeOverride=null){
        this.directory=directory;this.budget=budget;this.options=options??new();this.writeOverride=writeOverride;
        if(this.options.MaximumPending<1||this.options.MaximumPendingBytes<MaximumBytes*2||this.options.MaximumPeriods is <1 or >4096||this.options.MaximumDiskBytes<MaximumBytes*2){throw new ArgumentException("資源履歴の保持上限が不正です。");}
        this.periods=new(directory,budget,"ResourceHeat",1024,Tuning);this.worker=Task.Run(this.WorkAsync);
    }
    internal ResourceHeatPersistence TryRecord(ResourceHeatTrace trace){
        ResourceReservation? memory=null;
        lock(this.gate){
            this.captured++;
            if(this.closed){this.dropped++;return new("Dropped","Closed");}
            const long reservation=2L*MaximumBytes;
            if(this.pending>=this.options.MaximumPending||reservation>this.options.MaximumPendingBytes-this.bytes){this.dropped++;this.error="BufferFull";return new("Dropped",this.error);}
            try{
                memory=this.budget.ReserveMemory(reservation);trace.Validate();
                using var stream=new PayloadStream();JsonSerializer.Serialize(stream,trace with{Persistence=null},ManagementJson.Options);var payload=stream.ToArray();
                this.queue.Enqueue(new(payload,trace.ExecutionId,memory));this.pending++;this.bytes+=reservation;this.signal.Release();return new("Queued");
            }catch(ResourceLimitException){memory?.Dispose();this.dropped++;this.error="MemoryBudget";return new("Dropped",this.error);}
            catch(PayloadLimitException){memory?.Dispose();this.dropped++;this.error="PayloadLimit";return new("Dropped",this.error);}
            catch(Exception exception)when(exception is InvalidDataException or JsonException or NotSupportedException){memory?.Dispose();this.dropped++;this.error="InvalidRecord";return new("Dropped",this.error);}
        }
    }
    internal ResourceHeatHistoryStatus Status(){lock(this.gate){return new(this.session,this.captured,this.saved,this.dropped,this.failures,this.bytes,this.pending,this.error,this.closed);}}
    private async Task WorkAsync(){
        while(true){
            await this.signal.WaitAsync().ConfigureAwait(false);Pending work;
            lock(this.gate){if(!this.queue.TryDequeue(out work!)){if(this.closed){return;}continue;}}
            try{
                if(this.writeOverride is{} write){await write(work.Bytes,CancellationToken.None).ConfigureAwait(false);}
                else{
                    this.periods.EnsureWritable();lock(this.periodGate){this.Reclaim(1);}
                    var hash=SHA256.HashData(work.Bytes);var count=(work.Bytes.Length+ChunkBytes-1)/ChunkBytes;var rows=new ExecutionHistoryRow[count];
                    for(var index=0;index<count;index++){
                        var offset=index*ChunkBytes;var length=Math.Min(ChunkBytes,work.Bytes.Length-offset);
                        var chunk=new ResourceHeatChunk(Codec,work.ExecutionId,index,count,work.Bytes.Length,hash,work.Bytes.AsSpan(offset,length).ToArray());
                        rows[index]=ExecutionHistoryPeriods.EncodePayload(++this.sequence,this.session,MemoryPackSerializer.Serialize(chunk));
                    }
                    this.periods.WriteRows(rows);
                }
                lock(this.gate){this.saved++;this.error=null;}
                try{lock(this.periodGate){this.Reclaim(0);}}catch(Exception){lock(this.gate){this.error="RetentionFailed";}}
            }catch(IncompleteExecutionHistoryPeriodException){this.Failed("IncompletePeriod");}
            catch(ResourceLimitException){this.Failed("StorageBudget");}
            catch(Exception){this.Failed("SaveFailed");}
            finally{lock(this.gate){this.pending--;this.bytes-=work.Memory.Bytes;work.Memory.Dispose();}}
        }
    }
    private void Failed(string reason){lock(this.gate){this.failures++;this.dropped++;this.error=reason;}}
    private void Reclaim(int reserved){
        if(!Directory.Exists(this.directory)){return;}
        var periods=new DirectoryInfo(this.directory).EnumerateDirectories("period-*").Take(4098).OrderBy(period=>period.Name,StringComparer.Ordinal).Select(period=>(Directory:period,Bytes:period.EnumerateFiles("*",SearchOption.AllDirectories).Sum(file=>file.Length))).ToArray();
        if(periods.Length>4097){throw new IOException("資源履歴の期間検査上限を超えています。");}
        var incomplete=Directory.EnumerateDirectories(this.directory,"writing-*").Sum(path=>Directory.EnumerateFiles(path,"*",SearchOption.AllDirectories).Sum(file=>new FileInfo(file).Length));
        var total=checked(periods.Sum(period=>period.Bytes)+incomplete);var count=periods.Length;
        foreach(var period in periods){
            if(count<=this.options.MaximumPeriods-reserved&&total<=this.options.MaximumDiskBytes){break;}
            var root=Path.GetFullPath(this.directory)+Path.DirectorySeparatorChar;var target=Path.GetFullPath(period.Directory.FullName);
            if(!target.StartsWith(root,StringComparison.Ordinal)||period.Directory.Attributes.HasFlag(FileAttributes.ReparsePoint)){throw new InvalidDataException("資源履歴の削除先が不正です。");}
            period.Directory.Delete(true);count--;total-=period.Bytes;
        }
        if(total>this.options.MaximumDiskBytes){throw new IOException("資源履歴の保持容量を確保できません。");}
    }
    internal Task<ResourceHeatReadResult> ReadAsync(ResourceHeatQuery query,CancellationToken cancellation)=>Task.Run(()=>{lock(this.periodGate){return this.Read(query,cancellation);}},cancellation);
    private ResourceHeatReadResult Read(ResourceHeatQuery query,CancellationToken cancellation){
        query.Validate();var invalid=0;var limited=false;var seen=false;long kept=0;ResourceHeatTrace? found=null;string? unavailable="NotRecordedOrRemoved";
        using var memory=this.budget.ReserveMemory(0);
        try{
            if(Directory.Exists(this.directory)){
                if(this.Status().PendingCount==0){invalid+=Directory.EnumerateDirectories(this.directory,"writing-*").Take(this.options.MaximumPeriods+1).Count();}
                var paths=Directory.EnumerateDirectories(this.directory,"period-*").Take(this.options.MaximumPeriods+1).ToArray();
                if(paths.Length>this.options.MaximumPeriods){limited=true;}
                foreach(var path in paths.Take(this.options.MaximumPeriods)){
                    cancellation.ThrowIfCancellationRequested();
                    try{
                        memory.Resize(kept+(8L<<20));var rows=this.periods.ReadRows(path,cancellation);
                        var chunks=rows.Select(row=>MemoryPackSerializer.Deserialize<ResourceHeatChunk>(row.Payload)??throw new InvalidDataException("資源chunkがありません。")).ToArray();
                        if(chunks.Length==0){throw new InvalidDataException("資源chunkが空です。");}
                        if(chunks[0].ExecutionId!=query.ExecutionId){continue;}
                        if(seen){return new(null,this.Status(),invalid+1,limited,"DuplicateExecution");}seen=true;
                        var trace=Decode(chunks);trace.Validate();
                        if(trace.ExecutionId!=query.ExecutionId||trace.DefinitionId!=query.DefinitionId||trace.ObjectId!=query.ObjectId||query.ExpectedVersion is{} version&&trace.Version!=version||query.ExpectedArtifactHash is{} artifact&&trace.ArtifactHash!=artifact){unavailable="IdentityMismatch";continue;}
                        found=trace with{Persistence=new("Saved")};unavailable=null;kept=4L<<20;
                    }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
                    catch(ResourceLimitException){limited=true;unavailable="ReadMemoryBudget";break;}
                    catch(Exception){invalid++;}
                    finally{memory.Resize(kept);}
                }
            }
        }catch(DirectoryNotFoundException){invalid++;}catch(IOException){invalid++;}catch(UnauthorizedAccessException){invalid++;}
        if(found is null&&invalid>0&&unavailable=="NotRecordedOrRemoved"){unavailable="InvalidPeriod";}
        if(limited){found=null;unavailable??="ScanLimit";}
        return new(found,this.Status(),invalid,limited,unavailable);
    }
    private static ResourceHeatTrace Decode(ResourceHeatChunk[] chunks){
        var first=chunks[0];
        if(first.Codec!=Codec||first.ExecutionId==Guid.Empty||first.Count!=chunks.Length||first.Count is <1 or >512||first.TotalBytes is <1 or >MaximumBytes||first.Hash is not{Length:32}){throw new InvalidDataException("資源chunkのヘッダーが不正です。");}
        var bytes=new byte[first.TotalBytes];var used=new System.Collections.Generic.HashSet<int>();
        foreach(var chunk in chunks){
            var data=chunk.Data;
            if(chunk.Codec!=Codec||chunk.ExecutionId!=first.ExecutionId||chunk.Count!=first.Count||chunk.TotalBytes!=first.TotalBytes||chunk.Index<0||chunk.Index>=first.Count||!used.Add(chunk.Index)||chunk.Hash is not{Length:32}||!CryptographicOperations.FixedTimeEquals(chunk.Hash,first.Hash)||data is null){throw new InvalidDataException("資源chunkの識別・順序または内容が不正です。");}
            var offset=chunk.Index*ChunkBytes;var length=Math.Min(ChunkBytes,first.TotalBytes-offset);
            if(length<1||data.Length!=length){throw new InvalidDataException("資源chunkの長さが一致しません。");}data.CopyTo(bytes,offset);
        }
        if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes),first.Hash)){throw new InvalidDataException("資源記録のhashが一致しません。");}
        return JsonSerializer.Deserialize<ResourceHeatTrace>(bytes,ManagementJson.Options)??throw new InvalidDataException("資源記録がありません。");
    }
    public ValueTask DisposeAsync(){lock(this.gate){this.closed=true;if(this.disposal is null){this.signal.Release();this.disposal=this.DisposeCoreAsync();}return new(this.disposal);}}
    private async Task DisposeCoreAsync(){await this.worker.ConfigureAwait(false);this.signal.Dispose();}
}
