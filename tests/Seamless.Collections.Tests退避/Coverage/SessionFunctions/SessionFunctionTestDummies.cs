using MemoryPack;
using Tsavorite.core;
namespace Seamless.Collections.Tests;

internal sealed class DummyHeapObject:IHeapObject {
    public static int MaxSerializedObjectSize => 0;
    public long HeapMemorySize => 0;
    public IHeapObject Clone() => this;
    public void Serialize(BinaryWriter writer) { }
    public void WriteType(BinaryWriter writer,bool includeNamespace) { }
    public void DoSerialize(BinaryWriter writer) { }
    public void CacheSerializedObjectData(ref LogRecord srcLogRecord,ref LogRecord newLogRecord,ref RMWInfo rmwInfo) { }
    public void ClearSerializedObjectData() { }
    public void Dispose() { }
}

internal unsafe struct DummySourceLogRecord(int key,string value,bool asObject = false):ISourceLogRecord {
    private readonly byte[] KeyData = MemoryPackSerializer.Serialize(key);
    private readonly byte[] ValueData = MemoryPackSerializer.Serialize(value);
    public readonly ReadOnlySpan<byte> Key => this.KeyData;
    public readonly Span<byte> ValueSpan => this.ValueData;
    public readonly RecordInfo Info {
        get {
            if(!asObject) return default;
            var info = default(RecordInfo);
            info.SetValueIsObject();
            return info;
        }
    }
    public readonly ref RecordInfo InfoRef => throw new NotSupportedException();
    public readonly bool IsSet => true;
    public readonly IHeapObject ValueObject => null!;
    public readonly PinnedSpanByte PinnedValueSpan => default;
    public readonly bool HasETag => false;
    public readonly long ETag { get => 0; set { _=value; } }
    public readonly bool HasExpiration => false;
    public readonly long Expiration { get => 0; set { _=value; } }
    public readonly void ClearValueIfHeap(Action<IHeapObject> disposer) { }
    public readonly ref LogRecord AsMemoryLogRecordRef() => throw new NotSupportedException();
    public readonly ref DiskLogRecord AsDiskLogRecordRef() => throw new NotSupportedException();
    public readonly RecordFieldInfo GetRecordFieldInfo() => default;
    public readonly byte RecordType => 0;
    public readonly ReadOnlySpan<byte> Namespace => ReadOnlySpan<byte>.Empty;
    public readonly ObjectIdMap ObjectIdMap => null!;
    public readonly bool IsPinnedKey => false;
    public readonly byte* PinnedKeyPointer => null;
    public OverflowByteArray KeyOverflow { get => default; set { } }
    public readonly bool IsPinnedValue => false;
    public readonly byte* PinnedValuePointer => null;
    public OverflowByteArray ValueOverflow { get => default; set { } }
    public readonly bool IsMemoryLogRecord => true;
    public readonly bool IsDiskLogRecord => false;
    public readonly int AllocatedSize => 0;
    public readonly int ActualSize => 0;
    public readonly long CalculateHeapMemorySize() => 0;
    public readonly bool IsPinned => false;
    public readonly ReadOnlySpan<byte> KeyBytes => this.KeyData;
    public readonly bool HasNamespace => false;
    public readonly ReadOnlySpan<byte> NamespaceBytes => ReadOnlySpan<byte>.Empty;
}
