using System.IO.MemoryMappedFiles;
namespace Seamless.Persisted.Collections;

/// <summary>
/// データオフセットへ解決するレコードキーの、mmap 上のオープンアドレッシングハッシュ索引。
/// スロット=(ulong keyHash,long dataOffset)。空きは dataOffset==-1。
/// アクセスは常に mmap accessor 経由の単一経路で、常駐/ページアウトの判定を呼び出しごとに行わない
/// (溢れは OS ページングで透過処理)。拡張は一時ファイルへ再ハッシュしてメモリ有界に保つ。
/// 削除は Persisted の Set/Dictionary が公開しないため未対応(tombstone 不要)。
/// </summary>
internal sealed class PersistedHashIndex:IDisposable {
    // header: [0]=slotCapacity(long) [8]=entryCount(long) [16]=listCountAtFlush(long) [24]=reserved(long)
    private const long HeaderLength = 32;
    private const int SlotSize = 16; // ulong hash + long offset
    private const long EmptyOffset = -1;
    private const double LoadFactor = 0.72;

    private readonly string path;
    private readonly StorageTuning tuning;
    private FileStream stream = null!;
    private MemoryMappedFile mappedFile = null!;
    private MemoryMappedViewAccessor accessor = null!;
    private long slotCapacity;
    private long entryCount;

    public long EntryCount => this.entryCount;
    public long ListCountAtFlush { get; private set; }

    public PersistedHashIndex(string path,StorageTuning tuning,long expectedEntries) {
        this.path=path;
        this.tuning=tuning;
        var initialCapacity = CapacityFor(Math.Max(expectedEntries,0));
        var isNew = !File.Exists(path)||new FileInfo(path).Length<HeaderLength;
        this.OpenMappings(path,isNew ? initialCapacity : 0,isNew);
    }

    private void OpenMappings(string filePath,long newSlotCapacity,bool isNew) {
        this.stream=new FileStream(filePath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,this.tuning.ResolveBufferSize(),MemoryMappedListTuning.GetFileOptions(this.tuning));
        if(isNew) {
            this.slotCapacity=newSlotCapacity;
            this.entryCount=0;
            this.ListCountAtFlush=0;
            this.stream.SetLength(HeaderLength+this.slotCapacity*SlotSize);
            this.CreateAccessor();
            this.WriteHeader();
            this.InitializeSlots(0,this.slotCapacity);
        } else {
            this.CreateAccessor();
            this.slotCapacity=this.accessor.ReadInt64(0);
            this.entryCount=this.accessor.ReadInt64(8);
            this.ListCountAtFlush=this.accessor.ReadInt64(16);
            if(this.slotCapacity<1||this.stream.Length<HeaderLength+this.slotCapacity*SlotSize) {
                throw new InvalidDataException("The persisted hash index header is invalid.");
            }
        }
    }

    private void CreateAccessor() {
        this.mappedFile=MemoryMappedFile.CreateFromFile(this.stream,null,this.stream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.accessor=this.mappedFile.CreateViewAccessor(0,this.stream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void WriteHeader() {
        this.accessor.Write(0,this.slotCapacity);
        this.accessor.Write(8,this.entryCount);
        this.accessor.Write(16,this.ListCountAtFlush);
        this.accessor.Write(24,0L);
    }

    private void InitializeSlots(long startSlot,long endSlotExclusive) {
        for(var slot = startSlot;slot<endSlotExclusive;slot++) {
            this.accessor.Write(this.OffsetPosition(slot),EmptyOffset);
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
    public void Upsert(ulong hash,long dataOffset,IPersistedKeyMatcher matcher,ReadOnlySpan<byte> keyBytes) {
        var mask = this.slotCapacity-1;
        var slot = (long)(hash&(ulong)mask);
        while(true) {
            var existingOffset = this.accessor.ReadInt64(this.OffsetPosition(slot));
            if(existingOffset==EmptyOffset) {
                this.accessor.Write(this.HashPosition(slot),hash);
                this.accessor.Write(this.OffsetPosition(slot),dataOffset);
                this.entryCount++;
                this.accessor.Write(8,this.entryCount);
                this.GrowIfNeeded();
                return;
            }
            if(this.accessor.ReadUInt64(this.HashPosition(slot))==hash&&matcher.KeyEqualsAt(existingOffset,keyBytes)) {
                this.accessor.Write(this.OffsetPosition(slot),dataOffset);
                return;
            }
            slot=(slot+1)&mask;
        }
    }

    /// <summary>キーからデータオフセットを解決。tombstone が無いので空きスロット到達=不在。</summary>
    public bool TryGetOffset(ulong hash,IPersistedKeyMatcher matcher,ReadOnlySpan<byte> keyBytes,out long dataOffset) {
        var mask = this.slotCapacity-1;
        var slot = (long)(hash&(ulong)mask);
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

    private void GrowIfNeeded() {
        if(this.entryCount<(long)(this.slotCapacity*LoadFactor)) {
            return;
        }
        this.Grow();
    }

    private void Grow() {
        var newCapacity = this.slotCapacity*2;
        var tempPath = this.path+".tmp";
        if(File.Exists(tempPath)) {
            File.Delete(tempPath);
        }

        using(var tempStream = new FileStream(tempPath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)) {
            tempStream.SetLength(HeaderLength+newCapacity*SlotSize);
            using var tempMapped = MemoryMappedFile.CreateFromFile(tempStream,null,tempStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
            using var tempAccessor = tempMapped.CreateViewAccessor(0,tempStream.Length,MemoryMappedFileAccess.ReadWrite);
            tempAccessor.Write(0,newCapacity);
            tempAccessor.Write(8,this.entryCount);
            tempAccessor.Write(16,this.ListCountAtFlush);
            tempAccessor.Write(24,0L);
            for(var slot = 0L;slot<newCapacity;slot++) {
                tempAccessor.Write(HeaderLength+slot*SlotSize+8,EmptyOffset);
            }

            var newMask = newCapacity-1;
            for(var slot = 0L;slot<this.slotCapacity;slot++) {
                var offset = this.accessor.ReadInt64(this.OffsetPosition(slot));
                if(offset==EmptyOffset) {
                    continue;
                }
                var hash = this.accessor.ReadUInt64(this.HashPosition(slot));
                var target = (long)(hash&(ulong)newMask);
                while(tempAccessor.ReadInt64(HeaderLength+target*SlotSize+8)!=EmptyOffset) {
                    target=(target+1)&newMask;
                }
                tempAccessor.Write(HeaderLength+target*SlotSize,hash);
                tempAccessor.Write(HeaderLength+target*SlotSize+8,offset);
            }
            tempAccessor.Flush();
        }

        this.DisposeMappings();
        File.Delete(this.path);
        File.Move(tempPath,this.path);
        this.OpenMappings(this.path,0,false);
    }

    private void DisposeMappings() {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.Dispose();
    }

    private static long CapacityFor(long expectedEntries) {
        var needed = expectedEntries<=0
            ? 16L
            : (long)Math.Ceiling((expectedEntries+1)/LoadFactor);
        var capacity = 16L;
        while(capacity<needed) {
            capacity<<=1;
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
