namespace Seamless.Persisted.Collections;

public class AppendOnlyList<T>:AppendOnlyList<T,MemoryPackSerializer<T>> {
    public AppendOnlyList(string path,long initialDataCapacity = 4096)
        : base(path,initialDataCapacity) {
    }
}

public class AppendOnlyList<T, TSerializer>:IEnumerable<T>, IDisposable
    where TSerializer : ISerializer<T> {
    private readonly FileStream stream;
    private bool disposed;

    public string Path { get; }
    public long Count { get; private set; }

    public AppendOnlyList(string path,long initialDataCapacity = 4096) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if(initialDataCapacity<1) {
            throw new ArgumentOutOfRangeException(nameof(initialDataCapacity));
        }

        var directoryPath = global::System.IO.Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        Path=path;
        stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,4096,FileOptions.SequentialScan);
        if(stream.Length==0) {
            stream.SetLength(initialDataCapacity);
            stream.SetLength(0);
        }
        Count=LoadCount();
        stream.Position=stream.Length;
    }

    public long GetLongCount() => Count;

    public void Add(T item) {
        ThrowIfDisposed();
        var bytes = TSerializer.Serialize(ref item).ToArray();
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,bytes.Length);
        stream.Position=stream.Length;
        stream.Write(lengthBytes);
        stream.Write(bytes);
        Count++;
    }

    public IEnumerator<T> GetEnumerator() {
        ThrowIfDisposed();
        stream.Flush();
        using var read = new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,4096,FileOptions.SequentialScan);
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

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() {
        if(disposed) {
            return;
        }
        disposed=true;
        stream.Flush();
        stream.Dispose();
    }

    private long LoadCount() {
        var count = 0L;
        stream.Position=0;
        while(stream.Position<stream.Length) {
            Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
            if(stream.Read(lengthBytes)!=sizeof(int)) {
                throw new InvalidDataException("The append-only list contains a partial length prefix.");
            }
            var length = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
            if(length<0||stream.Position+length>stream.Length) {
                throw new InvalidDataException("The append-only list contains an invalid record length.");
            }
            stream.Position+=length;
            count++;
        }
        return count;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(disposed,this);
    }
}