using global::System.IO.MemoryMappedFiles;

namespace Seamless.Persisted.Collections;

public readonly record struct TempListTuning(FileAccess OptimizationMode,FileOptions accessPattern) {
    public FileAccess OptimizationMode { get; } = ValidateOptimizationMode(OptimizationMode);
    public FileOptions AccessPattern { get; } = ValidateAccessPattern(accessPattern);

    private static FileAccess ValidateOptimizationMode(FileAccess optimizationMode) =>
        optimizationMode is FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite
            ? optimizationMode
            : throw new ArgumentOutOfRangeException(nameof(optimizationMode));

    private static FileOptions ValidateAccessPattern(FileOptions accessPattern) {
        const FileOptions supportedAccessPatterns = FileOptions.SequentialScan|FileOptions.RandomAccess;

        if((accessPattern&~supportedAccessPatterns)!=0)
            throw new ArgumentOutOfRangeException(nameof(accessPattern));
        if(accessPattern==supportedAccessPatterns)
            throw new ArgumentException("SequentialScan and RandomAccess cannot be combined.",nameof(accessPattern));
        return accessPattern;
    }
}

public abstract class PersistedList<T>:IReadOnlyList<T>, IDisposable {
    private protected const int BufferSize = 4096;
    public string Path { get; }
    public long Count { get; protected set; }
    public long Capacity { get; protected set; }
    public bool IsDisposed { get; private set; }

    int IReadOnlyCollection<T>.Count => checked((int)this.Count);
    T IReadOnlyList<T>.this[int index] => this[index];

    // out 引数を持つため FLinq の傍受対象外。System.Linq.Enumerable は IReadOnlyCollection<T> を
    // 高速 count として認識しないため、インスタンスメソッドで拡張メソッドを上書きして O(1) で返す。
    public bool TryGetNonEnumeratedCount(out int count){
        count=checked((int)this.Count);
        return true;
    }

    public T this[int index] => this[(long)index];

    public T this[long index] {
        get {
            this.ThrowIfDisposed();
            if((ulong)index>=(ulong)this.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return this.GetItem(index);
        }
    }

    public ReadOnlySpan<T> AsSpan(long start) {
        this.ThrowIfDisposed();
        if((ulong)start>(ulong)this.Count)
            throw new ArgumentOutOfRangeException(nameof(start));
        return this.AsSpan(start,checked((int)(this.Count-start)));
    }

    public virtual ReadOnlySpan<T> AsSpan(long start,int length) {
        this.ThrowIfDisposed();
        this.ValidateSpanRange(start,length);
        if(length==0)
            return ReadOnlySpan<T>.Empty;

        var items = new T[length];
        for(var index = 0; index<length; index++) {
            items[index]=this.GetItem(start+index);
        }
        return items;
    }

    protected PersistedList() {
        this.Path=string.Empty;
    }

    protected PersistedList(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directoryPath = global::System.IO.Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        this.Path=path;
    }
    public long GetLongCount() => this.Count;
    public abstract void Add(T item);

    public abstract FileAccess OptimizationMode { get; }
    public abstract FileOptions AccessPattern { get; }
    public TempListTuning Tuning => new(this.OptimizationMode,this.AccessPattern);
    public void SetOptimizationMode(FileAccess optimizationMode) {
        this.SetTuning(new(optimizationMode,this.AccessPattern));
    }

    public void SetAccessPattern(FileOptions accessPattern) {
        this.SetTuning(new(this.OptimizationMode,accessPattern));
    }

    public abstract void SetTuning(TempListTuning tuning);

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

    protected void ValidateSpanRange(long start,int length) {
        if(start<0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if(length<0)
            throw new ArgumentOutOfRangeException(nameof(length));
        if(start>this.Count)
            throw new ArgumentOutOfRangeException(nameof(start));
        if(length>this.Count-start)
            throw new ArgumentOutOfRangeException(nameof(length));
    }

    protected abstract T GetItem(long index);
    protected abstract void OnCountChanged();
    protected abstract void DisposeCore();

    public struct Enumerator(PersistedList<T> list,bool ownsList = false,Action? disposeAction = null):IEnumerator<T> {
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
    private const int PosixFadvNormal = 0;
    private const int PosixFadvRandom = 1;
    private const int PosixFadvSequential = 2;
    private const int PosixFadvWillNeed = 3;
    private const int MadvNormal = 0;
    private const int MadvRandom = 1;
    private const int MadvSequential = 2;
    private const int MadvWillNeed = 3;

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Win32MemoryRangeEntry(nuint virtualAddress,nuint numberOfBytes) {
        public readonly nuint VirtualAddress = virtualAddress;
        public readonly nuint NumberOfBytes = numberOfBytes;
    }
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

    public static FileOptions GetFileOptions(TempListTuning tuning) {
        if(!OperatingSystem.IsWindows())
            return FileOptions.None;

        return tuning.AccessPattern;
    }

    public static bool RequiresReopen(TempListTuning currentTuning,TempListTuning nextTuning) {
        return OperatingSystem.IsWindows()
            &&currentTuning.AccessPattern!=nextTuning.AccessPattern;
    }

    public static void ApplyTuning(FileStream stream,MemoryMappedViewAccessor accessor,TempListTuning tuning) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(accessor);

        if(accessor.Capacity<=0)
            return;

        if(OperatingSystem.IsWindows()) {
            if(tuning.OptimizationMode==FileAccess.Read
                &&tuning.AccessPattern==FileOptions.SequentialScan) {
                PrefetchWindows(accessor);
            }
            return;
        }

        if(OperatingSystem.IsLinux()||OperatingSystem.IsMacOS()) {
            ApplyPosixFileHint(stream,tuning);
            ApplyPosixMemoryHint(accessor,tuning);
        }
    }


    private static void ApplyPosixFileHint(FileStream stream,TempListTuning tuning) {
        try {
            var fileDescriptor = unchecked((int)stream.SafeFileHandle.DangerousGetHandle());
            var advice = tuning.OptimizationMode==FileAccess.Write
                ? PosixFadvNormal
                : GetPosixFileAdvice(tuning.AccessPattern);
            _=posix_fadvise(fileDescriptor,0,0,advice);
            if(tuning.OptimizationMode==FileAccess.Read)
                _=posix_fadvise(fileDescriptor,0,0,PosixFadvWillNeed);
        }
        catch {
        }
    }

    private static unsafe void ApplyPosixMemoryHint(MemoryMappedViewAccessor accessor,TempListTuning tuning) {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try {
            if(pointer is null)
                return;

            var advice = tuning.OptimizationMode==FileAccess.Write
                ? MadvNormal
                : GetPosixMemoryAdvice(tuning.AccessPattern);
            _=madvise((nint)pointer,checked((nuint)accessor.Capacity),advice);
            if(tuning.OptimizationMode==FileAccess.Read)
                _=madvise((nint)pointer,checked((nuint)accessor.Capacity),MadvWillNeed);
        }
        catch {
        }
        finally {
            if(pointer is not null)
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    private static int GetPosixFileAdvice(FileOptions accessPattern) => accessPattern switch {
        FileOptions.SequentialScan => PosixFadvSequential,
        FileOptions.RandomAccess => PosixFadvRandom,
        _ => PosixFadvNormal,
    };

    private static int GetPosixMemoryAdvice(FileOptions accessPattern) => accessPattern switch {
        FileOptions.SequentialScan => MadvSequential,
        FileOptions.RandomAccess => MadvRandom,
        _ => MadvNormal,
    };

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
