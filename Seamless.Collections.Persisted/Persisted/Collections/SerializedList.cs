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
    public override FileAccess OptimizationMode => this.tuning.OptimizationMode;
    public override FileOptions AccessPattern => this.tuning.AccessPattern;

    public SerializedList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialDataCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));

        this.IndexPath=path+".index";
        this.indexStream=this.OpenStream(this.IndexPath);
        this.dataStream=this.OpenStream(this.Path);

        if(this.indexStream.Length==0&&this.dataStream.Length==0) {
            this.Count=0;
            this.Capacity=this.AlignIndexCapacity(initialCapacity);
            this.DataLength=0;
            this.DataCapacity=this.AlignDataCapacity(initialDataCapacity);
            this.indexStream.SetLength(this.GetIndexFileLength(this.Capacity));
            this.dataStream.SetLength(this.GetDataFileLength(this.DataCapacity));
            this.CreateMappings();
            this.InitializeIndexRange(0,this.Capacity);
            this.persistedCount=0;
        }
        else {
            if(this.indexStream.Length==0||this.dataStream.Length==0)
                throw new InvalidDataException("List companion files are incomplete.");
            this.CreateMappings();
            this.LoadState();
        }

        this.ApplyTuning();
    }
    public override void SetTuning(TempListTuning tuning) {
        this.ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;

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
        for(var readIndex = 1L; readIndex<this.Count; readIndex++) {
            var currentOffset = this.ReadIndexUnchecked(readIndex);
            if(this.CompareData(previousOffset,currentOffset)==0)
                continue;
            if(writeIndex!=readIndex) {
                this.WriteIndexUnchecked(writeIndex,currentOffset);
            }
            previousOffset=currentOffset;
            writeIndex++;
        }

        this.Count=writeIndex;
        this.OnCountChanged();
        this.indexAccessor.Flush();
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
        this.indexMappedFile=MemoryMappedFile.CreateFromFile(this.indexStream,null,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.indexAccessor=this.indexMappedFile.CreateViewAccessor(0,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        this.dataMappedFile=MemoryMappedFile.CreateFromFile(this.dataStream,null,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.dataAccessor=this.dataMappedFile.CreateViewAccessor(0,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void ReopenMappings() {
        this.indexAccessor.Flush();
        this.dataAccessor.Flush();
        this.indexAccessor.Dispose();
        this.indexMappedFile.Dispose();
        this.indexStream.Dispose();
        this.dataAccessor.Dispose();
        this.dataMappedFile.Dispose();
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
        this.WriteRecord(recordOffset,bytes);
        this.WriteIndexUnchecked(this.Count,recordOffset);
        this.Count++;
        this.DataLength=recordOffset+recordLength;
        this.OnCountChanged();
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

    private void RemapIndex(long newCapacity) {
        var oldCapacity = this.Capacity;
        this.indexAccessor.Flush();
        this.indexAccessor.Dispose();
        this.indexMappedFile.Dispose();
        this.Capacity=newCapacity;
        this.indexStream.SetLength(this.GetIndexFileLength(newCapacity));
        this.indexMappedFile=MemoryMappedFile.CreateFromFile(this.indexStream,null,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.indexAccessor=this.indexMappedFile.CreateViewAccessor(0,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        this.InitializeIndexRange(oldCapacity,newCapacity);
        this.ApplyTuning();
    }

    private void RemapData(long newCapacity) {
        this.dataAccessor.Flush();
        this.dataAccessor.Dispose();
        this.dataMappedFile.Dispose();
        this.DataCapacity=newCapacity;
        this.dataStream.SetLength(this.GetDataFileLength(newCapacity));
        this.dataMappedFile=MemoryMappedFile.CreateFromFile(this.dataStream,null,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.dataAccessor=this.dataMappedFile.CreateViewAccessor(0,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite);
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

        for(var i = 0; i<minimumLength; i++) {
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
        }
        this.persistedCount=this.Count;
    }

    protected override void DisposeCore() {
        this.indexAccessor.Flush();
        this.dataAccessor.Flush();
        this.indexAccessor.Dispose();
        this.indexMappedFile.Dispose();
        this.indexStream.Dispose();
        this.dataAccessor.Dispose();
        this.dataMappedFile.Dispose();
        this.dataStream.Dispose();
    }

    private T DeserializeAt(long recordOffset) {
        var bytes = this.ReadRecord(recordOffset);
        return TSerializer.Deserialize(bytes);
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

    private FileStream OpenStream(string path) {
        return new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,BufferSize,MemoryMappedListTuning.GetFileOptions(this.Tuning));
    }

    private void ApplyTuning() {
        MemoryMappedListTuning.ApplyTuning(this.indexStream,this.indexAccessor,this.Tuning);
        MemoryMappedListTuning.ApplyTuning(this.dataStream,this.dataAccessor,this.Tuning);
    }

    private long ReadIndexUnchecked(long index) => this.indexAccessor.ReadInt64(this.GetIndexOffset(index));

    private void WriteIndexUnchecked(long index,long recordOffset) {
        this.indexAccessor.Write(this.GetIndexOffset(index),recordOffset);
    }

    private long LoadCount() {
        var count = 0L;
        var foundUnused = false;
        for(var index = 0L; index<this.Capacity; index++) {
            var recordOffset = this.ReadIndexUnchecked(index);
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
        for(var index = 0L; index<this.Count; index++) {
            var recordOffset = this.ReadIndexUnchecked(index);
            var recordLength = this.ReadRecordLength(recordOffset,this.DataCapacity);
            var recordEnd = checked(recordOffset+sizeof(int)+(long)recordLength);
            if(recordEnd>dataLength)
                dataLength=recordEnd;
        }
        return dataLength;
    }

    private void InitializeIndexRange(long startIndex,long endIndexExclusive) {
        for(var index = startIndex; index<endIndexExclusive; index++) {
            this.WriteIndexUnchecked(index,UnusedIndex);
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

    public override FileAccess OptimizationMode => this.tuning.OptimizationMode;
    public override FileOptions AccessPattern => this.tuning.AccessPattern;

    public BlittableList(string path,long initialCapacity = 16) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        this.stream=this.OpenStream(this.Path);
        var elementSize = Unsafe.SizeOf<T>();
        var initializeHeader = this.stream.Length==0;
        if(this.stream.Length==0) {
            this.Count=0;
            this.Capacity=this.AlignCapacity(initialCapacity,elementSize);
            this.stream.SetLength(this.GetFileLength(this.Capacity,elementSize));
        }
        else {
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
    }

    public override void Add(T item) {
        this.ThrowIfDisposed();
        this.EnsureCapacity(this.Count+1);
        this.accessor.Write(this.GetOffset(this.Count),ref item);
        this.Count++;
        this.OnCountChanged();
    }

    public override void SetTuning(TempListTuning tuning) {
        this.ThrowIfDisposed();
        if(this.tuning.Equals(tuning))
            return;
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

    protected override void DisposeCore() {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.Dispose();
    }

    private void EnsureCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var elementSize = Unsafe.SizeOf<T>();
        var newCapacity = this.AlignCapacity(Math.Max(this.Capacity*2,minimumCapacity),elementSize);
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.Capacity=newCapacity;
        this.stream.SetLength(this.GetFileLength(newCapacity,elementSize));
        this.CreateMapping();
        this.ApplyTuning();
    }

    private void CreateMapping() {
        this.mappedFile=MemoryMappedFile.CreateFromFile(this.stream,null,this.stream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.accessor=this.mappedFile.CreateViewAccessor(0,this.stream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void ReopenMapping() {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.Dispose();
        this.stream=this.OpenStream(this.Path);
        this.CreateMapping();
        this.ApplyTuning();
    }

    private FileStream OpenStream(string path) =>
        new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,BufferSize,MemoryMappedListTuning.GetFileOptions(this.Tuning));

    private void ApplyTuning() =>
        MemoryMappedListTuning.ApplyTuning(this.stream,this.accessor,this.Tuning);

    private long LoadCount() => this.accessor.ReadInt64(0);
    private void WriteCount() => this.accessor.Write(0,this.Count);

    private long GetOffset(long index) => checked(HeaderLength+index*(long)Unsafe.SizeOf<T>());
    private long GetFileLength(long capacity,int elementSize) => checked(HeaderLength+capacity*(long)elementSize);
    private long AlignCapacity(long capacity,int elementSize) =>
        MemoryMappedListTuning.AlignElementCapacity(capacity,elementSize);
}
