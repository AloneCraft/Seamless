namespace Seamless.Collections.Persisted;

internal interface IPersistedDictionary<TKey, TValue>:IReadOnlyDictionary<TKey,TValue>, IDisposable
    where TKey : notnull {
    string Path { get; }
    void Add(TKey key,TValue value);
    IPersistedDictionary<TKey,TValue> Compact();
    void SetDisposeAction(Action? action);
}

public sealed class Dictionary<TKey,TValue>(
    string         path,
    long           initialCapacity    =16,
    long           initialDataCapacity=4096,
    StorageTuning? tuning             =null)
    :IReadOnlyDictionary<TKey,TValue>,IDisposable
where TKey : notnull {
    private readonly IPersistedDictionary<TKey,TValue> dictionary=CreateCore(path,initialCapacity,initialDataCapacity,tuning);
    public           string                            Path   => this.dictionary.Path;
    public           IEnumerable<TKey>                 Keys   => this.dictionary.Keys;
    public           IEnumerable<TValue>               Values => this.dictionary.Values;
    public           int                               Count  => this.dictionary.Count;
    public TValue this[TKey key] => this.dictionary[key];

    public void Add(TKey key,TValue value) =>
        this.dictionary.Add(key,value);

    public Dictionary<TKey,TValue> Compact() {
        this.dictionary.Compact();
        return this;
    }

    public bool ContainsKey(TKey key) =>
        this.dictionary.ContainsKey(key);

    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value) =>
        this.dictionary.TryGetValue(key,out value);

    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() =>
        this.dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        this.GetEnumerator();

    public void Dispose() =>
        this.dictionary.Dispose();

    internal void SetDisposeAction(Action? action) =>
        this.dictionary.SetDisposeAction(action);

    // 実体はどちらも SerializedList 派生なので、保存・復元・予算の口を持つ。
    internal IPersistedCommittable Committable => (IPersistedCommittable)this.dictionary;

    private static IPersistedDictionary<TKey,TValue> CreateCore(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning) {
        // キーと値の両方が固定長なら、AutoSerializer は BlittableDictionary と同じ生バイトを書く(リフレクション不要)。
        // どちらかが参照を含むときは、従来どおり両方とも MemoryPack にしてファイル形式を変えない。
        if(!RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()&&!RuntimeHelpers.IsReferenceOrContainsReferences<TValue>()) {
            return new SerializedDictionary<TKey,TValue,AutoSerializer<TKey>,AutoSerializer<TValue>>(path,initialCapacity,initialDataCapacity,tuning);
        }

        return new SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>(path,initialCapacity,initialDataCapacity,tuning);
    }
}

public class SerializedDictionary<TKey, TValue>
    :SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>
    where TKey : struct
    where TValue : struct {
    public SerializedDictionary(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }

    public SerializedDictionary(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
    }
}

public sealed class BlittableDictionary<TKey, TValue>
    :SerializedDictionary<TKey,TValue,BlittableSerializer<TKey>,BlittableSerializer<TValue>>
    where TKey : unmanaged
    where TValue : unmanaged {
    public BlittableDictionary(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }

    public BlittableDictionary(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
    }
}

public class SerializedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
    :SerializedList<SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>.Entry,SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>.EntrySerializer>, IPersistedDictionary<TKey,TValue>
    where TKey : notnull
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public readonly record struct Entry(TKey Key,TValue Value);

    public readonly struct EntrySerializer:ISerializer<Entry> {
        public static ReadOnlySpan<byte> Serialize(ref Entry value) {
            var key = value.Key;
            var item = value.Value;
            // key/value のバイト列は別々のローカルを指す(または MemoryPack の新規配列)ため
            // 同時に有効。中間の .ToArray() を挟まず直接 record バッファへ書き込む(技法b:確保削減)。
            var keyBytes = TKeySerializer.Serialize(ref key);
            var valueBytes = TValueSerializer.Serialize(ref item);
            var bytes = new byte[sizeof(int)+keyBytes.Length+sizeof(int)+valueBytes.Length];
            var span = bytes.AsSpan();
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span[..sizeof(int)],keyBytes.Length);
            keyBytes.CopyTo(span[sizeof(int)..]);
            var valueLengthOffset = sizeof(int)+keyBytes.Length;
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span.Slice(valueLengthOffset,sizeof(int)),valueBytes.Length);
            valueBytes.CopyTo(span[(valueLengthOffset+sizeof(int))..]);
            return bytes;
        }

        public static Entry Deserialize(ReadOnlySpan<byte> bytes) {
            if(bytes.Length<sizeof(int)*2) {
                throw new InvalidDataException("The persisted dictionary entry is too short.");
            }
            var keyLength = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes[..sizeof(int)]);
            if(keyLength<0||sizeof(int)+keyLength+sizeof(int)>bytes.Length) {
                throw new InvalidDataException("The persisted dictionary entry contains an invalid key length.");
            }
            var keyBytes = bytes.Slice(sizeof(int),keyLength);
            var valueLengthOffset = sizeof(int)+keyLength;
            var valueLength = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(valueLengthOffset,sizeof(int)));
            if(valueLength<0||valueLengthOffset+sizeof(int)+valueLength>bytes.Length) {
                throw new InvalidDataException("The persisted dictionary entry contains an invalid value length.");
            }
            var valueBytes = bytes.Slice(valueLengthOffset+sizeof(int),valueLength);
            return new(TKeySerializer.Deserialize(keyBytes),TValueSerializer.Deserialize(valueBytes));
        }
    }

    public SerializedDictionary(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }

    public SerializedDictionary(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
    }

    // Entry payload=[keyLen][keyBytes][valLen][valBytes]。キーのバイト範囲を索引キーにする。
    protected override bool UsesKeyIndex => true;

    protected override ReadOnlySpan<byte> ExtractKeyBytes(ReadOnlySpan<byte> recordPayload) {
        var keyLength = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(recordPayload[..sizeof(int)]);
        return recordPayload.Slice(sizeof(int),keyLength);
    }

    private static TValue ExtractValue(ReadOnlySpan<byte> recordPayload) {
        var keyLength = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(recordPayload[..sizeof(int)]);
        var valueLengthOffset = sizeof(int)+keyLength;
        var valueLength = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(recordPayload.Slice(valueLengthOffset,sizeof(int)));
        return TValueSerializer.Deserialize(recordPayload.Slice(valueLengthOffset+sizeof(int),valueLength));
    }

    public IEnumerable<TKey> Keys => this.EnumerateLatest().Select(static pair => pair.Key);
    public IEnumerable<TValue> Values => this.EnumerateLatest().Select(static pair => pair.Value);
    // 索引の相異キー数=BCL Dictionary.Count と同義。フル辞書構築を避け O(1) で返す。
    public new int Count => checked((int)this.KeyIndexEntryCount);

    public TValue this[TKey key] => this.TryGetValue(key,out var value)
        ? value
        : throw new KeyNotFoundException();

    public void Add(TKey key,TValue value) => this.Add(new Entry(key,value));

    public bool ContainsKey(TKey key) {
        var keyBytes = TKeySerializer.Serialize(ref key);
        return this.TryLookupKey(keyBytes,out _);
    }

    // O(1) 索引 lookup → 一致時のみ値のみ deserialize(キーは索引側でバイト透過比較=技法d)。
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var keyBytes = TKeySerializer.Serialize(ref key);
        if(this.TryLookupKey(keyBytes,out var recordOffset)) {
            value=ExtractValue(this.ReadRecordPayload(recordOffset));
            return true;
        }
        value=default;
        return false;
    }

    public SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> Compact() {
        var latest = new global::System.Collections.Generic.Dictionary<TKey,TValue>();
        foreach(var entry in this) {
            latest[entry.Key]=entry.Value;
        }
        var entries = latest
            .Select(static pair => new Entry(pair.Key,pair.Value))
            .OrderBy(static entry => entry.Key,new SerializedKeyComparer())
            .ToArray();
        this.Rewrite(entries);
        return this;
    }

    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.EnumerateLatest().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.EnumerateLatest().GetEnumerator();

    // 同じキーを後から Add すると後勝ちで、古いレコードもファイルに残る。全件を辞書に載せる代わりに、
    // レコードを順に読み、キー索引が指す最新のオフセットと一致するレコードだけを返す(メモリに全件を持たない)。
    private IEnumerable<KeyValuePair<TKey,TValue>> EnumerateLatest() {
        // Count(int) は相異キー数を返すよう隠しているので、レコード数は基底の long Count から読む。
        var count = ((PersistedList<Entry>)this).Count;
        for(var index = 0L;index<count;index++) {
            var recordOffset = this.GetRecordOffset(index);
            var payload = this.ReadRecordPayload(recordOffset);
            if(this.TryLookupKey(this.ExtractKeyBytes(payload),out var latestOffset)&&latestOffset==recordOffset) {
                var entry = EntrySerializer.Deserialize(payload);
                yield return new(entry.Key,entry.Value);
            }
        }
    }

    private sealed class SerializedKeyComparer:IComparer<TKey> {
        public int Compare(TKey? x,TKey? y) {
            if(x is null) {
                return y is null ? 0 : -1;
            }
            if(y is null) {
                return 1;
            }
            var left = x;
            var right = y;
            var leftBytes = TKeySerializer.Serialize(ref left);
            var rightBytes = TKeySerializer.Serialize(ref right);
            var length = Math.Min(leftBytes.Length,rightBytes.Length);
            for(var index = 0;index<length;index++) {
                var comparison = leftBytes[index].CompareTo(rightBytes[index]);
                if(comparison!=0) {
                    return comparison;
                }
            }
            return leftBytes.Length.CompareTo(rightBytes.Length);
        }
    }

    IPersistedDictionary<TKey,TValue> IPersistedDictionary<TKey,TValue>.Compact() {
        this.Compact();
        return this;
    }

    void IPersistedDictionary<TKey,TValue>.SetDisposeAction(Action? action) =>
        this.SetDisposeAction(action);
}
