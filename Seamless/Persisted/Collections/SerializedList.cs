using System.IO.MemoryMappedFiles;
namespace Seamless.Persisted.Collections;
public class SerializedList<T>(string path,long initialCapacity=16,long initialDataCapacity=4096):SerializedList<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity);
public class SerializedList<T, TSerializer>:PersistedList<T>where TSerializer : ISerializer<T> {
    private const long UnusedIndex = -1;
    private FileStream indexStream = null!;
    private MemoryMappedFile indexMappedFile = null!;
    private MemoryMappedViewAccessor indexAccessor = null!;
    private FileStream dataStream = null!;
    private MemoryMappedFile dataMappedFile = null!;
    private MemoryMappedViewAccessor dataAccessor = null!;
    private long persistedCount;
    private TempListTuning tuning = new(FileAccess.ReadWrite,FileOptions.SequentialScan);

    public string IndexPath { get; }
    public long DataLength { get; private set; }
    public long DataCapacity { get; private set; }
    public override FileAccess OptimizationMode => tuning.OptimizationMode;
    public override FileOptions AccessPattern => tuning.AccessPattern;

    public SerializedList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialDataCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));

        IndexPath=path+".index";
        indexStream=OpenStream(IndexPath);
        dataStream=OpenStream(Path);

        if(indexStream.Length==0&&dataStream.Length==0) {
            Count=0;
            Capacity=AlignIndexCapacity(initialCapacity);
            DataLength=0;
            DataCapacity=AlignDataCapacity(initialDataCapacity);
            indexStream.SetLength(GetIndexFileLength(Capacity));
            dataStream.SetLength(GetDataFileLength(DataCapacity));
            CreateMappings();
            InitializeIndexRange(0,Capacity);
            persistedCount=0;
        }
        else {
            if(indexStream.Length==0||dataStream.Length==0)
                throw new InvalidDataException("List companion files are incomplete.");
            CreateMappings();
            LoadState();
        }

        ApplyTuning();
    }
    public override void SetTuning(TempListTuning tuning) {
        ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;

        var requiresReopen = MemoryMappedListTuning.RequiresReopen(this.tuning,tuning);
        this.tuning=tuning;
        if(requiresReopen) {
            ReopenMappings();
            return;
        }

        ApplyTuning();
    }

    public override void Add(T item) {
        ThrowIfDisposed();
        var bytes = TSerializer.Serialize(ref item);
        AppendRecord(bytes.ToArray());
    }

    internal void AddRawBytes(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();
        AppendRecord(item.ToArray());
    }

    //    public override void Add(scoped ReadOnlySpan<byte> item) {
    //        this.ThrowIfDisposed();
    //        this.AppendRecord(item.ToArray());
    //    }

#if TSAVORITE1_1_1
    public void Add(global::Tsavorite.core.SpanByte item) {
        this.ThrowIfDisposed();
        this.AppendRecord(item.ToByteArray());
    }
#endif

    public void DuplicateRemoval() {
        ThrowIfDisposed();
        if(Count<2)
            return;

        SortBySerializedBits();

        var writeIndex = 1L;
        var previousOffset = ReadIndexUnchecked(0);
        for(var readIndex = 1L; readIndex<Count; readIndex++) {
            var currentOffset = ReadIndexUnchecked(readIndex);
            if(CompareData(previousOffset,currentOffset)==0)
                continue;
            if(writeIndex!=readIndex) {
                WriteIndexUnchecked(writeIndex,currentOffset);
            }
            previousOffset=currentOffset;
            writeIndex++;
        }

        Count=writeIndex;
        OnCountChanged();
        indexAccessor.Flush();
    }

    private void LoadState() {
        if(indexStream.Length%sizeof(long)!=0)
            throw new InvalidDataException("The managed list index file length is invalid.");

        Capacity=indexStream.Length/sizeof(long);
        DataCapacity=dataStream.Length;
        if(Capacity<1)
            throw new InvalidDataException("The managed list index file is empty.");
        if(DataCapacity<1)
            throw new InvalidDataException("The managed list data file is empty.");

        Count=LoadCount();
        DataLength=LoadDataLength();
        persistedCount=Count;
    }

    private void CreateMappings() {
        indexMappedFile=MemoryMappedFile.CreateFromFile(indexStream,null,indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        indexAccessor=indexMappedFile.CreateViewAccessor(0,indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        dataMappedFile=MemoryMappedFile.CreateFromFile(dataStream,null,dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        dataAccessor=dataMappedFile.CreateViewAccessor(0,dataStream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void ReopenMappings() {
        indexAccessor.Flush();
        dataAccessor.Flush();
        indexAccessor.Dispose();
        indexMappedFile.Dispose();
        indexStream.Dispose();
        dataAccessor.Dispose();
        dataMappedFile.Dispose();
        dataStream.Dispose();
        indexStream=OpenStream(IndexPath);
        dataStream=OpenStream(Path);
        CreateMappings();
        ApplyTuning();
    }

    protected void Rewrite(IEnumerable<T> items) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items.ToArray();
        Count=0;
        DataLength=0;
        OnCountChanged();
        InitializeIndexRange(0,Capacity);
        foreach(var item in materialized) {
            Add(item);
        }
        indexAccessor.Flush();
        dataAccessor.Flush();
    }
    private void AppendRecord(byte[] bytes) {
        EnsureIndexCapacity(Count+1);
        var recordOffset = DataLength;
        var recordLength = checked(sizeof(int)+(long)bytes.Length);
        EnsureDataCapacity(recordOffset+recordLength);
        WriteRecord(recordOffset,bytes);
        WriteIndexUnchecked(Count,recordOffset);
        Count++;
        DataLength=recordOffset+recordLength;
        OnCountChanged();
    }

    private void EnsureIndexCapacity(long minimumCapacity) {
        if(minimumCapacity<=Capacity)
            return;
        var newCapacity = AlignIndexCapacity(Math.Max(Capacity*2,minimumCapacity));
        RemapIndex(newCapacity);
    }

    private void EnsureDataCapacity(long minimumLength) {
        if(minimumLength<=DataCapacity)
            return;
        var newCapacity = AlignDataCapacity(Math.Max(DataCapacity*2,minimumLength));
        RemapData(newCapacity);
    }

    private void RemapIndex(long newCapacity) {
        var oldCapacity = Capacity;
        indexAccessor.Flush();
        indexAccessor.Dispose();
        indexMappedFile.Dispose();
        Capacity=newCapacity;
        indexStream.SetLength(GetIndexFileLength(newCapacity));
        indexMappedFile=MemoryMappedFile.CreateFromFile(indexStream,null,indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        indexAccessor=indexMappedFile.CreateViewAccessor(0,indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        InitializeIndexRange(oldCapacity,newCapacity);
        ApplyTuning();
    }

    private void RemapData(long newCapacity) {
        dataAccessor.Flush();
        dataAccessor.Dispose();
        dataMappedFile.Dispose();
        DataCapacity=newCapacity;
        dataStream.SetLength(GetDataFileLength(newCapacity));
        dataMappedFile=MemoryMappedFile.CreateFromFile(dataStream,null,dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        dataAccessor=dataMappedFile.CreateViewAccessor(0,dataStream.Length,MemoryMappedFileAccess.ReadWrite);
        ApplyTuning();
    }

    private void SortBySerializedBits() {
        var stack = new Stack<(long Left,long Right)>();
        stack.Push((0,Count-1));

        while(stack.Count>0) {
            var (leftStart,rightStart)=stack.Pop();
            var left = leftStart;
            var right = rightStart;

            while(left<right) {
                var i = left;
                var j = right;
                var pivotOffset = ReadIndexUnchecked(left+((right-left)>>1));

                while(i<=j) {
                    while(CompareData(ReadIndexUnchecked(i),pivotOffset)<0) {
                        i++;
                    }
                    while(CompareData(ReadIndexUnchecked(j),pivotOffset)>0) {
                        j--;
                    }
                    if(i<=j) {
                        if(i!=j) {
                            SwapIndex(i,j);
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
                }
                else {
                    if(left<j) {
                        stack.Push((left,j));
                    }
                    left=i;
                }
            }
        }
    }

    private void SwapIndex(long left,long right) {
        var leftOffset = ReadIndexUnchecked(left);
        var rightOffset = ReadIndexUnchecked(right);
        WriteIndexUnchecked(left,rightOffset);
        WriteIndexUnchecked(right,leftOffset);
    }

    private int CompareData(long leftOffset,long rightOffset) {
        if(leftOffset==rightOffset)
            return 0;

        var leftLength = ReadRecordLength(leftOffset);
        var rightLength = ReadRecordLength(rightOffset);
        var leftPosition = GetRecordPosition(leftOffset)+sizeof(int);
        var rightPosition = GetRecordPosition(rightOffset)+sizeof(int);
        var minimumLength = Math.Min(leftLength,rightLength);

        for(var i = 0; i<minimumLength; i++) {
            var leftByte = dataAccessor.ReadByte(leftPosition+i);
            var rightByte = dataAccessor.ReadByte(rightPosition+i);
            if(leftByte!=rightByte)
                return leftByte.CompareTo(rightByte);
        }

        return leftLength.CompareTo(rightLength);
    }

    protected override T GetItem(long index) {
        return DeserializeAt(ReadIndexUnchecked(index));
    }

    protected override void OnCountChanged() {
        if(Count<persistedCount) {
            InitializeIndexRange(Count,persistedCount);
        }
        persistedCount=Count;
    }

    protected override void DisposeCore() {
        indexAccessor.Flush();
        dataAccessor.Flush();
        indexAccessor.Dispose();
        indexMappedFile.Dispose();
        indexStream.Dispose();
        dataAccessor.Dispose();
        dataMappedFile.Dispose();
        dataStream.Dispose();
    }

    private T DeserializeAt(long recordOffset) {
        var bytes = ReadRecord(recordOffset);
        return TSerializer.Deserialize(bytes);
    }

    private byte[] ReadRecord(long recordOffset) {
        var length = ReadRecordLength(recordOffset);
        var bytes = new byte[length];
        if(length==0)
            return bytes;
        dataAccessor.ReadArray(GetRecordPosition(recordOffset)+sizeof(int),bytes,0,length);
        return bytes;
    }

    private int ReadRecordLength(long recordOffset) => ReadRecordLength(recordOffset,DataLength);

    private int ReadRecordLength(long recordOffset,long dataLength) {
        if(recordOffset<0||recordOffset>dataLength-sizeof(int))
            throw new InvalidDataException("The managed list record offset is invalid.");

        var length = dataAccessor.ReadInt32(GetRecordPosition(recordOffset));
        if(length<0||recordOffset+sizeof(int)+(long)length>dataLength)
            throw new InvalidDataException("The managed list record length is invalid.");
        return length;
    }

    private void WriteRecord(long recordOffset,byte[] bytes) {
        var position = GetRecordPosition(recordOffset);
        dataAccessor.Write(position,bytes.Length);
        if(bytes.Length!=0) {
            dataAccessor.WriteArray(position+sizeof(int),bytes,0,bytes.Length);
        }
    }

    private long GetIndexFileLength(long capacity) => checked(capacity*sizeof(long));
    private long GetDataFileLength(long dataCapacity) => dataCapacity;
    private long GetIndexOffset(long index) => checked(index*sizeof(long));
    private long GetRecordPosition(long recordOffset) => recordOffset;

    private long AlignIndexCapacity(long capacity) => MemoryMappedListTuning.AlignElementCapacity(capacity,sizeof(long));
    private long AlignDataCapacity(long capacity) => MemoryMappedListTuning.AlignBytes(capacity);

    private FileStream OpenStream(string path) {
        return new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,BufferSize,MemoryMappedListTuning.GetFileOptions(Tuning));
    }

    private void ApplyTuning() {
        MemoryMappedListTuning.ApplyTuning(indexStream,indexAccessor,Tuning);
        MemoryMappedListTuning.ApplyTuning(dataStream,dataAccessor,Tuning);
    }

    private long ReadIndexUnchecked(long index) => indexAccessor.ReadInt64(GetIndexOffset(index));

    private void WriteIndexUnchecked(long index,long recordOffset) {
        indexAccessor.Write(GetIndexOffset(index),recordOffset);
    }

    private long LoadCount() {
        var count = 0L;
        var foundUnused = false;
        for(var index = 0L; index<Capacity; index++) {
            var recordOffset = ReadIndexUnchecked(index);
            if(recordOffset==UnusedIndex) {
                foundUnused=true;
                continue;
            }
            if(recordOffset<0)
                throw new InvalidDataException("The managed list index file contains an invalid offset.");
            if(foundUnused)
                throw new InvalidDataException("The managed list index file contains a gap between live entries.");
            count++;
        }
        return count;
    }

    private long LoadDataLength() {
        var dataLength = 0L;
        for(var index = 0L; index<Count; index++) {
            var recordOffset = ReadIndexUnchecked(index);
            var recordLength = ReadRecordLength(recordOffset,DataCapacity);
            var recordEnd = checked(recordOffset+sizeof(int)+(long)recordLength);
            if(recordEnd>dataLength)
                dataLength=recordEnd;
        }
        return dataLength;
    }

    private void InitializeIndexRange(long startIndex,long endIndexExclusive) {
        for(var index = startIndex; index<endIndexExclusive; index++) {
            WriteIndexUnchecked(index,UnusedIndex);
        }
    }
}

public sealed class BlittableList<T>:PersistedList<T>
    where T : unmanaged {
    private const long HeaderLength = sizeof(long);
    private FileStream stream = null!;
    private MemoryMappedFile mappedFile = null!;
    private MemoryMappedViewAccessor accessor = null!;
    private TempListTuning tuning = new(FileAccess.ReadWrite,FileOptions.SequentialScan);

    public override FileAccess OptimizationMode => tuning.OptimizationMode;
    public override FileOptions AccessPattern => tuning.AccessPattern;

    public BlittableList(string path,long initialCapacity = 16) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        stream=OpenStream(Path);
        var elementSize = Unsafe.SizeOf<T>();
        var initializeHeader = stream.Length==0;
        if(stream.Length==0) {
            Count=0;
            Capacity=AlignCapacity(initialCapacity,elementSize);
            stream.SetLength(GetFileLength(Capacity,elementSize));
        }
        else {
            if(stream.Length<HeaderLength||(stream.Length-HeaderLength)%elementSize!=0)
                throw new InvalidDataException("The fixed list file length is invalid.");
            Capacity=(stream.Length-HeaderLength)/elementSize;
        }
        CreateMapping();
        if(initializeHeader)
            WriteCount();
        else {
            Count=LoadCount();
            if(Count<0||Count>Capacity)
                throw new InvalidDataException("The fixed list count is invalid.");
        }
        ApplyTuning();
    }

    public override void Add(T item) {
        ThrowIfDisposed();
        EnsureCapacity(Count+1);
        accessor.Write(GetOffset(Count),ref item);
        Count++;
        OnCountChanged();
    }

    public override void SetTuning(TempListTuning tuning) {
        ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;
        var requiresReopen = MemoryMappedListTuning.RequiresReopen(this.tuning,tuning);
        this.tuning=tuning;
        if(requiresReopen) {
            ReopenMapping();
            return;
        }
        ApplyTuning();
    }

    protected override T GetItem(long index) {
        accessor.Read(GetOffset(index),out T value);
        return value;
    }

    protected override void OnCountChanged() {
        WriteCount();
    }

    protected override void DisposeCore() {
        accessor.Flush();
        accessor.Dispose();
        mappedFile.Dispose();
        stream.Dispose();
    }

    private void EnsureCapacity(long minimumCapacity) {
        if(minimumCapacity<=Capacity)
            return;
        var elementSize = Unsafe.SizeOf<T>();
        var newCapacity = AlignCapacity(Math.Max(Capacity*2,minimumCapacity),elementSize);
        accessor.Flush();
        accessor.Dispose();
        mappedFile.Dispose();
        Capacity=newCapacity;
        stream.SetLength(GetFileLength(newCapacity,elementSize));
        CreateMapping();
        ApplyTuning();
    }

    private void CreateMapping() {
        mappedFile=MemoryMappedFile.CreateFromFile(stream,null,stream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        accessor=mappedFile.CreateViewAccessor(0,stream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void ReopenMapping() {
        accessor.Flush();
        accessor.Dispose();
        mappedFile.Dispose();
        stream.Dispose();
        stream=OpenStream(Path);
        CreateMapping();
        ApplyTuning();
    }

    private FileStream OpenStream(string path) =>
        new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,BufferSize,MemoryMappedListTuning.GetFileOptions(Tuning));

    private void ApplyTuning() =>
        MemoryMappedListTuning.ApplyTuning(stream,accessor,Tuning);

    private long LoadCount() => accessor.ReadInt64(0);
    private void WriteCount() => accessor.Write(0,Count);

    private long GetOffset(long index) => checked(HeaderLength+index*(long)Unsafe.SizeOf<T>());
    private long GetFileLength(long capacity,int elementSize) => checked(HeaderLength+capacity*(long)elementSize);
    private long AlignCapacity(long capacity,int elementSize) =>
        MemoryMappedListTuning.AlignElementCapacity(capacity,elementSize);
}
