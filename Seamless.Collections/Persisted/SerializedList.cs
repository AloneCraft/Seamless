namespace Seamless.Collections;

public class SerializedList<T>(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null):SerializedList<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity,tuning);
public class SerializedList<T, TSerializer>:PersistedList<T>, IPersistedKeyMatcher where TSerializer : ISerializer<T> {
    private const long UnusedIndex = -1;
    // Set/Dictionary が override して true にすると、レコードキー→データオフセットのディスク上のハッシュ索引を維持する。
    // これにより Contains/TryGetValue が O(n) 走査から O(1) 索引 lookup になる(技法 c/d)。
    private PersistedHashIndex? keyIndex;
    private readonly IPersistedKeyPolicy? keyPolicy;
    private Stream indexStream = null!;
    private PersistedFileAccessor indexAccessor = null!;
    private Stream dataStream = null!;
    private PersistedFileAccessor dataAccessor = null!;
    private long persistedCount;
    private StorageTuning tuning;

    public string IndexPath { get; }
    public long DataLength { get; private set; }
    public long DataCapacity { get; private set; }
    public override StorageTuning Tuning => this.tuning;

    public SerializedList(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null)
        :this(path,initialCapacity,initialDataCapacity,tuning,null,null){
    }

    internal SerializedList(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning,IPersistedKeyPolicy? keyPolicy,IPersistedStorageQuota? quota):base(path){
        this.keyPolicy=keyPolicy;
        this.StorageQuota=quota;
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialDataCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));

        this.tuning=tuning??StorageTuning.Default;
        this.IndexPath=path+".index";
        try {
            this.indexStream=this.OpenStream(this.IndexPath);
            this.dataStream=this.OpenStream(this.Path);

            if(this.indexStream.Length==0&&this.dataStream.Length==0) {
                this.Count=0;
                this.Capacity=this.AlignIndexCapacity(initialCapacity);
                this.DataLength=0;
                this.DataCapacity=this.AlignDataCapacity(initialDataCapacity);
                this.StorageQuota?.EnsureTotalBytes(checked(this.GetIndexFileLength(this.Capacity)+this.GetDataFileLength(this.DataCapacity)));
                this.indexStream.SetLength(this.GetIndexFileLength(this.Capacity));
                this.dataStream.SetLength(this.GetDataFileLength(this.DataCapacity));
                this.CreateMappings();
                this.InitializeIndexRange(0,this.Capacity);
                this.persistedCount=0;
            } else {
                if(this.indexStream.Length==0||this.dataStream.Length==0)
                    throw new InvalidDataException("List companion files are incomplete.");
                this.CreateMappings();
                this.LoadState();
            }

            this.ApplyTuning();
            this.InitializeKeyIndex();
        } catch {
            this.keyIndex?.Dispose();
            this.indexAccessor?.Dispose();
            this.dataAccessor?.Dispose();
            this.indexStream?.Dispose();
            this.dataStream?.Dispose();
            throw;
        }
    }

    // Set/Dictionary が override する。ExtractKeyBytes は payload からキーバイト範囲を切り出す(既定=payload 全体)。
    protected virtual bool UsesKeyIndex => false;
    protected virtual ReadOnlySpan<byte> ExtractKeyBytes(ReadOnlySpan<byte> recordPayload) => recordPayload;

    private string HashIndexPath => this.Path+".hindex";

    private void InitializeKeyIndex() {
        if(!this.UsesKeyIndex)
            return;
        var expectedEntries=this.Count;
        if(this.keyPolicy is not null){
            expectedEntries=0;
        }
        this.keyIndex=new PersistedHashIndex(this.HashIndexPath,this.Tuning,expectedEntries,this.ReserveHashStorage);
        if(this.keyPolicy is null&&this.keyIndex.ListCountAtFlush==this.Count)
            return; // 索引はデータと整合(高速 open)。不一致なら再構築。
        this.RebuildKeyIndex();
    }

    private void RebuildKeyIndex() {
        this.keyIndex!.Clear();
        for(var index = 0L;index<this.Count;index++) {
            var recordOffset = this.ReadIndexUnchecked(index);
            using var memory=this.keyPolicy?.ReserveRead(checked(128+2L*this.ReadRecordLength(recordOffset)));
            var payload = this.ReadRecord(recordOffset);
            var key = this.ExtractKeyBytes(payload);
            this.keyIndex.Upsert(this.HashKey(key),recordOffset,this,key);
        }
        this.keyIndex.SetListCount(this.Count);
        this.keyIndexStale=false;
    }

    // 末尾削除(Remove)などで件数が減ると、ハッシュ索引に消えたレコードが残る(索引に削除は無い)。
    // その場合は古いとだけ記録し、次の検索でまとめて作り直す(BUG-0003)。
    private bool keyIndexStale;

    // データオフセットのレコードキーと probe のキーバイト列を deserialize なしで透過比較する(技法 d)。
    bool IPersistedKeyMatcher.KeyEqualsAt(long dataOffset,ReadOnlySpan<byte> probeKeyBytes) {
        using var memory=this.keyPolicy?.ReserveRead(checked(128+2L*this.ReadRecordLength(dataOffset)));
        var payload = this.ReadRecord(dataOffset);
        if(this.keyPolicy is not null){
            return this.keyPolicy.Equals(this.ExtractKeyBytes(payload),probeKeyBytes);
        }
        return ByteRange.Equals(this.ExtractKeyBytes(payload),probeKeyBytes);
    }

    // キーバイト列からデータオフセットを O(1) で解決する。索引未使用なら false。
    protected bool TryLookupKey(ReadOnlySpan<byte> keyBytes,out long recordOffset) {
        if(this.keyIndex is null) {
            recordOffset=-1;
            return false;
        }
        if(this.keyIndexStale) {
            this.RebuildKeyIndex();
        }
        return this.keyIndex.TryGetOffset(this.HashKey(keyBytes),this,keyBytes,out recordOffset);
    }

    private ulong HashKey(ReadOnlySpan<byte> key){
        if(this.keyPolicy is not null){
            return this.keyPolicy.Hash(key);
        }
        return ByteRange.Hash(key);
    }

    private void ReserveHashStorage(long hashBytes){
        this.StorageQuota?.EnsureTotalBytes(checked(this.indexStream.Length+this.dataStream.Length+hashBytes));
    }

    protected long KeyIndexEntryCount => this.keyIndex?.EntryCount??0;
    public override void SetTuning(StorageTuning tuning) {
        this.ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;

        PersistedFile.ValidateTuning(tuning);
        var requiresReopen = MemoryMappedListTuning.RequiresReopen(this.tuning,tuning);
        this.tuning=tuning;
        if(requiresReopen) {
            this.ReopenMappings();
            return;
        }

        this.ApplyTuning();
    }

    public override void Add(T item) {
        this.ThrowIfDisposed();
        var bytes = TSerializer.Serialize(ref item);
        this.AppendRecord(bytes.ToArray());
    }

    internal void AddRawBytes(ReadOnlySpan<byte> item) {
        this.ThrowIfDisposed();
        this.AppendRecord(item.ToArray());
    }

    //    public override void Add(scoped ReadOnlySpan<byte> item) {
    //        this.ThrowIfDisposed();
    //        this.AppendRecord(item.ToArray());
    //    }

    public void DuplicateRemoval() {
        this.ThrowIfDisposed();
        if(this.Count<2)
            return;

        this.SortBySerializedBits();

        var writeIndex = 1L;
        var previousOffset = this.ReadIndexUnchecked(0);
        for(var readIndex = 1L;readIndex<this.Count;readIndex++) {
            var currentOffset = this.ReadIndexUnchecked(readIndex);
            if(this.CompareData(previousOffset,currentOffset)==0)
                continue;
            if(writeIndex!=readIndex) {
                this.WriteIndexUnchecked(writeIndex,currentOffset);
            }
            previousOffset=currentOffset;
            writeIndex++;
        }

        // 重複を除くだけなら、索引が指すオフセットのレコードは同じキーのまま残るので索引は有効。
        // 件数が減っても古いとは扱わない(それ以前の Remove で古くなっていた場合はその状態を保つ)。
        var wasStale = this.keyIndexStale;
        this.Count=writeIndex;
        this.OnCountChanged();
        this.keyIndexStale=wasStale;
        this.indexAccessor.Flush();
        // データオフセットは不変なので索引は有効。件数だけ整合させる。
        if(!wasStale) {
            this.keyIndex?.SetListCount(this.Count);
        }
    }

    private void LoadState() {
        if(this.indexStream.Length%sizeof(long)!=0)
            throw new InvalidDataException("The managed list index file length is invalid.");

        this.Capacity=this.indexStream.Length/sizeof(long);
        this.DataCapacity=this.dataStream.Length;
        if(this.Capacity<1)
            throw new InvalidDataException("The managed list index file is empty.");
        if(this.DataCapacity<1)
            throw new InvalidDataException("The managed list data file is empty.");

        this.Count=this.LoadCount();
        this.DataLength=this.LoadDataLength();
        this.persistedCount=this.Count;
    }

    private void CreateMappings() {
        this.indexAccessor=new PersistedFileAccessor(this.indexStream);
        this.dataAccessor=new PersistedFileAccessor(this.dataStream);
    }

    private void ReopenMappings() {
        this.indexAccessor.Flush();
        this.dataAccessor.Flush();
        this.indexAccessor.Dispose();
        this.indexStream.Dispose();
        this.dataAccessor.Dispose();
        this.dataStream.Dispose();
        this.indexStream=this.OpenStream(this.IndexPath);
        this.dataStream=this.OpenStream(this.Path);
        this.CreateMappings();
        this.ApplyTuning();
    }

    protected void Rewrite(IEnumerable<T> items) {
        this.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items.ToArray();
        this.Count=0;
        this.DataLength=0;
        this.OnCountChanged();
        this.InitializeIndexRange(0,this.Capacity);
        this.keyIndex?.Clear(); // データ全書き換えでオフセットが変わるため索引を作り直す。
        this.keyIndexStale=false; // 以降の Add で索引を作り直すので、古いものとして扱わない。
        foreach(var item in materialized) {
            this.Add(item);
        }
        this.indexAccessor.Flush();
        this.dataAccessor.Flush();
    }
    private void AppendRecord(byte[] bytes) {
        this.EnsureIndexCapacity(this.Count+1);
        var recordOffset = this.DataLength;
        var recordLength = checked(sizeof(int)+(long)bytes.Length);
        this.EnsureDataCapacity(recordOffset+recordLength);
        PersistedHashIndex.PreparedUpsert prepared=default;
        if(this.keyIndex is not null){
            var key=this.ExtractKeyBytes(bytes);
            prepared=this.keyIndex.PrepareUpsert(this.HashKey(key),this,key);
        }
        this.WriteRecord(recordOffset,bytes);
        this.WriteIndexUnchecked(this.Count,recordOffset);
        this.Count++;
        this.DataLength=recordOffset+recordLength;
        this.OnCountChanged();
        if(this.keyIndex is not null) {
            // entryCount は公開時に header へ書く。listCount は reopen 検証専用のため
            // 毎 Add では書かず Dispose/Rewrite/DuplicateRemoval 時にだけ確定する(書込みオーバーヘッド削減)。
            this.keyIndex.PublishPrepared(prepared,recordOffset);
        }
    }

    private void EnsureIndexCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var newCapacity = this.AlignIndexCapacity(Math.Max(this.Capacity*2,minimumCapacity));
        this.RemapIndex(newCapacity);
    }

    private void EnsureDataCapacity(long minimumLength) {
        if(minimumLength<=this.DataCapacity)
            return;
        var newCapacity = this.AlignDataCapacity(Math.Max(this.DataCapacity*2,minimumLength));
        this.RemapData(newCapacity);
    }

    internal override long StorageBytes => this.indexStream.Length+this.dataStream.Length+(this.keyIndex?.FileLength??0);

    internal override PersistedCommitState Commit() {
        this.ThrowIfDisposed();
        if(this.keyIndexStale) {
            this.RebuildKeyIndex();
        }
        this.keyIndex?.SetListCount(this.Count);
        this.indexAccessor.Flush();
        this.dataAccessor.Flush();
        this.keyIndex?.FlushToDisk();
        PersistedFile.FlushToDisk(this.indexStream);
        PersistedFile.FlushToDisk(this.dataStream);
        return new(this.Count,this.DataLength);
    }

    internal override void RollbackTo(PersistedCommitState state) {
        this.ThrowIfDisposed();
        if(state.Count<0||state.Count>this.Count||state.DataLength<0||state.DataLength>this.DataCapacity)
            throw new InvalidDataException("The saved state does not match the list files.");
        this.Count=state.Count;
        this.DataLength=state.DataLength;
        this.OnCountChanged();
        if(this.keyIndex is not null) {
            this.RebuildKeyIndex();
        }
    }

    private void RemapIndex(long newCapacity) {
        this.StorageQuota?.EnsureTotalBytes(this.GetIndexFileLength(newCapacity)+this.dataStream.Length+(this.keyIndex?.FileLength??0));
        var oldCapacity = this.Capacity;
        this.indexAccessor.Flush();
        this.indexAccessor.Dispose();
        this.Capacity=newCapacity;
        this.indexStream.SetLength(this.GetIndexFileLength(newCapacity));
        this.indexAccessor=new PersistedFileAccessor(this.indexStream);
        this.InitializeIndexRange(oldCapacity,newCapacity);
        this.ApplyTuning();
    }

    private void RemapData(long newCapacity) {
        this.StorageQuota?.EnsureTotalBytes(this.indexStream.Length+this.GetDataFileLength(newCapacity)+(this.keyIndex?.FileLength??0));
        this.dataAccessor.Flush();
        this.dataAccessor.Dispose();
        this.DataCapacity=newCapacity;
        this.dataStream.SetLength(this.GetDataFileLength(newCapacity));
        this.dataAccessor=new PersistedFileAccessor(this.dataStream);
        this.ApplyTuning();
    }

    private void SortBySerializedBits() {
        var stack = new Stack<(long Left,long Right)>();
        stack.Push((0,this.Count-1));

        while(stack.Count>0) {
            var (leftStart,rightStart)=stack.Pop();
            var left = leftStart;
            var right = rightStart;

            while(left<right) {
                var i = left;
                var j = right;
                var pivotOffset = this.ReadIndexUnchecked(left+((right-left)>>1));

                while(i<=j) {
                    while(this.CompareData(this.ReadIndexUnchecked(i),pivotOffset)<0) {
                        i++;
                    }
                    while(this.CompareData(this.ReadIndexUnchecked(j),pivotOffset)>0) {
                        j--;
                    }
                    if(i<=j) {
                        if(i!=j) {
                            this.SwapIndex(i,j);
                        }
                        i++;
                        j--;
                    }
                }

                if((j-left)<(right-i)) {
                    if(i<right) {
                        stack.Push((i,right));
                    }
                    right=j;
                } else {
                    if(left<j) {
                        stack.Push((left,j));
                    }
                    left=i;
                }
            }
        }
    }

    private void SwapIndex(long left,long right) {
        var leftOffset = this.ReadIndexUnchecked(left);
        var rightOffset = this.ReadIndexUnchecked(right);
        this.WriteIndexUnchecked(left,rightOffset);
        this.WriteIndexUnchecked(right,leftOffset);
    }

    private int CompareData(long leftOffset,long rightOffset) {
        if(leftOffset==rightOffset)
            return 0;

        var leftLength = this.ReadRecordLength(leftOffset);
        var rightLength = this.ReadRecordLength(rightOffset);
        var leftPosition = this.GetRecordPosition(leftOffset)+sizeof(int);
        var rightPosition = this.GetRecordPosition(rightOffset)+sizeof(int);
        var minimumLength = Math.Min(leftLength,rightLength);

        for(var i = 0;i<minimumLength;i++) {
            var leftByte = this.dataAccessor.ReadByte(leftPosition+i);
            var rightByte = this.dataAccessor.ReadByte(rightPosition+i);
            if(leftByte!=rightByte)
                return leftByte.CompareTo(rightByte);
        }

        return leftLength.CompareTo(rightLength);
    }

    protected override T GetItem(long index) {
        return this.DeserializeAt(this.ReadIndexUnchecked(index));
    }

    protected override void OnCountChanged() {
        if(this.Count<this.persistedCount) {
            this.InitializeIndexRange(this.Count,this.persistedCount);
            if(this.keyIndex is not null) {
                this.keyIndexStale=true;
            }
        }
        this.persistedCount=this.Count;
    }

    protected override void DisposeCore() {
        try {
            if(this.keyIndex is not null) {
                try {
                    // 古い索引は、開き直したときに再構築されるよう件数の不一致を記録する。
                    if(this.keyIndexStale) {
                        this.keyIndex.SetListCount(-1);
                    } else {
                        this.keyIndex.SetListCount(this.Count);
                    }
                } finally {
                    this.keyIndex.Dispose();
                    this.keyIndex=null;
                }
            }
        } finally {
            try {
                PersistedFile.Dispose(this.indexStream,this.indexAccessor);
            } finally {
                PersistedFile.Dispose(this.dataStream,this.dataAccessor);
            }
        }
    }

    private T DeserializeAt(long recordOffset) {
        var bytes = this.ReadRecord(recordOffset);
        return TSerializer.Deserialize(bytes);
    }

    // データオフセットのレコード payload(TSerializer が扱うバイト列)を読む。索引 lookup 後の値抽出に使う。
    protected byte[] ReadRecordPayload(long recordOffset) => this.ReadRecord(recordOffset);
    protected int GetRecordByteLength(long recordOffset)=>this.ReadRecordLength(recordOffset);

    // index 番目のレコードのデータオフセット。索引が指す最新レコードかどうかの判定(逐次走査)に使う。
    protected long GetRecordOffset(long index) {
        if((ulong)index>=(ulong)this.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return this.ReadIndexUnchecked(index);
    }

    internal int GetSerializedByteLength(long index){
        this.ThrowIfDisposed();
        return this.ReadRecordLength(this.GetRecordOffset(index));
    }

    private byte[] ReadRecord(long recordOffset) {
        var length = this.ReadRecordLength(recordOffset);
        var bytes = new byte[length];
        if(length==0)
            return bytes;
        this.dataAccessor.ReadArray(this.GetRecordPosition(recordOffset)+sizeof(int),bytes,0,length);
        return bytes;
    }

    private int ReadRecordLength(long recordOffset) => this.ReadRecordLength(recordOffset,this.DataLength);

    private int ReadRecordLength(long recordOffset,long dataLength) {
        if(recordOffset<0||recordOffset>dataLength-sizeof(int))
            throw new InvalidDataException("The managed list record offset is invalid.");

        var length = this.dataAccessor.ReadInt32(this.GetRecordPosition(recordOffset));
        if(length<0||recordOffset+sizeof(int)+(long)length>dataLength)
            throw new InvalidDataException("The managed list record length is invalid.");
        return length;
    }

    private void WriteRecord(long recordOffset,byte[] bytes) {
        var position = this.GetRecordPosition(recordOffset);
        this.dataAccessor.Write(position,bytes.Length);
        if(bytes.Length!=0) {
            this.dataAccessor.WriteArray(position+sizeof(int),bytes,0,bytes.Length);
        }
    }

    private long GetIndexFileLength(long capacity) => checked(capacity*sizeof(long));
    private long GetDataFileLength(long dataCapacity) => dataCapacity;
    private long GetIndexOffset(long index) => checked(index*sizeof(long));
    private long GetRecordPosition(long recordOffset) => recordOffset;

    private long AlignIndexCapacity(long capacity) => MemoryMappedListTuning.AlignElementCapacity(capacity,sizeof(long));
    private long AlignDataCapacity(long capacity) => MemoryMappedListTuning.AlignBytes(capacity);

    private Stream OpenStream(string path) {
        return PersistedFile.Open(path,this.Tuning);
    }

    private void ApplyTuning() {
        MemoryMappedListTuning.ApplyTuning(this.indexStream,this.indexAccessor,this.Tuning);
        MemoryMappedListTuning.ApplyTuning(this.dataStream,this.dataAccessor,this.Tuning);
        this.keyIndex?.SetTuning(this.Tuning);
    }

    private long ReadIndexUnchecked(long index) => this.indexAccessor.ReadInt64(this.GetIndexOffset(index));

    private void WriteIndexUnchecked(long index,long recordOffset) {
        this.indexAccessor.Write(this.GetIndexOffset(index),recordOffset);
    }

    // 保存(Commit)済みの部分は連続している。保存後に追記した未確定の部分は、強制終了でファイルへ書き戻された順序が歯抜けになりうるので、
    // 最初の未使用エントリまでを数える。それより先の有効に見えるエントリは未確定なので、RollbackTo が保存済みの件数へ戻すときに捨てる。
    // 保存済みの部分に歯抜けがあれば、数えた件数が保存済みの件数より少なくなり、RollbackTo が拒否する。
    private long LoadCount() {
        var count = 0L;
        for(var index = 0L;index<this.Capacity;index++) {
            var recordOffset = this.ReadIndexUnchecked(index);
            if(recordOffset==UnusedIndex) {
                break;
            }
            if(recordOffset<0)
                throw new InvalidDataException("The managed list index file contains an invalid offset.");
            count++;
        }
        return count;
    }

    private long LoadDataLength() {
        var dataLength = 0L;
        for(var index = 0L;index<this.Count;index++) {
            var recordOffset = this.ReadIndexUnchecked(index);
            long recordEnd;
            try {
                var recordLength = this.ReadRecordLength(recordOffset,this.DataCapacity);
                recordEnd=checked(recordOffset+sizeof(int)+(long)recordLength);
            } catch(InvalidDataException) {
                // 保存後に追記した未確定の記録は、強制終了で壊れうる。壊れた記録から先は未確定として数えない(保存済みの部分なら RollbackTo が拒否する)。
                this.Count=index;
                break;
            }
            if(recordEnd>dataLength)
                dataLength=recordEnd;
        }
        return dataLength;
    }

    private void InitializeIndexRange(long startIndex,long endIndexExclusive) {
        for(var index = startIndex;index<endIndexExclusive;index++) {
            this.WriteIndexUnchecked(index,UnusedIndex);
        }
    }
}

public sealed class BlittableList<T>:PersistedList<T>
    where T : unmanaged {
    private const long HeaderLength = sizeof(long);
    private Stream stream = null!;
    private PersistedFileAccessor accessor = null!;
    private StorageTuning tuning;

    public override StorageTuning Tuning => this.tuning;

    public BlittableList(string path,long initialCapacity = 16,StorageTuning? tuning = null) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        this.tuning=tuning??StorageTuning.Default;
        try {
            this.stream=this.OpenStream(this.Path);
            var elementSize = Unsafe.SizeOf<T>();
            var initializeHeader = this.stream.Length==0;
            if(this.stream.Length==0) {
                this.Count=0;
                this.Capacity=this.AlignCapacity(initialCapacity,elementSize);
                this.stream.SetLength(this.GetFileLength(this.Capacity,elementSize));
            } else {
                if(this.stream.Length<HeaderLength||(this.stream.Length-HeaderLength)%elementSize!=0)
                    throw new InvalidDataException("The fixed list file length is invalid.");
                this.Capacity=(this.stream.Length-HeaderLength)/elementSize;
            }
            this.CreateMapping();
            if(initializeHeader)
                this.WriteCount();
            else {
                this.Count=this.LoadCount();
                if(this.Count<0||this.Count>this.Capacity)
                    throw new InvalidDataException("The fixed list count is invalid.");
            }
            this.ApplyTuning();
        } catch {
            this.accessor?.Dispose();
            this.stream?.Dispose();
            throw;
        }
    }

    public override void Add(T item) {
        this.ThrowIfDisposed();
        this.EnsureCapacity(this.Count+1);
        this.accessor.Write(this.GetOffset(this.Count),ref item);
        this.Count++;
        this.OnCountChanged();
    }

    public override void SetTuning(StorageTuning tuning) {
        this.ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;
        PersistedFile.ValidateTuning(tuning);
        var requiresReopen = MemoryMappedListTuning.RequiresReopen(this.tuning,tuning);
        this.tuning=tuning;
        if(requiresReopen) {
            this.ReopenMapping();
            return;
        }
        this.ApplyTuning();
    }

    protected override T GetItem(long index) {
        this.accessor.Read(this.GetOffset(index),out T value);
        return value;
    }

    protected override void OnCountChanged() {
        this.WriteCount();
    }

    protected override void DisposeCore() => PersistedFile.Dispose(this.stream,this.accessor);

    internal override long StorageBytes => this.stream.Length;

    internal override PersistedCommitState Commit() {
        this.ThrowIfDisposed();
        this.accessor.Flush();
        PersistedFile.FlushToDisk(this.stream);
        return new(this.Count,this.Count*Unsafe.SizeOf<T>());
    }

    internal override void RollbackTo(PersistedCommitState state) {
        this.ThrowIfDisposed();
        if(state.Count<0||state.Count>this.Count)
            throw new InvalidDataException("The saved state does not match the list file.");
        this.Count=state.Count;
        this.OnCountChanged();
    }

    private void EnsureCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var elementSize = Unsafe.SizeOf<T>();
        var newCapacity = this.AlignCapacity(Math.Max(this.Capacity*2,minimumCapacity),elementSize);
        this.StorageQuota?.EnsureTotalBytes(this.GetFileLength(newCapacity,elementSize));
        this.accessor.Flush();
        this.accessor.Dispose();
        this.Capacity=newCapacity;
        this.stream.SetLength(this.GetFileLength(newCapacity,elementSize));
        this.CreateMapping();
        this.ApplyTuning();
    }

    private void CreateMapping() {
        this.accessor=new PersistedFileAccessor(this.stream);
    }

    private void ReopenMapping() {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.stream.Dispose();
        this.stream=this.OpenStream(this.Path);
        this.CreateMapping();
        this.ApplyTuning();
    }

    private Stream OpenStream(string path) =>
        PersistedFile.Open(path,this.Tuning);

    private void ApplyTuning() =>
        MemoryMappedListTuning.ApplyTuning(this.stream,this.accessor,this.Tuning);

    private long LoadCount() => this.accessor.ReadInt64(0);
    private void WriteCount() => this.accessor.Write(0,this.Count);

    private long GetOffset(long index) => checked(HeaderLength+index*(long)Unsafe.SizeOf<T>());
    private long GetFileLength(long capacity,int elementSize) => checked(HeaderLength+capacity*(long)elementSize);
    private long AlignCapacity(long capacity,int elementSize) =>
        MemoryMappedListTuning.AlignElementCapacity(capacity,elementSize);
}
