using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
using Seamless.Execution;
namespace Seamless.Collections.Tsavorite;

public interface IHistoryStrategy<TSelf> where TSelf:struct,IHistoryStrategy<TSelf>{
    static abstract bool Invoke(ref TSelf self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long commitVersion,bool deleted);
}

public sealed partial class HistoryStore{
    internal readonly HistoryStructureGate Structure=new();
    private readonly object reservations=new();
    private long pendingAppendBytes;
    public HistoryAppendReservation ReserveAppend(long bytes){
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock(this.reservations){
            // WAL受付前に同時準備済みの追加分も覆う。最小estimateで件数の上界を求め、
            // 耐久化後のInstallで初めて溢れpoolの予約不足になることを防ぐ。
            var pending=checked(this.pendingAppendBytes+bytes);var minimum=this.EstimateAppendBytes(0,0);
            this.EnsureOverflowCapacity(checked((pending+minimum-1)/minimum));
            var target=checked(this.Store.Log.TailAddress+this.pendingAppendBytes+bytes);this.EnsureHistoryCapacity(target);
            this.pendingAppendBytes=checked(this.pendingAppendBytes+bytes);return new(this,bytes);
        }
    }
    internal void EnsureHistoryCapacity(long tail){
        if(this.Kind==StoreKind.Memory){
            var maximum=checked(this.Store.Log.BufferSize*this.PageBytes);var required=checked(tail-this.Store.Log.BeginAddress+this.PageBytes*2);
            if(required>maximum){throw new ResourceLimitException(ResourceLimitKind.Memory,required,maximum);}
        }
        this.EnsureCapacity(tail,0,0);
    }
    internal void ReleaseAppend(long bytes){lock(this.reservations){this.pendingAppendBytes-=bytes;}}
    public long EstimateAppendBytes(int keyBytes,int rowBytes){
        ArgumentOutOfRangeException.ThrowIfNegative(keyBytes);ArgumentOutOfRangeException.ThrowIfNegative(rowBytes);
        var size=checked((long)keyBytes+rowBytes+HistoryFunctions.HeaderBytes+128);
        if(size>this.PageBytes){throw new ResourceLimitException(ResourceLimitKind.Memory,size,this.PageBytes);}
        return checked(size*2);
    }
}

public sealed class HistoryAppendReservation:IDisposable{
    private HistoryStore? owner;
    private readonly long bytes;
    internal HistoryAppendReservation(HistoryStore owner,long bytes){this.owner=owner;this.bytes=bytes;}
    public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.ReleaseAppend(this.bytes);
}

public sealed partial class HistoryAccessor{
    internal unsafe void RemoveDeletedHeads(){
        using var deletes=this.store.Store.NewSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,ContainsDelete>(new());
        using var scan=this.store.Store.Log.Scan(this.store.Store.Log.BeginAddress,this.store.Store.Log.TailAddress);
        while(scan.GetNext()){
            if(scan.Info.Tombstone){continue;}var latest=this.ReadRecord(scan.Key);
            if(!latest.HasValue||latest.Value.Address.Address!=scan.CurrentAddress||!latest.Value.Deleted){continue;}
            var key=latest.Value.Key;var input=default(EqualityEmpty);var output=false;var options=new RMWOptions();Status status;
            this.store.EnsureCapacity(this.store.Store.Log.TailAddress,key.Length,0);
            fixed(byte* pointer=key){
                status=deletes.BasicContext.RMW((FixedSpanByteKey)key,ref input,ref output,ref options);
                if(status.IsPending){deletes.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);using(completed){if(!completed.Next()){throw new IOException("The history deletion completed without a result.");}status=completed.Current.Status;}}
            }
            if(!status.IsCompletedSuccessfully){throw new IOException("The history tombstone cleanup failed: "+status);}
        }
    }
    public void IterateRecords<TStrategy>(ref TStrategy strategy,long readVersion) where TStrategy:struct,IHistoryStrategy<TStrategy>{
        ObjectDisposedException.ThrowIf(this.disposed,this);using var structure=this.store.Structure.Read();
        var functions=new HistoryIteration<TStrategy>(this,strategy,readVersion);
        try{var cursor=0L;Scans.Lookup.Next(this.store.Store,ref functions,ref cursor);}
        finally{strategy=functions.Strategy;}
    }
    internal void IterateRange<TStrategy>(ref TStrategy strategy,long readVersion,long begin,long end) where TStrategy:struct,IHistoryStrategy<TStrategy>{
        ObjectDisposedException.ThrowIf(this.disposed,this);
        var functions=new HistoryIteration<TStrategy>(this,strategy,readVersion);
        try{Scans.Lookup.Next(this.store.Store,ref functions,ref begin,end);}
        finally{strategy=functions.Strategy;}
    }
    public long SnapshotEndAddress=>this.store.Store.Log.TailAddress;
    /// <summary>MoveNextを越えてnativeのscan/epoch/handleを残さず、有限の行数だけコピーする。</summary>
    internal IReadOnlyList<HistoryRecord> ReadSnapshotBatch(long readVersion,ref long cursor,long untilAddress,int maximumRows=64){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);ObjectDisposedException.ThrowIf(this.disposed,this);
        if(maximumRows>256){throw new ArgumentOutOfRangeException(nameof(maximumRows));}
        var strategy=new HistoryBatch(maximumRows);var functions=new HistoryIteration<HistoryBatch>(this,strategy,readVersion);
        Scans.Lookup.Next(this.store.Store,ref functions,ref cursor,untilAddress);return functions.Strategy.Rows;
    }
    public HistorySnapshotCursor OpenSnapshot(long readVersion){ObjectDisposedException.ThrowIf(this.disposed,this);return new(this.store,this.region,readVersion);}
    private struct HistoryBatch(int maximum):IHistoryStrategy<HistoryBatch>{
        internal readonly List<HistoryRecord> Rows=[];
        private readonly int maximum=maximum;
        public static bool Invoke(ref HistoryBatch self,ReadOnlySpan<byte> key,ReadOnlySpan<byte> row,long version,bool deleted){
            self.Rows.Add(new(key.ToArray(),row.ToArray(),version,deleted,default,default));return self.Rows.Count<self.maximum;
        }
    }
    private sealed class HistoryIteration<TStrategy>(HistoryAccessor access,TStrategy strategy,long readVersion):IScanIteratorFunctions where TStrategy:struct,IHistoryStrategy<TStrategy>{
        internal TStrategy Strategy=strategy;
        public bool Reader<T>(in T source,RecordMetadata metadata,long numberOfRecords,out CursorRecordResult result) where T:ISourceLogRecord{
            result=CursorRecordResult.Accept;var header=HistoryFunctions.Decode(source.ValueSpan);
            if(header.Version<=readVersion){return TStrategy.Invoke(ref this.Strategy,source.KeyBytes,source.ValueSpan[HistoryFunctions.HeaderBytes..],header.Version,header.Deleted);}
            var visible=access.ReadVisibleRecord(source.KeyBytes,readVersion);
            if(!visible.HasValue){result=CursorRecordResult.Skip;return true;}
            return TStrategy.Invoke(ref this.Strategy,visible.Value.Key,visible.Value.Row,visible.Value.CommitVersion,visible.Value.Deleted);
        }
        public bool OnStart(long beginAddress,long endAddress)=>true;
        public void OnStop(bool completed,long numberOfRecords){}
        public void OnException(Exception exception,long numberOfRecords){}
    }
}

/// <summary>native epochとsessionはReadBatchの呼出し内だけ。保護leaseとコピーした行だけをawait間に残す。</summary>
public sealed class HistorySnapshotCursor:IDisposable{
    private readonly object ranges=new();
    private readonly HistoryStore store;
    private readonly ParallelRegionState? region;
    private readonly long version,until;
    private readonly IDisposable structure;
    private long cursor;
    private bool disposed;
    internal HistorySnapshotCursor(HistoryStore store,ParallelRegionState? region,long version){
        this.store=store;this.region=region;this.version=version;store.OnAccessorCreated(region);
        try{this.structure=store.Structure.Read();this.cursor=store.Store.Log.BeginAddress;this.until=store.Store.Log.TailAddress;}catch{store.OnAccessorDisposed(region,false,0);throw;}
    }
    /// <summary>アドレス範囲の分割数の上限。snapshotの行数ではない。</summary>
    public int AvailablePartitions=>checked((int)Math.Min(int.MaxValue,(this.until-this.cursor+this.store.PageBytes-1)/this.store.PageBytes));
    /// <summary>
    /// 一つの有界chunkを取得して処理する。走査構造はcursorのownerが全worker終了まで保持する。
    /// ReadBatchと混在させず、全workerの終了前にcursorをDisposeしないこと。
    /// native session/epochはこの呼出し内だけで所有する。
    /// </summary>
    public bool IterateNextRange<TStrategy>(ref TStrategy strategy,int maximumPages=2) where TStrategy:struct,IHistoryStrategy<TStrategy>{
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPages);long begin,end;
        lock(this.ranges){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(this.cursor>=this.until){return false;}
            begin=this.cursor;var nextPage=checked((begin/this.store.PageBytes+maximumPages)*this.store.PageBytes);
            end=Math.Min(this.until,nextPage);this.cursor=end;
        }
        using var access=new HistoryAccessor(this.store,this.region);access.IterateRange(ref strategy,this.version,begin,end);return true;
    }
    public IReadOnlyList<HistoryRecord> ReadBatch(int maximumRows=64){
        ObjectDisposedException.ThrowIf(this.disposed,this);if(this.cursor>=this.until){return [];}
        using var access=new HistoryAccessor(this.store,this.region);var before=this.cursor;
        var rows=access.ReadSnapshotBatch(this.version,ref this.cursor,this.until,maximumRows);
        if(this.cursor<=before){this.cursor=this.until;}return rows;
    }
    public void Dispose(){if(this.disposed){return;}this.disposed=true;try{this.structure.Dispose();}finally{this.store.OnAccessorDisposed(this.region,false,0);}}
}
