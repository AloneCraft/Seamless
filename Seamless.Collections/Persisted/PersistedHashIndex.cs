namespace Seamless.Collections;

/// <summary>
/// データオフセットへ解決するレコードキーの、ファイル上のオープンアドレッシングハッシュ索引。
/// スロット=(ulong keyHash,long dataOffset)。空きは dataOffset==-1。
/// Buffered は mmap、Unbuffered は整列したファイル IO を使う。拡張も同じ設定の一時ファイルへ
/// 再ハッシュして、要素数に比例した管理メモリを確保しない。
/// 削除は Persisted の Set/Dictionary が公開しないため未対応(tombstone 不要)。
/// </summary>
internal sealed class PersistedHashIndex:IDisposable {
    // 形式1: [0]=-slotCapacity(long) [8]=entryCount(long) [16]=listCountAtFlush(long) [24]=formatVersion(long)
    // 容量の負エンコードにより、従来の reader も新しいスロット配置を誤読せず拒否する。
    private const long FormatVersion = 1;
    private const long HeaderLength = 32;
    private const int SlotSize = 16; // ulong hash + long offset
    private const long EmptyOffset = -1;
    private const double LoadFactor = 0.72;

    private readonly string path;
    private readonly Action<long>? reserveStorage;
    private StorageTuning tuning;
    private Stream stream = null!;
    private PersistedFileAccessor accessor = null!;
    private bool mappingsDisposed = true;
    private long slotCapacity;
    private long entryCount;

    public long EntryCount => this.entryCount;
    public long FileLength => this.stream.Length;
    // 共通ストアの保存(Commit)で呼ぶ。変更をファイルへ反映し、さらにディスクへ書き切る。
    public void FlushToDisk() {
        this.accessor.Flush();
        PersistedFile.FlushToDisk(this.stream);
    }
    public long ListCountAtFlush { get; private set; }

    public void SetTuning(StorageTuning tuning) {
        if(this.tuning.Equals(tuning)) {
            return;
        }
        _=tuning.ToFileOptions();
        this.DisposeMappings();
        this.tuning=tuning;
        this.OpenMappings(this.path,0,false);
    }

    public PersistedHashIndex(string path,StorageTuning tuning,long expectedEntries,Action<long>? reserveStorage=null) {
        this.reserveStorage=reserveStorage;
        this.path=path;
        this.tuning=tuning;
        var initialCapacity = CapacityFor(Math.Max(expectedEntries,0));
        var isNew = !PersistedFile.Exists(path);
        if(!isNew){
            var fileLength=PersistedFile.GetLength(path);
            if(fileLength>0&&fileLength<HeaderLength){
                throw new InvalidDataException("The persisted hash index header is incomplete.");
            }
            isNew=fileLength==0;
        }
        this.OpenMappings(path,isNew ? initialCapacity : 0,isNew);
    }

    private void OpenMappings(string filePath,long newSlotCapacity,bool isNew) {
        this.stream=PersistedFile.Open(filePath,this.tuning);
        this.accessor=null!;
        this.mappingsDisposed=false;
        try {
            if(isNew) {
                this.slotCapacity=newSlotCapacity;
                this.entryCount=0;
                this.ListCountAtFlush=0;
                var fileLength=checked(HeaderLength+this.slotCapacity*SlotSize);
                this.reserveStorage?.Invoke(fileLength);
                this.stream.SetLength(fileLength);
                this.CreateAccessor();
                this.WriteHeader();
                this.InitializeSlots(0,this.slotCapacity);
            } else {
                if(this.stream.Length<HeaderLength){
                    throw new InvalidDataException("The persisted hash index header is incomplete.");
                }
                this.reserveStorage?.Invoke(this.stream.Length);
                this.CreateAccessor();
                var storedCapacity=this.accessor.ReadInt64(0);
                var entryCount=this.accessor.ReadInt64(8);
                var listCountAtFlush=this.accessor.ReadInt64(16);
                var formatVersion=this.accessor.ReadInt64(24);
                if(formatVersion!=FormatVersion||storedCapacity>=0||storedCapacity==long.MinValue){
                    throw new InvalidDataException("The persisted hash index format is unsupported or invalid.");
                }
                var slotCapacity=-storedCapacity;
                if((slotCapacity&(slotCapacity-1))!=0
                    ||slotCapacity>(this.stream.Length-HeaderLength)/SlotSize
                    ||entryCount<0||entryCount>=slotCapacity||listCountAtFlush<-1){
                    throw new InvalidDataException("The persisted hash index header is invalid.");
                }
                this.slotCapacity=slotCapacity;
                this.entryCount=entryCount;
                this.ListCountAtFlush=listCountAtFlush;
            }
        } catch {
            this.DisposeMappings(flush:false);
            throw;
        }
    }

    private void CreateAccessor() {
        this.accessor=new PersistedFileAccessor(this.stream);
    }

    private void WriteHeader() {
        this.accessor.Write(0,-this.slotCapacity);
        this.accessor.Write(8,this.entryCount);
        this.accessor.Write(16,this.ListCountAtFlush);
        this.accessor.Write(24,FormatVersion);
    }

    private void InitializeSlots(long startSlot,long endSlotExclusive) {
        for(var slot = startSlot;slot<endSlotExclusive;slot++) {
            this.accessor.Write(this.OffsetPosition(slot),EmptyOffset);
        }
    }

    // 保存ハッシュと等価比較は維持し、探索開始位置だけへ全64bitを混ぜる。
    private static long GetInitialSlot(ulong hash,long mask){
        unchecked{
            hash^=hash>>33;
            hash*=0xff51afd7ed558ccdUL;
            hash^=hash>>33;
            hash*=0xc4ceb9fe1a85ec53UL;
            hash^=hash>>33;
            return (long)(hash&(ulong)mask);
        }
    }

    private long HashPosition(long slot) => HeaderLength+slot*SlotSize;
    private long OffsetPosition(long slot) => HeaderLength+slot*SlotSize+8;

    /// <summary>索引を空にする(データ全書き換え=Rewrite の前段で使う)。</summary>
    public void Clear() {
        this.entryCount=0;
        this.ListCountAtFlush=0;
        this.WriteHeader();
        this.InitializeSlots(0,this.slotCapacity);
    }

    public void SetListCount(long listCount) {
        this.ListCountAtFlush=listCount;
        this.accessor.Write(16,listCount);
    }

    /// <summary>キーの索引登録。既存キーは最新オフセットへ更新(dictionary の後勝ち)。</summary>
    public void Upsert(ulong hash,long dataOffset,IPersistedKeyMatcher matcher,ReadOnlySpan<byte> keyBytes){
        var prepared=this.PrepareUpsert(hash,matcher,keyBytes);
        this.PublishPrepared(prepared,dataOffset);
    }
    /// <summary>キーからデータオフセットを解決。tombstone が無いので空きスロット到達=不在。</summary>
    public bool TryGetOffset(ulong hash,IPersistedKeyMatcher matcher,ReadOnlySpan<byte> keyBytes,out long dataOffset) {
        var mask = this.slotCapacity-1;
        var slot = GetInitialSlot(hash,mask);
        while(true) {
            var existingOffset = this.accessor.ReadInt64(this.OffsetPosition(slot));
            if(existingOffset==EmptyOffset) {
                dataOffset=EmptyOffset;
                return false;
            }
            if(this.accessor.ReadUInt64(this.HashPosition(slot))==hash&&matcher.KeyEqualsAt(existingOffset,keyBytes)) {
                dataOffset=existingOffset;
                return true;
            }
            slot=(slot+1)&mask;
        }
    }

    internal readonly record struct PreparedUpsert(long Slot,ulong Hash,bool Exists);

    /// <summary>比較・予算確認・拡張をすべて済ませ、単一スレッドで直後に公開する位置を確定する。</summary>
    internal PreparedUpsert PrepareUpsert(ulong hash,IPersistedKeyMatcher matcher,ReadOnlySpan<byte> keyBytes){
        var mask=this.slotCapacity-1;
        var slot=GetInitialSlot(hash,mask);
        while(true){
            var existingOffset=this.accessor.ReadInt64(this.OffsetPosition(slot));
            if(existingOffset==EmptyOffset){
                if(this.entryCount+1>=(long)(this.slotCapacity*LoadFactor)){
                    this.Grow();
                    mask=this.slotCapacity-1;
                    slot=GetInitialSlot(hash,mask);
                    continue;
                }
                return new(slot,hash,false);
            }
            if(this.accessor.ReadUInt64(this.HashPosition(slot))==hash&&matcher.KeyEqualsAt(existingOffset,keyBytes)){
                return new(slot,hash,true);
            }
            slot=(slot+1)&mask;
        }
    }

    /// <summary>確定位置への書き込みだけを行い、利用者の比較器や予算確認を再実行しない。</summary>
    internal void PublishPrepared(PreparedUpsert prepared,long dataOffset){
        if(!prepared.Exists){
            this.accessor.Write(this.HashPosition(prepared.Slot),prepared.Hash);
        }
        this.accessor.Write(this.OffsetPosition(prepared.Slot),dataOffset);
        if(!prepared.Exists){
            this.entryCount++;
            this.accessor.Write(8,this.entryCount);
        }
    }
    private void Grow(){
        var newCapacity=checked(this.slotCapacity*2);
        var newLength=checked(HeaderLength+newCapacity*SlotSize);
        var oldLength=this.stream.Length;
        var tempPath=this.path+".tmp-"+Guid.NewGuid().ToString("N");
        this.reserveStorage?.Invoke(checked(oldLength+newLength));
        try{
            using(var tempStream=PersistedFile.Open(tempPath,this.tuning,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){
                tempStream.SetLength(newLength);
                using var tempAccessor=new PersistedFileAccessor(tempStream);
                tempAccessor.Write(0,-newCapacity);
                tempAccessor.Write(8,this.entryCount);
                tempAccessor.Write(16,this.ListCountAtFlush);
                tempAccessor.Write(24,FormatVersion);
                for(var slot=0L;slot<newCapacity;slot++){
                    tempAccessor.Write(HeaderLength+slot*SlotSize+8,EmptyOffset);
                }
                var newMask=newCapacity-1;
                for(var slot=0L;slot<this.slotCapacity;slot++){
                    var offset=this.accessor.ReadInt64(this.OffsetPosition(slot));
                    if(offset==EmptyOffset){
                        continue;
                    }
                    var hash=this.accessor.ReadUInt64(this.HashPosition(slot));
                    var target=GetInitialSlot(hash,newMask);
                    while(tempAccessor.ReadInt64(HeaderLength+target*SlotSize+8)!=EmptyOffset){
                        target=(target+1)&newMask;
                    }
                    tempAccessor.Write(HeaderLength+target*SlotSize,hash);
                    tempAccessor.Write(HeaderLength+target*SlotSize+8,offset);
                }
                tempAccessor.Flush();
                PersistedFile.FlushToDisk(tempStream);
            }
            this.DisposeMappings();
            try{
                PersistedFile.Move(tempPath,this.path);
            }finally{
                this.OpenMappings(this.path,0,false);
            }
        }finally{
            if(PersistedFile.Exists(tempPath)){
                PersistedFile.Delete(tempPath);
            }
            this.reserveStorage?.Invoke(PersistedFile.GetLength(this.path));
        }
    }
    private void DisposeMappings(bool flush=true) {
        if(this.mappingsDisposed) {
            return;
        }
        this.mappingsDisposed=true;
        try {
            if(flush) {
                this.accessor?.Flush();
            }
        } finally {
            try {
                this.accessor?.Dispose();
            } finally {
                this.stream.Dispose();
            }
        }
    }

    private static long CapacityFor(long expectedEntries) {
        double needed=16;
        if(expectedEntries>0){
            needed=Math.Ceiling((expectedEntries+1D)/LoadFactor);
        }
        var maximumCapacity=(long.MaxValue-HeaderLength)/SlotSize;
        var capacity = 16L;
        while(capacity<needed) {
            if(capacity>maximumCapacity/2){
                throw new ArgumentOutOfRangeException(nameof(expectedEntries),"The requested hash index capacity exceeds the supported file length.");
            }
            capacity*=2;
        }
        return capacity;
    }

    public void Dispose() {
        this.DisposeMappings();
    }
}

/// <summary>データオフセットに格納されたレコードキーと、probe のキーバイト列を透過比較する。</summary>
internal interface IPersistedKeyMatcher {
    bool KeyEqualsAt(long dataOffset,ReadOnlySpan<byte> probeKeyBytes);
}

/// <summary>新しい作業用 hash API の比較規則。旧 API はこの口を使わずバイト等価を保つ。</summary>
internal interface IPersistedKeyPolicy{
    ulong Hash(ReadOnlySpan<byte> key);
    bool Equals(ReadOnlySpan<byte> left,ReadOnlySpan<byte> right);
    IDisposable ReserveRead(long bytes);
}
