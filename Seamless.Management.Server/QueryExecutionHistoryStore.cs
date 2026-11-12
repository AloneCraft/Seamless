using Seamless.Execution;

namespace Seamless.Management.Server;

internal sealed record QueryExecutionHistoryOptions(int MaximumPending=256,long MaximumPendingBytes=1L<<20,int MaximumFiles=4096,long MaximumDiskBytes=32L<<20);
/// <summary>任意の診断履歴。DB/WALに触れず、記録I/Oは専用consumerへ送る。</summary>
internal sealed class QueryExecutionHistoryStore:IAsyncDisposable{
    private const int MaximumPeriodScan=65537;
    private sealed class RetentionException(Exception inner):IOException("実績履歴の保持上限を確保できませんでした。",inner);
    private sealed record Pending(QueryExecutionHistoryEntry Entry,ResourceReservation Reservation);
    private readonly object gate=new();
    private readonly object periodGate=new();
    private readonly Queue<Pending> queue=new();
    private readonly SemaphoreSlim signal=new(0);
    private readonly ResourceBudget budget;
    private readonly QueryExecutionHistoryOptions options;
    private readonly string directory;
    private readonly ExecutionHistoryPeriods periods;
    private readonly Guid session=Guid.NewGuid();
    private readonly Task worker;
    private readonly Func<QueryExecutionHistoryEntry,CancellationToken,Task>? writeOverride;
    private long sequence,accepted,saved,dropped,failures,bytes;
    private int pending;
    private string? error;
    private bool closed;
    private Task? disposal;
    internal QueryExecutionHistoryStore(string directory,ResourceBudget budget,QueryExecutionHistoryOptions? options=null,Func<QueryExecutionHistoryEntry,CancellationToken,Task>? writeOverride=null){
        this.directory=directory;this.budget=budget;this.options=options??new();this.writeOverride=writeOverride;this.periods=new(directory,budget);
        if(this.options.MaximumPending<1||this.options.MaximumPendingBytes<1024||this.options.MaximumFiles<1||this.options.MaximumFiles>65536||this.options.MaximumDiskBytes<32768){throw new ArgumentException("実績履歴の上限が不正です。");}
        this.worker=Task.Run(this.WorkAsync);
    }
    internal bool TryRecord(QueryExecutionObservation observation){
        var size=8192L+32L*(observation.DefinitionId.Length+observation.ObjectId.Length+(observation.ArtifactHash?.Length??0)+(observation.ErrorCode?.Length??0));
        lock(this.gate){
            if(this.closed){return false;}
            var next=++this.sequence;
            if(this.pending>=this.options.MaximumPending||size>this.options.MaximumPendingBytes-this.bytes){this.dropped++;this.error="BufferFull";return false;}
            ResourceReservation reservation;
            try{reservation=this.budget.ReserveMemory(size);}catch(ResourceLimitException){this.dropped++;this.error="MemoryBudget";return false;}
            this.queue.Enqueue(new(new(this.session,next,observation),reservation));this.pending++;this.bytes+=size;this.accepted++;this.signal.Release();
        }
        return true;
    }
    internal QueryExecutionHistoryStatus Status(){lock(this.gate){return new(this.session,this.accepted,this.saved,this.dropped,this.failures,this.bytes,this.pending,this.error,this.closed);}}
    private async Task WorkAsync(){
        while(true){
            await this.signal.WaitAsync().ConfigureAwait(false);
            var batch=new List<Pending>(64);
            lock(this.gate){
                if(!this.queue.TryDequeue(out var first)){if(this.closed){return;}continue;}batch.Add(first);
                while(batch.Count<64&&this.queue.TryDequeue(out var next)){batch.Add(next);_=this.signal.Wait(0);}
            }
            try{
                if(this.writeOverride is{} write){foreach(var work in batch){await write(work.Entry,CancellationToken.None).ConfigureAwait(false);}}
                else{
                    this.periods.EnsureWritable();
                    try{lock(this.periodGate){this.Reclaim(reservedPeriods:1);}}catch(Exception exception){throw new RetentionException(exception);}
                    _=this.periods.Write(batch.Select(work=>work.Entry).ToArray());
                }
                lock(this.gate){this.saved+=batch.Count;this.error=null;}
                try{lock(this.periodGate){this.Reclaim();}}catch(Exception){lock(this.gate){this.error="RetentionFailed";}}
            }catch(IncompleteExecutionHistoryPeriodException){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="IncompletePeriod";}}
            catch(RetentionException){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="RetentionFailed";}}
            catch(Exception){lock(this.gate){this.failures++;this.dropped+=batch.Count;this.error="SaveFailed";}}
            finally{lock(this.gate){foreach(var work in batch){this.pending--;this.bytes-=work.Reservation.Bytes;work.Reservation.Dispose();}}}
        }
    }
    private void Reclaim(int reservedPeriods=0){
        if(!Directory.Exists(this.directory)){return;}
        // 保持件数と検査上限を分離し、既に保持件数を超えた領域もまとめて回収する。
        var directories=new DirectoryInfo(this.directory).EnumerateDirectories("period-*").Take(MaximumPeriodScan+1).ToArray();
        if(directories.Length>MaximumPeriodScan){throw new InvalidDataException("実績期間の検査上限を超えています。");}
        var periods=directories.OrderBy(period=>period.Name,StringComparer.Ordinal).Select(period=>(Directory:period,Bytes:period.EnumerateFiles("*",SearchOption.AllDirectories).Sum(file=>file.Length))).ToArray();
        // 未完領域は回収対象にせず、残存する物理量として保持上限へ算入する。
        var incompleteBytes=Directory.EnumerateDirectories(this.directory,"writing-*").Sum(path=>Directory.EnumerateFiles(path,"*",SearchOption.AllDirectories).Sum(file=>new FileInfo(file).Length));
        var total=checked(periods.Sum(period=>period.Bytes)+incompleteBytes);var count=periods.Length;
        foreach(var period in periods){
            if(count<=this.options.MaximumFiles-reservedPeriods&&total<=this.options.MaximumDiskBytes){break;}
            var root=Path.GetFullPath(this.directory)+Path.DirectorySeparatorChar;var target=Path.GetFullPath(period.Directory.FullName);
            if(!target.StartsWith(root,StringComparison.Ordinal)||period.Directory.Attributes.HasFlag(FileAttributes.ReparsePoint)){throw new InvalidDataException("実績期間の削除先が不正です。");}
            period.Directory.Delete(true);total-=period.Bytes;count--;
        }
        if(total>this.options.MaximumDiskBytes){throw new InvalidDataException("実績履歴の保持上限を確保できません。");}
    }
    internal Task<QueryExecutionHistoryPage> ReadAsync(QueryExecutionHistoryQuery query,CancellationToken cancellation)=>Task.Run(()=>{lock(this.periodGate){return this.Read(query,cancellation);}},cancellation);
    private QueryExecutionHistoryPage Read(QueryExecutionHistoryQuery query,CancellationToken cancellation){
        var values=new List<(QueryExecutionHistoryEntry Entry,long Bytes)>();var sequences=new Dictionary<Guid,List<long>>();var executions=new HashSet<Guid>();var positions=new HashSet<(Guid,long)>();var invalid=0;var limited=false;var absent=false;var readBudgetLimited=false;
        using var memory=this.budget.ReserveMemory(0);
        try{
            if(!Directory.Exists(this.directory)){absent=true;}
            else{
                if(this.Status().PendingCount==0){invalid+=Directory.EnumerateDirectories(this.directory,"writing-*").Take(this.options.MaximumFiles).Count();}
                var examined=0;
                foreach(var path in Directory.EnumerateDirectories(this.directory,"period-*").Take(this.options.MaximumFiles+1)){
                    if(examined++==this.options.MaximumFiles){limited=true;break;}
                    cancellation.ThrowIfCancellationRequested();
                    long reading=0;
                    try{
                        const long requested=64L*32768*4;memory.Resize(memory.Bytes+requested);reading=requested;
                        var entries=this.periods.Read(path,cancellation);
                        if(entries.Any(entry=>!Valid(entry.Observation))){invalid++;continue;}
                        foreach(var entry in entries){
                            if(!executions.Add(entry.Observation.ExecutionId)||!positions.Add((entry.SessionId,entry.Sequence))){invalid++;continue;}
                            memory.Resize(memory.Bytes+256);
                            if(!sequences.TryGetValue(entry.SessionId,out var numbers)){numbers=[];sequences.Add(entry.SessionId,numbers);}numbers.Add(entry.Sequence);
                            var observation=entry.Observation;
                            if(observation.DefinitionId==query.DefinitionId&&observation.ObjectId==query.ObjectId&&(query.Before is null||string.CompareOrdinal(Key(entry),query.Before)<0)){
                                const long kept=32768*4;memory.Resize(memory.Bytes+kept);values.Add((entry,kept));
                                if(values.Count>query.Limit+1){var oldest=values.MinBy(value=>Key(value.Entry),StringComparer.Ordinal);values.Remove(oldest);memory.Resize(memory.Bytes-oldest.Bytes);}
                            }
                        }
                    }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
                    catch(ResourceLimitException){limited=true;readBudgetLimited=true;break;}
                    catch(Exception){invalid++;}
                    finally{if(reading>0){memory.Resize(memory.Bytes-reading);}}
                }
            }
        }catch(DirectoryNotFoundException){absent=true;}catch(IOException){invalid++;}catch(UnauthorizedAccessException){invalid++;}
        long missing=0;
        foreach(var group in sequences.Values){
            var numbers=group.Distinct().Order().ToArray();
            for(var index=1;index<numbers.Length;index++){var gap=numbers[index]-numbers[index-1]-1;if(gap>long.MaxValue-missing){missing=long.MaxValue;}else{missing+=gap;}}
        }
        var selected=values.Select(value=>value.Entry).OrderByDescending(entry=>Key(entry),StringComparer.Ordinal).ToArray();
        var more=selected.Length>query.Limit;var rows=selected.Take(query.Limit).ToArray();string? next=null;if(more){next=Key(rows[^1]);}
        var status=this.Status();if(readBudgetLimited&&status.ErrorCode is null){status=status with{ErrorCode="ReadMemoryBudget"};}
        return new(rows,next,more,status,missing,invalid,limited,absent);
    }
    private static string Key(QueryExecutionHistoryEntry entry)=>entry.Observation.CompletedAtUtc.UtcTicks.ToString("D19",System.Globalization.CultureInfo.InvariantCulture)+":"+entry.Observation.ExecutionId.ToString("N");
    private static bool Valid(QueryExecutionObservation value)=>
        value.ExecutionId!=Guid.Empty&&!string.IsNullOrWhiteSpace(value.DefinitionId)&&!string.IsNullOrWhiteSpace(value.ObjectId)&&
        value.DefinitionId.Length<=4096&&value.ObjectId.Length<=4096&&value.Elapsed>=TimeSpan.Zero&&value.DatabaseGateWait>=TimeSpan.Zero&&
        value.DatabaseGateWait<=value.Elapsed&&value.TimeToFirstOutput is not{Ticks:<0}&&!(value.TimeToFirstOutput>value.Elapsed)&&
        value.ObservedOutputCount>=0&&value.ReturnedOutputCount>=0&&value.ReturnedOutputCount<=value.ObservedOutputCount&&
        (value.Outcome is "Succeeded" or "Failed" or "Cancelled")&&(value.Phase is "Execution" or "DatabaseGate" or "VersionBinding")&&
        (!value.IdentityResolved||value.Version>0&&value.ArtifactHash is{Length:64} hash&&hash.All(Uri.IsHexDigit));
    internal static void Validate(QueryExecutionHistoryQuery query){
        if(string.IsNullOrWhiteSpace(query.DefinitionId)||query.DefinitionId.Length>4096||string.IsNullOrWhiteSpace(query.ObjectId)||query.ObjectId.Length>4096||query.Limit is <1 or >100||query.Before is{} before&&(before.Length!=52||before[19]!=':'||!long.TryParse(before.AsSpan(0,19),out _)||!Guid.TryParseExact(before.AsSpan(20),"N",out _))){throw new ManagementException("InvalidRequest","実績履歴の対象またはページ条件が不正です。");}
    }
    public ValueTask DisposeAsync(){lock(this.gate){this.closed=true;if(this.disposal is null){this.signal.Release();this.disposal=this.DisposeCoreAsync();}return new(this.disposal);}}
    private async Task DisposeCoreAsync(){await this.worker.ConfigureAwait(false);this.signal.Dispose();}
}
