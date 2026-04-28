using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Seamless.Collections;

public class UnmanagedList<T>:List<T>
    where T : unmanaged {
    private readonly int elementSize = Unsafe.SizeOf<T>();
    private FileStream stream = null!;
    private MemoryMappedFile mappedFile = null!;
    private MemoryMappedViewAccessor accessor = null!;
    private unsafe byte* pointer;
    private TempListTuning tuning = new(FileAccess.ReadWrite,FileOptions.SequentialScan);

    public override FileAccess OptimizationMode => this.tuning.OptimizationMode;
    public override FileOptions AccessPattern => this.tuning.AccessPattern;

    internal UnmanagedList(string path,long initialCapacity = 16) : base(path) {
        if(initialCapacity<1)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        this.stream=this.OpenStream();
        if(this.stream.Length==0) {
            this.Count=0;
            this.Capacity=this.AlignCapacity(initialCapacity);
            this.stream.SetLength(this.GetFileLength(this.Capacity));
            this.CreateMapping();
        }
        else {
            this.CreateMapping();
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
            this.ReopenMapping();
            return;
        }

        this.ApplyTuning();
    }

    public override void Add(T item) {
        this.EnsureCapacity(this.Count+1);
        this.WriteUnchecked(this.Count,item);
        this.Count++;
    }

    public new ref T this[int index] => ref this[checked((long)index)];

    public new ref T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref this.GetItemReferenceUnchecked(index);
        }
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

    public override ReadOnlySpan<T> AsSpan(long start,int length) {
        this.ThrowIfDisposed();
        this.ValidateSpanRange(start,length);
        if(length==0)
            return ReadOnlySpan<T>.Empty;

        var items = new T[length];
        var readCount = this.accessor.ReadArray(this.GetItemOffset(start),items,0,length);
        if(readCount!=length)
            throw new EndOfStreamException("Failed to read the requested span.");
        return items;
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

    private void LoadState() {
        if(this.stream.Length%this.elementSize!=0)
            throw new InvalidDataException("The unmanaged list file length is invalid.");

        this.Capacity=this.stream.Length/this.elementSize;
        this.Count=this.Capacity;
    }

    private void CreateMapping() {
        this.mappedFile=MemoryMappedFile.CreateFromFile(this.stream,null,this.stream.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen: true);
        this.accessor=this.mappedFile.CreateViewAccessor(0,this.stream.Length,MemoryMappedFileAccess.ReadWrite);
        this.AcquirePointer();
    }

    private void ReopenMapping() {
        this.accessor.Flush();
        this.ReleasePointer();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.Dispose();
        this.stream=this.OpenStream();
        this.CreateMapping();
        this.ApplyTuning();
    }

    private void EnsureCapacity(long minimumCapacity) {
        if(minimumCapacity<=this.Capacity)
            return;
        var newCapacity = this.AlignCapacity(Math.Max(this.Capacity*2,minimumCapacity));
        this.Remap(newCapacity);
    }

    private void Remap(long newCapacity) {
        this.accessor.Flush();
        this.ReleasePointer();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.Capacity=newCapacity;
        this.stream.SetLength(this.GetFileLength(newCapacity));
        this.CreateMapping();
        this.ApplyTuning();
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

    protected override T GetItem(long index) => this.GetItemReferenceUnchecked(index);

    protected override void OnCountChanged() {
    }

    protected override void DisposeCore() {
        this.accessor.Flush();
        this.ReleasePointer();
        this.accessor.Dispose();
        this.mappedFile.Dispose();
        this.stream.SetLength(this.GetFileLength(this.Count));
        this.stream.Dispose();
    }

    private long AlignCapacity(long capacity) => MemoryMappedListTuning.AlignElementCapacity(capacity,this.elementSize);
    private long GetFileLength(long capacity) => checked(capacity*this.elementSize);
    private long GetItemOffset(long index) => checked(index*this.elementSize);

    private FileStream OpenStream() {
        return new(this.Path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,BufferSize,MemoryMappedListTuning.GetFileOptions(this.Tuning));
    }

    private void ApplyTuning() {
        MemoryMappedListTuning.ApplyTuning(this.stream,this.accessor,this.Tuning);
    }

    private unsafe void AcquirePointer() {
        byte* pointer = null;
        this.accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        this.pointer=pointer;
    }

    private unsafe void ReleasePointer() {
        if(this.pointer is null)
            return;
        this.accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        this.pointer=null;
    }

    private unsafe ref T GetItemReferenceUnchecked(long index) {
        if(this.pointer is null)
            throw new InvalidOperationException("The list is not mapped.");

        ref var firstByte = ref Unsafe.AsRef<byte>(this.pointer);
        return ref Unsafe.As<byte,T>(ref Unsafe.AddByteOffset(ref firstByte,(nint)this.GetItemOffset(index)));
    }

    private T ReadUnchecked(long index) => this.GetItemReferenceUnchecked(index);

    private void WriteUnchecked(long index,T value) {
        this.GetItemReferenceUnchecked(index)=value;
    }
}
