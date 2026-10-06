using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Seamless.Collections;

/// <summary>Persisted のファイル形式を保ち、設定に応じてメモリマップまたは整列した直接 I/O を使う。</summary>
internal static class PersistedFile {
    public static void ValidateTuning(StorageTuning tuning) {
        if(tuning.BufferingMode==StorageBufferingMode.Unbuffered&&!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("Unbuffered Persisted I/O is supported on Windows.");
        }
    }

    public static Stream Open(string path,StorageTuning tuning,FileMode mode=FileMode.OpenOrCreate,FileAccess access=FileAccess.ReadWrite,FileShare share=FileShare.Read) {
        if(PersistedVirtualFiles.IsVirtual(path)) {
            return PersistedVirtualFiles.Open(path,mode);
        }
        ValidateTuning(tuning);
        if(tuning.BufferingMode==StorageBufferingMode.Unbuffered) {
            return new NativeUnbufferedStream(path,mode,access,share,tuning);
        }
        return new FileStream(path,mode,access,share,tuning.ResolveBufferSize(),MemoryMappedListTuning.GetFileOptions(tuning));
    }

    public static void Dispose(Stream stream,PersistedFileAccessor accessor) {
        try {
            accessor.Flush();
        } finally {
            try {
                accessor.Dispose();
            } finally {
                stream.Dispose();
            }
        }
    }

    // 実ファイルと、メモリ上の仮想ファイル(メモリストア)で同じ操作を使えるようにする。
    public static bool Exists(string path) => PersistedVirtualFiles.IsVirtual(path) ? PersistedVirtualFiles.Exists(path) : File.Exists(path);
    public static long GetLength(string path) => PersistedVirtualFiles.IsVirtual(path) ? PersistedVirtualFiles.GetLength(path) : new FileInfo(path).Length;
    public static void Move(string source,string destination) {
        if(PersistedVirtualFiles.IsVirtual(source)) {
            PersistedVirtualFiles.Move(source,destination);
        } else {
            File.Move(source,destination,overwrite:true);
        }
    }
    public static void Delete(string path) {
        if(PersistedVirtualFiles.IsVirtual(path)) {
            PersistedVirtualFiles.Delete(path);
        } else {
            File.Delete(path);
        }
    }
    /// <summary>ファイルの置き場のフォルダを作る。仮想パスにはフォルダが無いので何もしない。</summary>
    public static void EnsureDirectory(string? directoryPath) {
        if(!string.IsNullOrEmpty(directoryPath)&&!PersistedVirtualFiles.IsVirtual(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public static void FlushToDisk(Stream stream) {
        if(stream is InMemoryFileStream) {
            return;
        }
        if(stream is FileStream file) {
            file.Flush(flushToDisk:true);
        } else if(stream is NativeUnbufferedStream direct) {
            direct.Flush(flushToDisk:true);
        } else {
            throw new ArgumentException("Unknown persisted file backend.",nameof(stream));
        }
    }
}

/// <summary>Buffered では既存のメモリマップを使い、Unbuffered では直接 I/O だけを使う。</summary>
internal sealed class PersistedFileAccessor:IDisposable {
    private readonly MemoryMappedFile? mappedFile;
    private readonly NativeUnbufferedStream? direct;
    private readonly InMemoryFileBuffer? memory;
    internal MemoryMappedViewAccessor? MappedAccessor { get; }

    public PersistedFileAccessor(Stream stream) {
        if(stream is InMemoryFileStream inMemory) {
            this.memory=inMemory.Buffer;
            return;
        }
        if(stream is NativeUnbufferedStream direct) {
            this.direct=direct;
            return;
        }
        if(stream is not FileStream file) {
            throw new ArgumentException("Unknown persisted file backend.",nameof(stream));
        }
        this.mappedFile=MemoryMappedFile.CreateFromFile(file,null,file.Length,MemoryMappedFileAccess.ReadWrite,HandleInheritability.None,leaveOpen:true);
        try {
            this.MappedAccessor=this.mappedFile.CreateViewAccessor(0,file.Length,MemoryMappedFileAccess.ReadWrite);
        } catch {
            this.mappedFile.Dispose();
            throw;
        }
    }

    public long ReadInt64(long position) { this.Read(position,out long value);return value; }
    public ulong ReadUInt64(long position) { this.Read(position,out ulong value);return value; }
    public int ReadInt32(long position) { this.Read(position,out int value);return value; }
    public byte ReadByte(long position) { this.Read(position,out byte value);return value; }
    public void Write(long position,long value) => this.Write(position,ref value);
    public void Write(long position,ulong value) => this.Write(position,ref value);
    public void Write(long position,int value) => this.Write(position,ref value);

    public void Read<T>(long position,out T value) where T:unmanaged {
        if(this.MappedAccessor is { } mapped) {
            mapped.Read(position,out value);
            return;
        }
        if(this.memory is { } memory) {
            value=memory.ReadAt<T>(position);
            return;
        }
        value=default;
        var bytes=MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value,1));
        if(this.direct!.ReadAt(position,bytes)!=bytes.Length) {
            throw new EndOfStreamException();
        }
    }

    public void Write<T>(long position,ref T value) where T:unmanaged {
        if(this.MappedAccessor is { } mapped) {
            mapped.Write(position,ref value);
            return;
        }
        if(this.memory is { } memory) {
            memory.WriteAt(position,ref value);
            return;
        }
        this.direct!.WriteAt(position,MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value,1)));
    }

    public int ReadArray(long position,byte[] bytes,int offset,int count) {
        if(this.MappedAccessor is { } mapped) {
            return mapped.ReadArray(position,bytes,offset,count);
        }
        if(this.memory is { } memory) {
            memory.Span(position,count).CopyTo(bytes.AsSpan(offset,count));
            return count;
        }
        var read=this.direct!.ReadAt(position,bytes.AsSpan(offset,count));
        if(read!=count) {
            throw new EndOfStreamException();
        }
        return read;
    }

    public void WriteArray(long position,byte[] bytes,int offset,int count) {
        if(this.MappedAccessor is { } mapped) {
            mapped.WriteArray(position,bytes,offset,count);
            return;
        }
        if(this.memory is { } memory) {
            bytes.AsSpan(offset,count).CopyTo(memory.Span(position,count));
            return;
        }
        this.direct!.WriteAt(position,bytes.AsSpan(offset,count));
    }

    public void Flush() {
        if(this.MappedAccessor is { } mapped) {
            mapped.Flush();
        } else if(this.memory is null) {
            this.direct!.Flush();
        }
    }

    public void Dispose() {
        this.MappedAccessor?.Dispose();
        this.mappedFile?.Dispose();
    }
}
