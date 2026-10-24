using Store=Seamless.Execution.Store;
namespace Benchmark.Tpc.Core;
/// <summary>
/// 保存先に依らない DB の共通部分(取り消し記録、表と索引の一覧、索引の作り方、操作用オブジェクトの開け閉め)。
/// 表と索引の宣言はベンチマークごとの派生クラスが持つ。
/// </summary>
public abstract class TpcDatabaseBase:IDisposable{
    private readonly List<ITableView> tables=[];
    private readonly List<IIndexView> indexes=[];
    private readonly List<IDisposable> owned=[];
    private readonly List<IHandleOwner> handles=[];
    private readonly List<IBulkLoadable> bulkLoadables=[];
    private bool disposed;
    protected TpcDatabaseBase(Store store){
        this.Store=store;
    }
    public Store Store{get;}
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
    protected OrderedIndex<TOwner,TEntry> CreateIndex<TOwner,TEntry>(string name)
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
