using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Seamless.Collections;

public enum TempListAccessPattern {
    Sequential,
    Random,
}

public abstract class List<T>:IReadOnlyList<T>, IDisposable {
    public string Path { get; }
    public long Count { get; protected set; }
    public long Capacity { get; protected set; }
    public bool IsDisposed { get; private set; }

    int IReadOnlyCollection<T>.Count => checked((int)this.Count);
    T IReadOnlyList<T>.this[int index] => this[index];

    public T this[int index] => this[(long)index];

    public T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return this.GetItem(index);
        }
    }

    private protected List() {
        this.Path=string.Empty;
    }

    private protected List(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directoryPath = System.IO.Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        this.Path=path;
    }
    public abstract void Add(T item);

    //public abstract void Add(scoped ReadOnlySpan<byte> item);

    //public abstract void Add(global::Tsavorite.core.SpanByte item);

    public bool Remove() {
        this.ThrowIfDisposed();
        if(this.Count==0)
            return false;
        this.Count--;
        this.OnCountChanged();
        return true;
    }

    public Enumerator GetEnumerator() {
        this.ThrowIfDisposed();
        return new(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public void Dispose() {
        if(this.IsDisposed)
            return;
        this.IsDisposed=true;
        this.DisposeCore();
    }

    protected void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(this.IsDisposed,this);
    }

    protected abstract T GetItem(long index);
    protected abstract void OnCountChanged();
    protected abstract void DisposeCore();

    public struct Enumerator(List<T> list,bool ownsList = false,global::System.Action? disposeAction = null):IEnumerator<T> {
        private long index = -1;
        public T Current => list[this.index];
        object IEnumerator.Current => this.Current!;
        public bool MoveNext() {
            var nextIndex = this.index+1;
            if(nextIndex>=list.Count)
                return false;
            this.index=nextIndex;
            return true;
        }
        public void Reset() {
            this.index=-1;
        }
        public readonly void Dispose() {
            if(ownsList) {
                list.Dispose();
            }
            disposeAction?.Invoke();
        }
    }
}

internal static class MemoryMappedListTuning {
    private const int PosixFadvRandom = 1;
    private const int PosixFadvSequential = 2;
    private const int MadvRandom = 1;
    private const int MadvSequential = 2;
    private const int DefaultFileBufferSize = 4096;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Win32MemoryRangeEntry {
        public readonly nuint VirtualAddress;
        public readonly nuint NumberOfBytes;

        public Win32MemoryRangeEntry(nuint virtualAddress,nuint numberOfBytes) {
            this.VirtualAddress=virtualAddress;
            this.NumberOfBytes=numberOfBytes;
        }
    }

    public static FileStream OpenFileStream(string path,TempListAccessPattern accessPattern = TempListAccessPattern.Sequential) =>
        new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,DefaultFileBufferSize,GetFileOptions(accessPattern));

    public static long AlignBytes(long byteLength) {
        if(byteLength<1)
            throw new ArgumentOutOfRangeException(nameof(byteLength));

        var pageSize = Math.Max(Environment.SystemPageSize,1);
        var remainder = byteLength%pageSize;
        return remainder==0
            ? byteLength
            : checked(byteLength+(pageSize-remainder));
    }

    public static long AlignElementCapacity(long capacity,int elementSize) {
        if(capacity<1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if(elementSize<1)
            throw new ArgumentOutOfRangeException(nameof(elementSize));

        var bytes = checked(capacity*(long)elementSize);
        return AlignBytes(bytes)/elementSize;
    }

    public static void ApplyAccessPattern(FileStream stream,MemoryMappedViewAccessor accessor,TempListAccessPattern accessPattern) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(accessor);

        if(accessor.Capacity<=0)
            return;

        if(OperatingSystem.IsWindows()) {
            if(accessPattern==TempListAccessPattern.Sequential) {
                PrefetchWindows(accessor);
            }
            return;
        }

        if(OperatingSystem.IsLinux()||OperatingSystem.IsMacOS()) {
            ApplyPosixFileHint(stream,accessPattern);
            ApplyPosixMemoryHint(accessor,accessPattern);
        }
    }

    private static FileOptions GetFileOptions(TempListAccessPattern accessPattern) => accessPattern switch {
        TempListAccessPattern.Random => FileOptions.RandomAccess,
        _ => FileOptions.SequentialScan,
    };

    private static void ApplyPosixFileHint(FileStream stream,TempListAccessPattern accessPattern) {
        try {
            var fileDescriptor = unchecked((int)stream.SafeFileHandle.DangerousGetHandle());
            var advice = accessPattern==TempListAccessPattern.Sequential ? PosixFadvSequential : PosixFadvRandom;
            _=posix_fadvise(fileDescriptor,0,0,advice);
        }
        catch {
        }
    }

    private static unsafe void ApplyPosixMemoryHint(MemoryMappedViewAccessor accessor,TempListAccessPattern accessPattern) {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try {
            if(pointer is null)
                return;

            var advice = accessPattern==TempListAccessPattern.Sequential ? MadvSequential : MadvRandom;
            _=madvise((nint)pointer,checked((nuint)accessor.Capacity),advice);
        }
        catch {
        }
        finally {
            if(pointer is not null)
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    private static unsafe void PrefetchWindows(MemoryMappedViewAccessor accessor) {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try {
            if(pointer is null)
                return;

            var ranges = new[] {
                new Win32MemoryRangeEntry((nuint)pointer,checked((nuint)accessor.Capacity))
            };
            _=PrefetchVirtualMemory(GetCurrentProcess(),(nuint)ranges.Length,ranges,0);
        }
        catch {
        }
        finally {
            if(pointer is not null)
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    [DllImport("kernel32.dll",SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll",SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrefetchVirtualMemory(IntPtr hProcess,nuint numberOfEntries,[In] Win32MemoryRangeEntry[] virtualAddresses,uint flags);

    [DllImport("libc",SetLastError = true)]
    private static extern int posix_fadvise(int fd,long offset,long length,int advice);

    [DllImport("libc",SetLastError = true)]
    private static extern int madvise(nint addr,nuint length,int advice);
}
