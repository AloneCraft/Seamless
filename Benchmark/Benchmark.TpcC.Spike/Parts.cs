using System.Runtime.InteropServices;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Spike;
/// <summary>実験が期待と違う結果を見たときの例外。</summary>
public sealed class SpikeFailure(string message):Exception(message);
/// <summary>区画の中のデータ本体 1 つ(表か索引)。模型(標準の辞書)を持たせると、読むたびに模型と照合する。</summary>
public interface IPart:IDisposable{
    string Name{get;}
    /// <summary>操作用オブジェクトを閉じる。</summary>
    void Release();
    /// <summary>操作用オブジェクトを開く(今のスレッドで)。</summary>
    void Acquire();
    /// <summary>データ本体を全走査した要約。</summary>
    Digest Scan();
    /// <summary>模型の要約。模型が無ければ例外。</summary>
    Digest Expected{get;}
    /// <summary>データ本体が持つ件数(分からなければ全走査で数える)。</summary>
    long StoreCount();
    /// <summary>データ本体の中身で模型を作り直す(開き直した直後に使う)。</summary>
    void RebuildMirror();
    /// <summary>ログの位置(先頭・メモリの先頭・読み取り専用の境・末尾)。</summary>
    (long Begin,long Head,long ReadOnly,long Tail) Address{get;}
}
public sealed class TableStore<TPad>:IPart where TPad:unmanaged{
    private readonly SetStore<K4,Row<TPad>> data;
    private Set<K4,Row<TPad>>? handle;
    private readonly Dictionary<K4,Row<TPad>>? mirror;
    private Digest expected;
    public TableStore(Store store,string name,TsavoriteTuning tuning,long expectedCount,bool withMirror){
        this.Name=name;
        this.data=store.CreateTsavoriteSet<K4,Row<TPad>>(name,tuning,expectedCount);
        this.handle=this.data.Create();
        if(withMirror){
            this.mirror=[];
        }
    }
    public string Name{get;}
    public SetStore<K4,Row<TPad>> Data=>this.data;
    private Set<K4,Row<TPad>> Handle=>this.handle??throw new InvalidOperationException(this.Name+": the handle is released.");
    public bool TryGet(in K4 key,out Row<TPad> row){
        var found=this.Handle.TryGetValue(key,out row);
        if(this.mirror is not null){
            var inMirror=this.mirror.TryGetValue(key,out var model);
            if(found!=inMirror){
                throw new SpikeFailure($"{this.Name}: TryGet({key}) store={found} model={inMirror}");
            }
            if(found&&!Same(row,model)){
                throw new SpikeFailure($"{this.Name}: TryGet({key}) returned a different row (X={row.X},Y={row.Y} model X={model.X},Y={model.Y})");
            }
        }
        return found;
    }
    public Row<TPad> Get(in K4 key){
        if(!this.TryGet(key,out var row)){
            throw new SpikeFailure($"{this.Name}: {key} was not found.");
        }
        return row;
    }
    public void Insert(in Row<TPad> row){
        if(!this.Handle.Add(row)){
            throw new SpikeFailure($"{this.Name}: Add({row.K}) returned false.");
        }
        if(this.mirror is not null){
            if(!this.mirror.TryAdd(row.K,row)){
                throw new SpikeFailure($"{this.Name}: {row.K} already exists in the model.");
            }
            this.expected+=new Digest(1,Hashing.Of(row));
        }
    }
    /// <summary>TPC-E の TsavoriteTable.Update と同じ、Remove→Add の上書き。</summary>
    public void Replace(in Row<TPad> row){
        if(!this.Handle.Remove(row.K)){
            throw new SpikeFailure($"{this.Name}: Remove({row.K}) returned false in Replace.");
        }
        if(!this.Handle.Add(row)){
            throw new SpikeFailure($"{this.Name}: Add({row.K}) returned false in Replace.");
        }
        if(this.mirror is not null){
            if(!this.mirror.TryGetValue(row.K,out var old)){
                throw new SpikeFailure($"{this.Name}: {row.K} is not in the model.");
            }
            this.mirror[row.K]=row;
            this.expected+=new Digest(0,Hashing.Of(row)-Hashing.Of(old));
        }
    }
    public void Delete(in K4 key){
        if(!this.Handle.Remove(key)){
            throw new SpikeFailure($"{this.Name}: Remove({key}) returned false.");
        }
        if(this.mirror is not null){
            if(!this.mirror.Remove(key,out var old)){
                throw new SpikeFailure($"{this.Name}: {key} is not in the model.");
            }
            this.expected+=new Digest(-1,0-Hashing.Of(old));
        }
    }
    public void Release(){
        this.handle?.Dispose();
        this.handle=null;
    }
    public void Acquire(){
        this.handle??=this.data.Create();
    }
    public Digest Scan(){
        var digest=default(Digest);
        foreach(var row in this.Handle){
            digest+=new Digest(1,Hashing.Of(row));
        }
        return digest;
    }
    public Digest Expected{
        get{
            if(this.mirror is null){
                throw new InvalidOperationException(this.Name+" has no model.");
            }
            return this.expected;
        }
    }
    public long StoreCount()=>this.data.GetLongCount();
    public (long Begin,long Head,long ReadOnly,long Tail) Address=>this.data.Address;
    public void RebuildMirror(){
        if(this.mirror is null){
            return;
        }
        this.mirror.Clear();
        this.expected=default;
        foreach(var row in this.Handle){
            this.mirror.Add(row.K,row);
            this.expected+=new Digest(1,Hashing.Of(row));
        }
    }
    private static bool Same(in Row<TPad> left,in Row<TPad> right){
        return MemoryMarshal.AsBytes(new ReadOnlySpan<Row<TPad>>(in left)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<Row<TPad>>(in right)));
    }
    public void Dispose(){
        this.Release();
    }
}
public sealed class IndexStore:IPart{
    private readonly DictionaryStore<K4,V24> data;
    private Seamless.Collections.Tsavorite.IDictionary<K4,V24>? handle;
    private readonly Dictionary<K4,V24>? mirror;
    private Digest expected;
    public IndexStore(Store store,string name,TsavoriteTuning tuning,long expectedCount,bool withMirror){
        this.Name=name;
        this.data=store.CreateTsavorite<K4,V24>(name,tuning,expectedCount);
        this.handle=this.data.Create();
        if(withMirror){
            this.mirror=[];
        }
    }
    public string Name{get;}
    private Seamless.Collections.Tsavorite.IDictionary<K4,V24> Handle=>this.handle??throw new InvalidOperationException(this.Name+": the handle is released.");
    public bool TryGet(in K4 key,out V24 value){
        var found=this.Handle.TryGetValue(key,out value);
        if(this.mirror is not null){
            var inMirror=this.mirror.TryGetValue(key,out var model);
            if(found!=inMirror){
                throw new SpikeFailure($"{this.Name}: TryGet({key}) store={found} model={inMirror}");
            }
            if(found&&(value.A!=model.A||value.B!=model.B||value.C!=model.C)){
                throw new SpikeFailure($"{this.Name}: TryGet({key}) returned a different value.");
            }
        }
        return found;
    }
    /// <summary>追加または上書き(辞書の代入)。</summary>
    public void Put(in K4 key,in V24 value){
        this.Handle[key]=value;
        if(this.mirror is not null){
            if(this.mirror.TryGetValue(key,out var old)){
                this.expected+=new Digest(0,Hashing.Of(key,value)-Hashing.Of(key,old));
            }else{
                this.expected+=new Digest(1,Hashing.Of(key,value));
            }
            this.mirror[key]=value;
        }
    }
    /// <summary>辞書の Remove(TPC-E の found-bugs #2 で疑われた操作)。</summary>
    public bool Remove(in K4 key){
        var removed=this.Handle.Remove(key);
        if(this.mirror is not null){
            var inMirror=this.mirror.Remove(key,out var old);
            if(removed!=inMirror){
                throw new SpikeFailure($"{this.Name}: Remove({key}) store={removed} model={inMirror}");
            }
            if(inMirror){
                this.expected+=new Digest(-1,0-Hashing.Of(key,old));
            }
        }
        return removed;
    }
    public void Release(){
        this.handle?.Dispose();
        this.handle=null;
    }
    public void Acquire(){
        this.handle??=this.data.Create();
    }
    public Digest Scan(){
        var digest=default(Digest);
        foreach(var pair in this.Handle){
            var key=pair.Key;
            var value=pair.Value;
            digest+=new Digest(1,Hashing.Of(key,value));
        }
        return digest;
    }
    public Digest Expected{
        get{
            if(this.mirror is null){
                throw new InvalidOperationException(this.Name+" has no model.");
            }
            return this.expected;
        }
    }
    public long StoreCount()=>this.data.GetLongCount();
    public (long Begin,long Head,long ReadOnly,long Tail) Address=>this.data.Address;
    public void RebuildMirror(){
        if(this.mirror is null){
            return;
        }
        this.mirror.Clear();
        this.expected=default;
        foreach(var pair in this.Handle){
            var key=pair.Key;
            var value=pair.Value;
            this.mirror.Add(key,value);
            this.expected+=new Digest(1,Hashing.Of(key,value));
        }
    }
    public void Dispose(){
        this.Release();
    }
}
