using System.IO.MemoryMappedFiles;

namespace Seamless.Persisted.Collections;

public enum StorageAccessPattern {
    Auto,
    Sequential,
    Random,
}

public enum StorageBufferingMode {
    Auto,
    Buffered,
    Unbuffered,
}

public enum StorageBufferSizeStrategy {
    Auto,
    Explicit,
}

public readonly record struct StorageTuning {
    public static StorageTuning Default => new(FileAccess.ReadWrite,StorageAccessPattern.Sequential);

    public FileAccess OptimizationMode { get; }
    public StorageAccessPattern AccessPattern { get; }
    public StorageBufferingMode BufferingMode { get; }
    public StorageBufferSizeStrategy BufferSizeStrategy { get; }
    public int BufferSize { get; }

    public StorageTuning(
        FileAccess optimizationMode,
        StorageAccessPattern accessPattern = StorageAccessPattern.Sequential,
        StorageBufferingMode bufferingMode = StorageBufferingMode.Auto,
        StorageBufferSizeStrategy bufferSizeStrategy = StorageBufferSizeStrategy.Auto,
        int bufferSize = 0) {
        this.OptimizationMode=ValidateOptimizationMode(optimizationMode);
        this.AccessPattern=ValidateAccessPattern(accessPattern);
        this.BufferingMode=ValidateBufferingMode(bufferingMode);
        this.BufferSizeStrategy=ValidateBufferSizeStrategy(bufferSizeStrategy);
        this.BufferSize=ValidateBufferSize(bufferSizeStrategy,bufferSize);
    }

    private static FileAccess ValidateOptimizationMode(FileAccess optimizationMode) =>
        optimizationMode is FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite
            ? optimizationMode
            : throw new ArgumentOutOfRangeException(nameof(optimizationMode));

    private static StorageAccessPattern ValidateAccessPattern(StorageAccessPattern accessPattern) =>
        Enum.IsDefined(accessPattern)
            ? accessPattern
            : throw new ArgumentOutOfRangeException(nameof(accessPattern));

    private static StorageBufferingMode ValidateBufferingMode(StorageBufferingMode bufferingMode) =>
        Enum.IsDefined(bufferingMode)
            ? bufferingMode
            : throw new ArgumentOutOfRangeException(nameof(bufferingMode));

    private static StorageBufferSizeStrategy ValidateBufferSizeStrategy(StorageBufferSizeStrategy bufferSizeStrategy) =>
        Enum.IsDefined(bufferSizeStrategy)
            ? bufferSizeStrategy
            : throw new ArgumentOutOfRangeException(nameof(bufferSizeStrategy));

    private static int ValidateBufferSize(StorageBufferSizeStrategy strategy,int bufferSize) {
        if(strategy==StorageBufferSizeStrategy.Auto) {
            if(bufferSize!=0)
                throw new ArgumentException("Auto buffer sizing does not accept an explicit buffer size.",nameof(bufferSize));
            return 0;
        }

        if(bufferSize<1)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        return bufferSize;
    }

    public int ResolveBufferSize(uint sectorSize = 0) {
        if(this.BufferSizeStrategy==StorageBufferSizeStrategy.Explicit)
            return this.BufferSize;

        var sectorAlignment = sectorSize==0
            ? 1
            : checked((int)Math.Min((uint)int.MaxValue,sectorSize));
        var alignment = Math.Max(4096,Math.Max(Environment.SystemPageSize,sectorAlignment));
        return this.AccessPattern switch {
            StorageAccessPattern.Random => Math.Min(64*1024,alignment),
            _ => Math.Min(1024*1024,Math.Max(64*1024,alignment*16)),
        };
    }

    public FileOptions ToFileOptions() {
        if(this.BufferingMode==StorageBufferingMode.Unbuffered)
            throw new NotSupportedException("Unbuffered Persisted FileStream access is not supported by this backend.");
        return this.AccessPattern switch {
            StorageAccessPattern.Sequential => FileOptions.SequentialScan,
            StorageAccessPattern.Random => FileOptions.RandomAccess,
            _ => FileOptions.None,
        };
    }
}

public abstract class PersistedList<T>:IReadOnlyList<T>, IDisposable {
    private Action? disposeAction;
    public string Path { get; }
    public long Count { get; protected set; }
    public long Capacity { get; protected set; }
    public bool IsDisposed { get; private set; }

    int IReadOnlyCollection<T>.Count => checked((int)this.Count);
    T IReadOnlyList<T>.this[int index] => this[index];

    // out 引数を持つため FLinq の傍受対象外。System.Linq.Enumerable は IReadOnlyCollection<T> を
    // 高速 count として認識しないため、インスタンスメソッドで拡張メソッドを上書きして O(1) で返す。
    public bool TryGetNonEnumeratedCount(out int count) {
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
        for(var index = 0;index<length;index++) {
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

    public abstract StorageTuning Tuning { get; }
    public FileAccess OptimizationMode => this.Tuning.OptimizationMode;
    public StorageAccessPattern AccessPattern => this.Tuning.AccessPattern;
    public StorageBufferingMode BufferingMode => this.Tuning.BufferingMode;
    public int BufferSize => this.Tuning.ResolveBufferSize();
    public void SetOptimizationMode(FileAccess optimizationMode) {
        this.SetTuning(new(optimizationMode,this.AccessPattern,this.BufferingMode,this.Tuning.BufferSizeStrategy,this.Tuning.BufferSize));
    }

    public void SetAccessPattern(StorageAccessPattern accessPattern) {
        this.SetTuning(new(this.OptimizationMode,accessPattern,this.BufferingMode,this.Tuning.BufferSizeStrategy,this.Tuning.BufferSize));
    }

    public void SetBufferingMode(StorageBufferingMode bufferingMode) {
        this.SetTuning(new(this.OptimizationMode,this.AccessPattern,bufferingMode,this.Tuning.BufferSizeStrategy,this.Tuning.BufferSize));
    }

    public abstract void SetTuning(StorageTuning tuning);

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
        try {
            this.DisposeCore();
        } finally {
            this.disposeAction?.Invoke();
        }
    }

    internal void SetDisposeAction(Action? action) =>
        this.disposeAction=action;

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

    public static FileOptions GetFileOptions(StorageTuning tuning) {
        if(!OperatingSystem.IsWindows())
            return FileOptions.None;

        return tuning.ToFileOptions();
    }

    public static bool RequiresReopen(StorageTuning currentTuning,StorageTuning nextTuning) {
        return OperatingSystem.IsWindows()
            &&(currentTuning.AccessPattern!=nextTuning.AccessPattern
                ||currentTuning.BufferingMode!=nextTuning.BufferingMode
                ||currentTuning.ResolveBufferSize()!=nextTuning.ResolveBufferSize());
    }

    public static void ApplyTuning(FileStream stream,MemoryMappedViewAccessor accessor,StorageTuning tuning) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(accessor);

        if(accessor.Capacity<=0)
            return;

        if(OperatingSystem.IsWindows()) {
            if(tuning.OptimizationMode==FileAccess.Read
                &&tuning.AccessPattern==StorageAccessPattern.Sequential) {
                PrefetchWindows(accessor);
            }
            return;
        }

        if(OperatingSystem.IsLinux()||OperatingSystem.IsMacOS()) {
            ApplyPosixFileHint(stream,tuning);
            ApplyPosixMemoryHint(accessor,tuning);
        }
    }


    private static void ApplyPosixFileHint(FileStream stream,StorageTuning tuning) {
        try {
            var fileDescriptor = unchecked((int)stream.SafeFileHandle.DangerousGetHandle());
            var advice = tuning.OptimizationMode==FileAccess.Write
                ? PosixFadvNormal
                : GetPosixFileAdvice(tuning.AccessPattern);
            _=posix_fadvise(fileDescriptor,0,0,advice);
            if(tuning.OptimizationMode==FileAccess.Read)
                _=posix_fadvise(fileDescriptor,0,0,PosixFadvWillNeed);
        } catch {
        }
    }

    private static unsafe void ApplyPosixMemoryHint(MemoryMappedViewAccessor accessor,StorageTuning tuning) {
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
        } catch {
        } finally {
            if(pointer is not null)
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    private static int GetPosixFileAdvice(StorageAccessPattern accessPattern) => accessPattern switch {
        StorageAccessPattern.Sequential => PosixFadvSequential,
        StorageAccessPattern.Random => PosixFadvRandom,
        _ => PosixFadvNormal,
    };

    private static int GetPosixMemoryAdvice(StorageAccessPattern accessPattern) => accessPattern switch {
        StorageAccessPattern.Sequential => MadvSequential,
        StorageAccessPattern.Random => MadvRandom,
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
        } catch {
        } finally {
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
