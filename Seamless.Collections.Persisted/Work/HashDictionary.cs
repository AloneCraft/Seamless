namespace Seamless.Collections.Persisted.Work;

/// <summary>追加・上書き・検索を交互に行う、共通 Store 所有のディスク上の辞書。同じキーは最後の Add が勝つ。</summary>
public sealed class HashDictionary<TKey,TValue>:IReadOnlyDictionary<TKey,TValue>,IDisposable where TKey:notnull{
    private readonly HashStore<KeyValuePair<TKey,TValue>,PairSerializer> store;
    internal HashDictionary(global::Seamless.Execution.Store owner,string name,IEqualityComparer<TKey> comparer,string comparerId,StorageTuning? tuning){
        this.store=new(owner,name,"dictionary",comparerId,(path,quota)=>new Records(path,tuning,new HashKeyPolicy<TKey,global::Seamless.Collections.Serializers.Auto<TKey>>(comparer,owner.Budget),quota));
    }
    public int Count=>checked((int)this.GetLongCount());
    public long GetLongCount()=>this.store.Records.DistinctCount;
    public bool IsDisposed=>this.store.IsDisposed;
    public IEnumerable<TKey> Keys{
        get{
            foreach(var pair in this){
                yield return pair.Key;
            }
        }
    }
    public IEnumerable<TValue> Values{
        get{
            foreach(var pair in this){
                yield return pair.Value;
            }
        }
    }
    public TValue this[TKey key]{
        get{
            if(this.TryGetValue(key,out var value)){
                return value;
            }
            throw new KeyNotFoundException();
        }
        set=>this.Add(key,value);
    }
    public void Add(TKey key,TValue value){
        ArgumentNullException.ThrowIfNull(key);
        var records=this.store.Records;
        var pair=new KeyValuePair<TKey,TValue>(key,value);
        var bytes=PairSerializer.Serialize(ref pair);
        using var memory=this.store.Budget.ReserveMemory(checked(128+3L*bytes.Length));
        records.AddRawBytes(bytes);
    }
    public bool ContainsKey(TKey key){
        ArgumentNullException.ThrowIfNull(key);
        var records=this.store.Records;
        var bytes=global::Seamless.Collections.Serializers.Auto<TKey>.Serialize(ref key);
        using var memory=this.store.Budget.ReserveMemory(checked(128+2L*bytes.Length));
        return records.Lookup(bytes,out _);
    }
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)]out TValue value){
        ArgumentNullException.ThrowIfNull(key);
        var records=this.store.Records;
        var bytes=global::Seamless.Collections.Serializers.Auto<TKey>.Serialize(ref key);
        using var keyMemory=this.store.Budget.ReserveMemory(checked(128+2L*bytes.Length));
        if(records.Lookup(bytes,out var offset)){
            using var recordMemory=this.store.Budget.ReserveMemory(checked(128+2L*records.ByteLengthAt(offset)));
            value=records.ReadAt(offset).Value;
            return true;
        }
        value=default;
        return false;
    }
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator()=>this.store.Enumerate(latestOnly:true).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>this.store.Dispose();

    private sealed class Records(string path,StorageTuning? tuning,IPersistedKeyPolicy policy,IPersistedStorageQuota quota)
        :HashRecords<KeyValuePair<TKey,TValue>,PairSerializer>(path,tuning,policy,quota){
        protected override ReadOnlySpan<byte> ExtractKeyBytes(ReadOnlySpan<byte> payload){
            if(payload.Length<sizeof(int)){
                throw new InvalidDataException("The dictionary key length is missing.");
            }
            var length=System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload);
            if(length<0||length>payload.Length-sizeof(int)){
                throw new InvalidDataException("The dictionary key length is invalid.");
            }
            return payload.Slice(sizeof(int),length);
        }
    }
    private readonly struct PairSerializer:global::Seamless.Collections.Serializers.IStrategy<KeyValuePair<TKey,TValue>>{
        public static ReadOnlySpan<byte> Serialize(ref KeyValuePair<TKey,TValue> pair){
            var entry=new SerializedDictionary<TKey,TValue,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.Auto<TValue>>.Entry(pair.Key,pair.Value);
            return SerializedDictionary<TKey,TValue,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.Auto<TValue>>.EntrySerializer.SerializeOwned(ref entry);
        }
        public static KeyValuePair<TKey,TValue> Deserialize(ReadOnlySpan<byte> bytes){
            var entry=SerializedDictionary<TKey,TValue,global::Seamless.Collections.Serializers.Auto<TKey>,global::Seamless.Collections.Serializers.Auto<TValue>>.EntrySerializer.Deserialize(bytes);
            return new(entry.Key,entry.Value);
        }
    }
}
