using Seamless.Collections;
using Seamless.Collections.Tsavorite;
namespace Benchmark.TpcE.Core;
/// <summary>
/// Tsavorite の <c>Set&lt;TKey,TRow&gt;</c> に置いた表。<see cref="Rows"/> の静的な型が具体型なので、FLinq は表を直接回す。
/// 上書きの API が無いので、更新は Remove→Add で行う。
/// </summary>
public sealed class TsavoriteTable<TKey,TRow>:TableCore<TKey,TRow>,IHandleOwner
    where TKey:unmanaged
    where TRow:struct,IRecord<TKey>{
    private readonly SetStore<TKey,TRow> data;
    private Set<TKey,TRow>? handle;
    public TsavoriteTable(SetStore<TKey,TRow> data,TxnLog log,int tableId,string name):base(log,tableId,name){
        this.data=data;
        this.handle=data.Create();
    }
    /// <summary>FLinq の直接走査の起点。列挙順は不定。</summary>
    public Set<TKey,TRow> Rows=>this.Handle;
    public override long Count=>this.Handle.GetLongCount();
    private Set<TKey,TRow> Handle=>this.handle??throw new InvalidOperationException($"{this.Name}: the handle is released.");
    public override bool TryGet(TKey key,out TRow row){
        if(this.Handle.TryGetValue(key,out var found)){
            row=found;
            return true;
        }
        row=default;
        return false;
    }
    public override IEnumerable<TRow> EnumerateRows(){
        foreach(var row in this.Handle){
            yield return row;
        }
    }
    protected override void InsertRaw(TKey key,TRow row){
        if(!this.Handle.Add(row)){
            throw new InvalidOperationException($"{this.Name}: key {key} already exists.");
        }
    }
    protected override void ReplaceRaw(TKey key,TRow row){
        if(!this.Handle.Remove(key)){
            throw new KeyNotFoundException($"{this.Name}: key {key} was not found.");
        }
        if(!this.Handle.Add(row)){
            throw new InvalidOperationException($"{this.Name}: key {key} reappeared during replace.");
        }
    }
    protected override void DeleteRaw(TKey key){
        if(!this.Handle.Remove(key)){
            throw new KeyNotFoundException($"{this.Name}: key {key} was not found.");
        }
    }
    /// <summary>操作用オブジェクトを閉じる(保存で件数を記録させる・読み込み前に閉じる)。</summary>
    public void ReleaseHandle(){
        this.handle?.Dispose();
        this.handle=null;
    }
    public void AcquireHandle(){
        this.handle??=this.data.Create();
    }
    public void Dispose(){
        this.ReleaseHandle();
    }
}
/// <summary>Tsavorite の操作用オブジェクト(データ本体ごとに同時に 1 つ)を開け閉めできるもの。</summary>
public interface IHandleOwner:IDisposable{
    void ReleaseHandle();
    void AcquireHandle();
}
