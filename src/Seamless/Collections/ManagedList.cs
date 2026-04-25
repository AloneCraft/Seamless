using System.Collections;
using System.IO.MemoryMappedFiles;

namespace Seamless.Collections;

public class ManagedList<T>:ManagedList<T,ManagedSerializer<T>> {
    public ManagedList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) :
        base(path,initialCapacity,initialDataCapacity) { }
}

public class ManagedList<T, TSerializer>:List<T>
    where TSerializer : ISerializer<T> {
    private const long IndexMagic = 0x53454D4C49445831;
    private const long DataMagic = 0x53454D4C44415431;
    private const int Version = 1;
    private const long MagicOffset = 0;
    private const long VersionOffset = 8;
    private const long CountOffset = 16;
    private const long CapacityOffset = 24;
    private const long DataLengthOffset = 16;
    private const long DataCapacityOffset = 24;
    private const int HeaderSize = 32;

    private FileStream indexStream;
    private MemoryMappedFile indexMappedFile = null!;
    private MemoryMappedViewAccessor indexAccessor = null!;
    private FileStream dataStream;
    private MemoryMappedFile dataMappedFile = null!;
    private MemoryMappedViewAccessor dataAccessor = null!;

    public string IndexPath { get; }
    public long DataLength { get; private set; }
    public long DataCapacity { get; private set; }

    public ManagedList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        if(initialDataCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));

        this.IndexPath=path+".index";
        this.indexStream=new FileStream(this.IndexPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
        this.dataStream=new FileStream(this.Path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);

        if(this.indexStream.Length==0&&this.dataStream.Length==0) {
            this.Count=0;
            this.Capacity=initialCapacity;
            this.DataLength=0;
            this.DataCapacity=initialDataCapacity;
            this.indexStream.SetLength(this.GetIndexFileLength(this.Capacity));
            this.dataStream.SetLength(this.GetDataFileLength(this.DataCapacity));
            this.CreateMappings();
            this.WriteIndexHeader();
            this.WriteDataHeader();
        }
        else {
            if(this.indexStream.Length==0||this.dataStream.Length==0)
                throw new InvalidDataException("ManagedList companion files are incomplete.");
            this.CreateMappings();
            this.LoadHeaders();
        }
    }

    public void Add(T item) {
        this.ThrowIfDisposed();
        var bytes = TSerializer.Serialize(ref item);
        this.AppendRecord(bytes.ToArray());
    }

    public void Add(scoped ReadOnlySpan<byte> item) {
        this.ThrowIfDisposed();
        this.AppendRecord(item.ToArray());
    }

#if TSAVORITE1_1_1
    public void Add(global::Tsavorite.core.SpanByte item) {
        this.ThrowIfDisposed();
        this.AppendRecord(item.ToByteArray());
    }
#endif

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

    private void LoadHeaders() {
        var indexMagic = this.indexAccessor.ReadInt64(MagicOffset);
        if(indexMagic!=IndexMagic)
            throw new InvalidDataException("The companion index file is not a Seamless.Collections.ManagedList index file.");
        var indexVersion = this.indexAccessor.ReadInt32(VersionOffset);
        if(indexVersion!=Version)
            throw new InvalidDataException($"Unsupported index file version: {indexVersion}.");

        var dataMagic = this.dataAccessor.ReadInt64(MagicOffset);
        if(dataMagic!=DataMagic)
            throw new InvalidDataException("The managed list data file is invalid.");
        var dataVersion = this.dataAccessor.ReadInt32(VersionOffset);
        if(dataVersion!=Version)
            throw new InvalidDataException($"Unsupported data file version: {dataVersion}.");

        this.Count=this.indexAccessor.ReadInt64(CountOffset);
        this.Capacity=this.indexAccessor.ReadInt64(CapacityOffset);
        this.DataLength=this.dataAccessor.ReadInt64(DataLengthOffset);
        this.DataCapacity=this.dataAccessor.ReadInt64(DataCapacityOffset);

        if(this.Count<0||this.Capacity<1||this.Count>this.Capacity)
            throw new InvalidDataException("The managed list index header is invalid.");
        if(this.DataLength<0||this.DataCapacity<1||this.DataLength>this.DataCapacity)
            throw new InvalidDataException("The managed list data header is invalid.");
        if(this.indexStream.Length<this.GetIndexFileLength(this.Capacity))
            throw new InvalidDataException("The managed list index file is truncated.");
        if(this.dataStream.Length<this.GetDataFileLength(this.DataCapacity))
            throw new InvalidDataException("The managed list data file is truncated.");
    }

    private void CreateMappings() {
        this.indexMappedFile=MemoryMappedFile.CreateFromFile(this.indexStream,null,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.indexAccessor=this.indexMappedFile.CreateViewAccessor(0,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        this.dataMappedFile=MemoryMappedFile.CreateFromFile(this.dataStream,null,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.dataAccessor=this.dataMappedFile.CreateViewAccessor(0,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite);
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
        this.WriteDataHeader();
        this.OnCountChanged();
    }

    private void EnsureIndexCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var newCapacity = Math.Max(this.Capacity*2,minimumCapacity);
        this.RemapIndex(newCapacity);
    }

    private void EnsureDataCapacity(long minimumLength) {
        if(minimumLength<=this.DataCapacity)
            return;
        var newCapacity = Math.Max(this.DataCapacity*2,minimumLength);
        this.RemapData(newCapacity);
    }

    private void RemapIndex(long newCapacity) {
        this.indexAccessor.Flush();
        this.indexAccessor.Dispose();
        this.indexMappedFile.Dispose();
        this.Capacity=newCapacity;
        this.indexStream.SetLength(this.GetIndexFileLength(newCapacity));
        this.indexMappedFile=MemoryMappedFile.CreateFromFile(this.indexStream,null,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.indexAccessor=this.indexMappedFile.CreateViewAccessor(0,this.indexStream.Length,MemoryMappedFileAccess.ReadWrite);
        this.WriteIndexHeader();
    }

    private void RemapData(long newCapacity) {
        this.dataAccessor.Flush();
        this.dataAccessor.Dispose();
        this.dataMappedFile.Dispose();
        this.DataCapacity=newCapacity;
        this.dataStream.SetLength(this.GetDataFileLength(newCapacity));
        this.dataMappedFile=MemoryMappedFile.CreateFromFile(this.dataStream,null,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.dataAccessor=this.dataMappedFile.CreateViewAccessor(0,this.dataStream.Length,MemoryMappedFileAccess.ReadWrite);
        this.WriteDataHeader();
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
        this.WriteIndexHeader();
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

    private int ReadRecordLength(long recordOffset) {
        if(recordOffset<0||recordOffset>this.DataLength-sizeof(int))
            throw new InvalidDataException("The managed list record offset is invalid.");

        var length = this.dataAccessor.ReadInt32(this.GetRecordPosition(recordOffset));
        if(length<0||recordOffset+sizeof(int)+(long)length>this.DataLength)
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

    private long GetIndexFileLength(long capacity) => checked(HeaderSize+(capacity*sizeof(long)));
    private long GetDataFileLength(long dataCapacity) => checked(HeaderSize+dataCapacity);
    private long GetIndexOffset(long index) => checked(HeaderSize+(index*sizeof(long)));
    private long GetRecordPosition(long recordOffset) => checked(HeaderSize+recordOffset);

    private long ReadIndexUnchecked(long index) => this.indexAccessor.ReadInt64(this.GetIndexOffset(index));

    private void WriteIndexUnchecked(long index,long recordOffset) {
        this.indexAccessor.Write(this.GetIndexOffset(index),recordOffset);
    }

    private void WriteIndexHeader() {
        this.indexAccessor.Write(MagicOffset,IndexMagic);
        this.indexAccessor.Write(VersionOffset,Version);
        this.indexAccessor.Write(CountOffset,this.Count);
        this.indexAccessor.Write(CapacityOffset,this.Capacity);
    }

    private void WriteDataHeader() {
        this.dataAccessor.Write(MagicOffset,DataMagic);
        this.dataAccessor.Write(VersionOffset,Version);
        this.dataAccessor.Write(DataLengthOffset,this.DataLength);
        this.dataAccessor.Write(DataCapacityOffset,this.DataCapacity);
    }
}
