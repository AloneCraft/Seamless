namespace Seamless.Collections.Persisted;

public class AppendOnlyList<T>(string path,long initialDataCapacity=4096,StorageTuning? tuning=null)
    :AppendOnlyList<T,global::Seamless.Collections.Serializers.MemoryPack<T>>(path,initialDataCapacity,tuning);

public class AppendOnlyList<T, TSerializer>:IEnumerable<T>, IDisposable
    where TSerializer : global::Seamless.Collections.Serializers.IStrategy<T> {
    private readonly Stream stream;
    private readonly StorageTuning tuning;
    private Action? disposeAction;
    private bool disposed;

    public string Path { get; }
    public long Count { get; private set; }
    public StorageTuning Tuning => this.tuning;

    public AppendOnlyList(string path,long initialDataCapacity = 4096,StorageTuning? tuning = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if(initialDataCapacity<1) {
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));
        }

        this.tuning=tuning??StorageTuning.Default;
        PersistedFile.EnsureDirectory(global::System.IO.Path.GetDirectoryName(path));

        this.Path=path;
        try {
            this.stream=this.OpenStream(FileAccess.ReadWrite,FileShare.Read);
            if(this.stream.Length==0) {
                this.stream.SetLength(initialDataCapacity);
                this.stream.SetLength(0);
            }
            this.Count=this.LoadCount();
            this.stream.Position=this.stream.Length;
        } catch {
            this.stream?.Dispose();
            throw;
        }
    }

    public long GetLongCount() => this.Count;

    public void Add(T item) {
        this.ThrowIfDisposed();
        var bytes = TSerializer.Serialize(ref item).ToArray();
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,bytes.Length);
        this.stream.Position=this.stream.Length;
        this.stream.Write(lengthBytes);
        this.stream.Write(bytes);
        this.Count++;
    }

    public IEnumerator<T> GetEnumerator() {
        this.ThrowIfDisposed();
        this.stream.Flush();
        using var read = this.OpenStream(FileAccess.Read,FileShare.ReadWrite);
        while(read.Position<read.Length) {
            var lengthBytes = new byte[sizeof(int)];
            if(read.Read(lengthBytes)!=sizeof(int)) {
                throw new InvalidDataException("The append-only list contains a partial length prefix.");
            }
            var length = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if(length<0||read.Position+length>read.Length) {
                throw new InvalidDataException("The append-only list contains an invalid record length.");
            }
            var bytes = new byte[length];
            if(length!=0&&read.Read(bytes)!=length) {
                throw new InvalidDataException("The append-only list contains a partial record.");
            }
            yield return TSerializer.Deserialize(bytes);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        try {
            try {
                this.stream.Flush();
            } finally {
                this.stream.Dispose();
            }
        } finally {
            this.disposeAction?.Invoke();
        }
    }

    internal void SetDisposeAction(Action? action) =>
        this.disposeAction=action;

    private long LoadCount() {
        var count = 0L;
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        this.stream.Position=0;
        while(this.stream.Position<this.stream.Length) {
            if(this.stream.Read(lengthBytes)!=sizeof(int)) {
                throw new InvalidDataException("The append-only list contains a partial length prefix.");
            }
            var length = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if(length<0||this.stream.Position+length>this.stream.Length) {
                throw new InvalidDataException("The append-only list contains an invalid record length.");
            }
            this.stream.Position+=length;
            count++;
        }
        return count;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
    }

    private Stream OpenStream(FileAccess access,FileShare share) =>
        PersistedFile.Open(
            this.Path,
            this.tuning,
            access==FileAccess.Read ? FileMode.Open : FileMode.OpenOrCreate,
            access,
            share);
}
