namespace Seamless.Persisted.Collections;

public class SerializedDictionary<TKey,TValue>
    : SerializedDictionary<TKey,TValue,MemoryPackSerializer<TKey>,MemoryPackSerializer<TValue>>
    where TKey : struct
    where TValue : struct {
    public SerializedDictionary(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }
}

public sealed class BlittableDictionary<TKey,TValue>
    : SerializedDictionary<TKey,TValue,BlittableSerializer<TKey>,BlittableSerializer<TValue>>
    where TKey : unmanaged
    where TValue : unmanaged {
    public BlittableDictionary(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }
}

public class SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>
    : SerializedList<SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>.Entry,SerializedDictionary<TKey,TValue,TKeySerializer,TValueSerializer>.EntrySerializer>, IReadOnlyDictionary<TKey,TValue>
    where TKey : notnull
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public readonly record struct Entry(TKey Key,TValue Value);

    public readonly struct EntrySerializer:ISerializer<Entry> {
        public static ReadOnlySpan<byte> Serialize(ref Entry value) {
            var key = value.Key;
            var item = value.Value;
            var keyBytes = TKeySerializer.Serialize(ref key).ToArray();
            var valueBytes = TValueSerializer.Serialize(ref item).ToArray();
            var bytes = new byte[sizeof(int)+keyBytes.Length+sizeof(int)+valueBytes.Length];
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0,sizeof(int)),keyBytes.Length);
            keyBytes.CopyTo(bytes.AsSpan(sizeof(int)));
            var valueLengthOffset = sizeof(int)+keyBytes.Length;
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(valueLengthOffset,sizeof(int)),valueBytes.Length);
            valueBytes.CopyTo(bytes.AsSpan(valueLengthOffset+sizeof(int)));
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

    public IEnumerable<TKey> Keys => this.EnumerateLatest().Select(static pair => pair.Key);
    public IEnumerable<TValue> Values => this.EnumerateLatest().Select(static pair => pair.Value);
    public new int Count => checked((int)this.EnumerateLatest().LongCount());

    public TValue this[TKey key] => this.TryGetValue(key,out var value)
        ? value
        : throw new KeyNotFoundException();

    public void Add(TKey key,TValue value) => this.Add(new Entry(key,value));

    public bool ContainsKey(TKey key) => this.TryGetValue(key,out _);

    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TValue value) {
        var comparer = EqualityComparer<TKey>.Default;
        for(var index = this.GetLongCount()-1; index>=0; index--) {
            var entry = this[index];
            if(comparer.Equals(entry.Key,key)) {
                value=entry.Value;
                return true;
            }
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

    private IEnumerable<KeyValuePair<TKey,TValue>> EnumerateLatest() {
        var latest = new global::System.Collections.Generic.Dictionary<TKey,TValue>();
        foreach(var entry in (IEnumerable<Entry>)this) {
            latest[entry.Key]=entry.Value;
        }
        foreach(var pair in latest) {
            yield return pair;
        }
    }

    private sealed class SerializedKeyComparer:IComparer<TKey> {
        public int Compare(TKey x,TKey y) {
            var left = x;
            var right = y;
            var leftBytes = TKeySerializer.Serialize(ref left);
            var rightBytes = TKeySerializer.Serialize(ref right);
            var length = Math.Min(leftBytes.Length,rightBytes.Length);
            for(var index = 0; index<length; index++) {
                var comparison = leftBytes[index].CompareTo(rightBytes[index]);
                if(comparison!=0) {
                    return comparison;
                }
            }
            return leftBytes.Length.CompareTo(rightBytes.Length);
        }
    }
}