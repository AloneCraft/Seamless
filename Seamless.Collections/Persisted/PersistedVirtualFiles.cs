using System.Collections.Concurrent;
namespace Seamless.Collections;
/// <summary>
/// ファイルを使わないメモリストア用の、メモリ上の仮想ファイル。ネイティブメモリを直接持ち、アクセサはポインタで読み書きする
/// (メモリマップや直接 I/O と同じ「位置を指定して読み書き」で、要素ごとの呼び出しに余計な層を足さない)。
/// 長さの変更は <see cref="Resize"/>(実ファイルの SetLength と同じ。増えた部分は 0 で埋める)。
/// </summary>
internal sealed unsafe class InMemoryFileBuffer:IDisposable {
    private byte* pointer;
    private long length;
    private bool disposed;
    public byte* Pointer => this.pointer;
    public long Length => this.length;
    public void Resize(long newLength) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        ArgumentOutOfRangeException.ThrowIfNegative(newLength);
        if(newLength==this.length) {
            return;
        }
        var resized = (byte*)NativeMemory.Realloc(this.pointer,(nuint)Math.Max(newLength,1));
        if(newLength>this.length) {
            NativeMemory.Clear(resized+this.length,(nuint)(newLength-this.length));
        }
        this.pointer=resized;
        this.length=newLength;
    }
    /// <summary>範囲を確かめて、位置の指す領域を返す(実ファイルの範囲外の読み書きと同じく例外)。</summary>
    public Span<byte> Span(long position,int count) {
        if(position<0||count<0||position+count>this.length) {
            throw new EndOfStreamException();
        }
        return new Span<byte>(this.pointer+position,count);
    }
    public T ReadAt<T>(long position) where T:unmanaged {
        if(position<0||position+sizeof(T)>this.length) {
            throw new EndOfStreamException();
        }
        return Unsafe.ReadUnaligned<T>(this.pointer+position);
    }
    public void WriteAt<T>(long position,ref T value) where T:unmanaged {
        if(position<0||position+sizeof(T)>this.length) {
            throw new EndOfStreamException();
        }
        Unsafe.WriteUnaligned(this.pointer+position,value);
    }
    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        NativeMemory.Free(this.pointer);
        this.pointer=null;
        this.length=0;
    }
}

/// <summary>メモリ上の仮想ファイルの <see cref="Stream"/>。位置・読み書き・長さの変更だけを持つ。ストリームを閉じても、仮想ファイルは削除するまで残る。</summary>
internal sealed unsafe class InMemoryFileStream(InMemoryFileBuffer buffer):Stream {
    private long position;
    public InMemoryFileBuffer Buffer => buffer;
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => buffer.Length;
    public override long Position {
        get => this.position;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            this.position=value;
        }
    }
    public override void Flush() { }
    public override void SetLength(long value) => buffer.Resize(value);
    public override long Seek(long offset,SeekOrigin origin) {
        long next;
        if(origin==SeekOrigin.Begin) {
            next=offset;
        } else if(origin==SeekOrigin.Current) {
            next=this.position+offset;
        } else {
            next=buffer.Length+offset;
        }
        this.Position=next;
        return next;
    }
    public override int Read(byte[] bytes,int offset,int count) => this.Read(bytes.AsSpan(offset,count));
    public override int Read(Span<byte> destination) {
        var available = Math.Max(0,buffer.Length-this.position);
        var count = (int)Math.Min(available,destination.Length);
        new ReadOnlySpan<byte>(buffer.Pointer+this.position,count).CopyTo(destination);
        this.position+=count;
        return count;
    }
    public override void Write(byte[] bytes,int offset,int count) => this.Write(bytes.AsSpan(offset,count));
    public override void Write(ReadOnlySpan<byte> source) {
        var end = checked(this.position+source.Length);
        if(end>buffer.Length) {
            buffer.Resize(end);
        }
        source.CopyTo(new Span<byte>(buffer.Pointer+this.position,source.Length));
        this.position=end;
    }
}

/// <summary>
/// メモリストアの Persisted が使う仮想ファイルの置き場。仮想パスは <see cref="Prefix"/> で始まり、実ファイルの代わりに名前で引く。
/// 開く・存在確認・移動・削除は実ファイルと同じ意味で、各コレクションの根(<see cref="CreateRoot"/>)の破棄でまとめて解放する。
/// </summary>
internal static class PersistedVirtualFiles {
    public const string Prefix = "memory-persisted\\";
    private static readonly ConcurrentDictionary<string,InMemoryFileBuffer> Files = new(StringComparer.Ordinal);
    public static string CreateRoot() => Prefix+Guid.NewGuid().ToString("N");
    public static bool IsVirtual(string path) => path.StartsWith(Prefix,StringComparison.Ordinal);
    public static bool Exists(string path) => Files.ContainsKey(path);
    public static long GetLength(string path) {
        if(Files.TryGetValue(path,out var buffer)) {
            return buffer.Length;
        }
        throw new FileNotFoundException("The in-memory file does not exist.",path);
    }
    public static InMemoryFileStream Open(string path,FileMode mode) {
        InMemoryFileBuffer? buffer;
        switch(mode) {
            case FileMode.CreateNew:
                buffer=new();
                if(!Files.TryAdd(path,buffer)) {
                    buffer.Dispose();
                    throw new IOException($"The in-memory file '{path}' already exists.");
                }
                break;
            case FileMode.Create:
            case FileMode.Truncate:
                buffer=Files.GetOrAdd(path,static _ => new());
                buffer.Resize(0);
                break;
            case FileMode.Open:
                if(!Files.TryGetValue(path,out buffer)) {
                    throw new FileNotFoundException("The in-memory file does not exist.",path);
                }
                break;
            default:
                buffer=Files.GetOrAdd(path,static _ => new());
                break;
        }
        return new(buffer);
    }
    /// <summary>名前を変える。移動先に仮想ファイルがあれば置き換える(置き換えた側のバッファは解放する。開いているストリームが無いときに呼ぶ)。</summary>
    public static void Move(string source,string destination) {
        if(!Files.TryRemove(source,out var buffer)) {
            throw new FileNotFoundException("The in-memory file does not exist.",source);
        }
        InMemoryFileBuffer? replaced = null;
        Files.AddOrUpdate(destination,buffer,(_,existing) => {
            replaced=existing;
            return buffer;
        });
        replaced?.Dispose();
    }
    public static void Delete(string path) {
        if(Files.TryRemove(path,out var buffer)) {
            buffer.Dispose();
        }
    }
    /// <summary>根の下の仮想ファイルをすべて解放する。</summary>
    public static void DeleteRoot(string root) {
        var prefix = root+"\\";
        foreach(var path in Files.Keys) {
            if(path.StartsWith(prefix,StringComparison.Ordinal)) {
                Delete(path);
            }
        }
    }
}
