using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
/// <summary>
/// 保存先に依らない DB の共通部分(取り消し記録、表と索引の一覧、索引の作り方)。表と索引の宣言は TpcEDatabaseBase.Tables.cs。
/// </summary>
public abstract partial class TpcEDatabaseBase:IDisposable{
    private readonly List<ITableView> tables=[];
    private readonly List<IIndexView> indexes=[];
    private readonly List<IDisposable> owned=[];
    private readonly List<IHandleOwner> handles=[];
    private readonly List<IBulkLoadable> bulkLoadables=[];
    private bool disposed;
    protected TpcEDatabaseBase(Store store,TpcEScale scale){
        this.Store=store;
        this.Scale=scale;
    }
    public Store Store{get;}
    public TpcEScale Scale{get;}
    public TxnLog Log{get;}=new();
    public IReadOnlyList<ITableView> Tables=>this.tables;
    public IReadOnlyList<IIndexView> Indexes=>this.indexes;
    protected int NextTableId=>this.tables.Count+1;
    protected TTable AddTable<TTable>(TTable table) where TTable:ITableView{
        this.tables.Add(table);
        if(table is IDisposable disposable){
            this.Own(disposable);
        }
        return table;
    }
    private T Own<T>(T resource) where T:IDisposable{
        this.owned.Add(resource);
        if(resource is IHandleOwner handleOwner){
            this.handles.Add(handleOwner);
        }
        return resource;
    }
    private OrderedIndex<TOwner,TEntry> CreateIndex<TOwner,TEntry>(string name)
        where TOwner:unmanaged
        where TEntry:unmanaged,IComparable<TEntry>{
        var headers=this.Own(this.CreateKeyValueStore<TOwner,IndexHeader>(name+".h"));
        var chunks=this.Own(this.CreateKeyValueStore<ChunkKey<TOwner>,IndexChunk<TEntry>>(name+".c"));
        var index=new OrderedIndex<TOwner,TEntry>(name,headers,chunks);
        this.indexes.Add(index);
        this.bulkLoadables.Add(index);
        return index;
    }
    protected abstract IKeyValueStore<TKey,TValue> CreateKeyValueStore<TKey,TValue>(string name)
        where TKey:unmanaged
        where TValue:unmanaged;
    /// <summary>初期投入を始める。索引は持ち主ごとの見出しと末尾のチャンクをメモリに溜め、<see cref="EndBulkLoad"/> でまとめて書く。</summary>
    public void BeginBulkLoad(){
        foreach(var item in this.bulkLoadables){
            item.BeginBulk();
        }
    }
    public void EndBulkLoad(){
        foreach(var item in this.bulkLoadables){
            item.EndBulk();
        }
    }
    /// <summary>Tsavorite の操作用オブジェクトを全部閉じる(保存で件数を記録させる経路を通すため)。Persisted では何もしない。</summary>
    public void ReleaseHandles(){
        foreach(var handle in this.handles){
            handle.ReleaseHandle();
        }
    }
    public void AcquireHandles(){
        foreach(var handle in this.handles){
            handle.AcquireHandle();
        }
    }
    public void Dispose(){
        if(this.disposed){
            return;
        }
        this.disposed=true;
        for(var index=this.owned.Count-1;index>=0;index--){
            this.owned[index].Dispose();
        }
        this.owned.Clear();
        GC.SuppressFinalize(this);
    }
}
/// <summary>表の行の変化を索引の更新につなぐ。</summary>
public static class IndexBinding{
    /// <summary>行から (持ち主, 要素) を作る関数で、表の挿入・更新・削除を索引へ反映する。同じ (持ち主, 要素) のままなら何もしない。</summary>
    public static void Bind<TKey,TRow,TOwner,TEntry>(TableCore<TKey,TRow> table,OrderedIndex<TOwner,TEntry> index,Func<TRow,(TOwner Owner,TEntry Entry)> select)
        where TKey:unmanaged
        where TRow:struct,Seamless.Collections.IRecord<TKey>
        where TOwner:unmanaged,IEquatable<TOwner>
        where TEntry:unmanaged,IComparable<TEntry>,IEquatable<TEntry>{
        table.AddHook((in TRow? before,in TRow? after)=>{
            (TOwner Owner,TEntry Entry)? old=null;
            if(before is TRow previous){
                old=select(previous);
            }
            (TOwner Owner,TEntry Entry)? next=null;
            if(after is TRow current){
                next=select(current);
            }
            if(old is {} o&&next is {} n&&o.Owner.Equals(n.Owner)&&o.Entry.Equals(n.Entry)){
                return;
            }
            if(old is {} removed){
                index.Remove(removed.Owner,removed.Entry);
            }
            if(next is {} added){
                index.Insert(added.Owner,added.Entry);
            }
        });
    }
}
/// <summary>時刻順に並ぶ ID(取引・保有の索引の要素)。同時刻は ID 順。</summary>
public readonly record struct TimedId(long Dts,long Id):IComparable<TimedId>{
    public int CompareTo(TimedId other){
        var dts=this.Dts.CompareTo(other.Dts);
        if(dts!=0){
            return dts;
        }
        return this.Id.CompareTo(other.Id);
    }
}
/// <summary>会社→競合の索引の要素。</summary>
public readonly record struct CompetitorEntry(long CompetitorId,Code8 IndustryId):IComparable<CompetitorEntry>{
    public int CompareTo(CompetitorEntry other){
        var competitor=this.CompetitorId.CompareTo(other.CompetitorId);
        if(competitor!=0){
            return competitor;
        }
        return this.IndustryId.CompareTo(other.IndustryId);
    }
}
/// <summary>会社名の 128 ビットのハッシュ(会社名→会社の索引の持ち主)。衝突に備え、引いた行の名前を必ず照合する。</summary>
public readonly record struct NameHash(ulong High,ulong Low){
    public static NameHash Of(string name){
        var hash=System.IO.Hashing.XxHash128.HashToUInt128(System.Text.Encoding.UTF8.GetBytes(name));
        return new((ulong)(hash>>64),(ulong)hash);
    }
}
