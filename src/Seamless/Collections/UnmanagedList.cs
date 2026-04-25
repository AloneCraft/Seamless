using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Seamless.Collections;

public class UnmanagedList<T>:List<T>
    where T : unmanaged {
    private const long Magic = 0x53454D554E4C5354;
    private const int Version = 1;
    private const long MagicOffset = 0;
    private const long VersionOffset = 8;
    private const long ElementSizeOffset = 12;
    private const long CountOffset = 16;
    private const long CapacityOffset = 24;
    private const int HeaderSize = 32;

    private readonly int elementSize = Unsafe.SizeOf<T>();
    private readonly FileStream stream;
    private MemoryMappedFile mappedFile = null!;
    private MemoryMappedViewAccessor accessor = null!;

    public UnmanagedList(string path,long initialCapacity = 16) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        this.stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
        if(this.stream.Length==0) {
            this.Capacity=initialCapacity;
            this.Count=0;
            this.stream.SetLength(this.GetFileLength(this.Capacity));
            this.CreateMapping();
            this.WriteHeader();
        }
        else {
            this.CreateMapping();
            this.LoadHeader();
        }
    }

    public void Add(T item) {
        this.ThrowIfDisposed();
        this.EnsureCapacity(this.Count+1);
        this.WriteUnchecked(this.Count,item);
        this.Count++;
        this.OnCountChanged();
    }

    public T RemoveLast() {
        if(!this.TryRemoveLast(out var item))
            throw new InvalidOperationException("The list is empty.");
        return item;
    }

    public bool TryRemoveLast(out T item) {
        this.ThrowIfDisposed();
        if(this.Count==0) {
            item=default;
            return false;
        }

        var lastIndex = this.Count-1;
        item=this.ReadUnchecked(lastIndex);
        this.Count=lastIndex;
        this.OnCountChanged();
        return true;
    }

    public void Sort() {
        this.ThrowIfDisposed();
        if(this.Count<2)
            return;
        this.SortByBits();
        this.accessor.Flush();
    }

    public void DuplicateRemoval() {
        this.ThrowIfDisposed();
        if(this.Count<2)
            return;

        this.SortByBits();

        var writeIndex = 1L;
        var previous = this.ReadUnchecked(0);
        for(var readIndex = 1L; readIndex<this.Count; readIndex++) {
            var current = this.ReadUnchecked(readIndex);
            if(CompareBits(previous,current)==0)
                continue;
            if(writeIndex!=readIndex) {
                this.WriteUnchecked(writeIndex,current);
            }
            previous=current;
            writeIndex++;
        }

        this.Count=writeIndex;
        this.OnCountChanged();
        this.accessor.Flush();
    }

    private void LoadHeader() {
        var magic = this.accessor.ReadInt64(MagicOffset);
        if(magic!=Magic)
            throw new InvalidDataException("The memory-mapped file is not a Seamless.Collections.UnmanagedList file.");

        var version = this.accessor.ReadInt32(VersionOffset);
        if(version!=Version)
            throw new InvalidDataException($"Unsupported list file version: {version}.");

        var fileElementSize = this.accessor.ReadInt32(ElementSizeOffset);
        if(fileElementSize!=this.elementSize)
            throw new InvalidDataException($"Element size mismatch. File={fileElementSize}, Current={this.elementSize}.");

        this.Count=this.accessor.ReadInt64(CountOffset);
        this.Capacity=this.accessor.ReadInt64(CapacityOffset);
        if(this.Count<0||this.Capacity<1||this.Count>this.Capacity)
            throw new InvalidDataException("The list header is invalid.");
        if(this.stream.Length<this.GetFileLength(this.Capacity))
            throw new InvalidDataException("The list file is truncated.");
    }

    private void CreateMapping() {
        this.mappedFile=MemoryMappedFile.CreateFromFile(this.stream,null,this.stream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.accessor=this.mappedFile.CreateViewAccessor(0,this.stream.Length,MemoryMappedFileAccess.ReadWrite);
    }

    private void EnsureCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var newCapacity = Math.Max(this.Capacity*2,minimumCapacity);
        this.Remap(newCapacity);
    }

    private void Remap(long newCapacity) {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.Capacity=newCapacity;
        this.stream.SetLength(this.GetFileLength(newCapacity));
        this.CreateMapping();
        this.WriteHeader();
    }

    private void SortByBits() {
        var stack = new Stack<(long Left,long Right)>();
        stack.Push((0,this.Count-1));

        while(stack.Count>0) {
            var (leftStart,rightStart)=stack.Pop();
            var left = leftStart;
            var right = rightStart;

            while(left<right) {
                var i = left;
                var j = right;
                var pivot = this.ReadUnchecked(left+((right-left)>>1));

                while(i<=j) {
                    while(CompareBits(this.ReadUnchecked(i),pivot)<0) {
                        i++;
                    }
                    while(CompareBits(this.ReadUnchecked(j),pivot)>0) {
                        j--;
                    }
                    if(i<=j) {
                        if(i!=j) {
                            this.Swap(i,j);
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

    private void Swap(long leftIndex,long rightIndex) {
        var left = this.ReadUnchecked(leftIndex);
        var right = this.ReadUnchecked(rightIndex);
        this.WriteUnchecked(leftIndex,right);
        this.WriteUnchecked(rightIndex,left);
    }

    private static int CompareBits(T left,T right) {
        var leftBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref left,1));
        var rightBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref right,1));
        return leftBytes.SequenceCompareTo(rightBytes);
    }

    protected override T GetItem(long index) => this.ReadUnchecked(index);

    protected override void OnCountChanged() {
        this.WriteHeader();
    }

    protected override void DisposeCore() {
        this.accessor.Flush();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.Dispose();
    }

    private long GetFileLength(long capacity) => checked(HeaderSize+(capacity*this.elementSize));
    private long GetItemOffset(long index) => checked(HeaderSize+(index*this.elementSize));

    private T ReadUnchecked(long index) {
        this.accessor.Read(this.GetItemOffset(index),out T value);
        return value;
    }

    private void WriteUnchecked(long index,T value) {
        this.accessor.Write(this.GetItemOffset(index),ref value);
    }

    private void WriteHeader() {
        this.accessor.Write(MagicOffset,Magic);
        this.accessor.Write(VersionOffset,Version);
        this.accessor.Write(ElementSizeOffset,this.elementSize);
        this.accessor.Write(CountOffset,this.Count);
        this.accessor.Write(CapacityOffset,this.Capacity);
    }
}
