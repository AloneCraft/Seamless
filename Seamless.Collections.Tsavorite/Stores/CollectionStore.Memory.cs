namespace Seamless.Collections.Tsavorite;

public abstract partial class CollectionStore<TKey,TValue,TStoreFunctions,TAllocator>{
    private long overflowReservationBytes=TsavoriteMemoryAccounting.OverflowBytes(0);
    private long overflowBucketLimit=TsavoriteMemoryAccounting.OverflowBucketLimit(0);
    private global::Seamless.Execution.ResourceReservation? failedGrowthOverlap;
    private long RequiredNativeMemory(long logBytes,long? index=null,long? overflow=null){
        var page=this.Settings.PageSize;var capacity=this.Settings.LogMemorySize;
        var requested=Math.Min(capacity/page,Math.Max(2,checked((Math.Max(0,logBytes)+page-1)/page+2)));
        // Compactが論理先頭を進めても、nativeが保持するページは直ちに消えるとは限らない。
        var allocated=this.Store.Log.MemorySizeBytes/page;
        var overflowBytes=overflow??Math.Max(this.overflowReservationBytes,TsavoriteMemoryAccounting.ObservedOverflowBytes(this.Store.IndexOverflowSizeBytes,this.Store.OverflowBucketCount,this.indexBytes));
        return TsavoriteMemoryAccounting.Required(index??this.indexBytes,overflowBytes,capacity,page,this.Settings.LogDevice.SectorSize,Math.Max(requested,allocated));
    }
    internal void EnsureOverflowCapacity(long additionalEntries=0){
        if(this.layout is null){return;}
        var required=checked(this.Store.OverflowBucketCount+additionalEntries+2L*Math.Max(1,Volatile.Read(ref this.accessorCount))+2);
        if(required<Volatile.Read(ref this.overflowBucketLimit)){return;}
        this.storeGate.Wait();
        try{this.EnsureOverflowCapacityCore(additionalEntries);}finally{this.storeGate.Release();}
    }
    // storeGate内でだけ呼ぶ。復旧でnativeのallocatorが交換された場合も会計を合わせる。
    private void EnsureOverflowCapacityCore(long additionalEntries=0){
        if(this.layout is null){return;}
        var required=checked(this.Store.OverflowBucketCount+additionalEntries+2L*Math.Max(1,Volatile.Read(ref this.accessorCount))+2);
        // 2.2.5の公式確保量も照合する。将来分の推定は割当前の予約にのみ用いる。
        var bytes=Math.Max(TsavoriteMemoryAccounting.OverflowBytes(required,this.indexBytes),TsavoriteMemoryAccounting.ObservedOverflowBytes(this.Store.IndexOverflowSizeBytes,this.Store.OverflowBucketCount,this.indexBytes));
        if(bytes>this.overflowReservationBytes){this.layout.Memory.Resize(checked(this.layout.Memory.Bytes+bytes-this.overflowReservationBytes));this.overflowReservationBytes=bytes;}
        Volatile.Write(ref this.overflowBucketLimit,TsavoriteMemoryAccounting.OverflowBucketLimit(required));
    }
    private NativePageRelease TrackNativePageRelease(){
        // Garnet 2.2.5でもFreePageはtrackerがある場合だけ旧ページをpoolへ返す。
        // Startしないため背景の退避を始めない。Memoryの有効な履歴をNullDeviceへ追い出さない。
        var log=this.Store.Log;var tracker=new LogSizeTracker<TStoreFunctions,TAllocator>(log,Math.Max(this.Settings.LogMemorySize,4*this.Settings.PageSize),0,0,null!);
        log.SetLogSizeTracker(tracker);return new(log);
    }
    private readonly struct NativePageRelease(LogAccessor<TStoreFunctions,TAllocator> log):IDisposable{
        public void Dispose()=>log.SetLogSizeTracker(null!);
    }
}
