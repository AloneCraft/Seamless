using System.Collections;
using MemoryPack;
using Seamless.Collections;
using PersistedDictionary=Seamless.Collections.Persisted;
namespace Benchmark.Tpc.Core;
/// <summary>
/// Persisted の行の入れ物。Persisted には削除が無いので、削除は <see cref="Live"/>=false の行を追記して表す(後勝ち)。
/// </summary>
[MemoryPackable]
public partial struct Slot<T>{
    public bool Live;
    public T Row;
    public Slot(bool live,T row){
        this.Live=live;
        this.Row=row;
    }
}
/// <summary>
/// Persisted の <c>Dictionary&lt;TKey,Slot&lt;TRow&gt;&gt;</c> に置いた表。
/// 更新と削除はどちらも追記で、古いレコードはファイルに残る(列挙は全レコードを読み、最新の生きている行だけを返す)。
/// </summary>
public sealed class PersistedTable<TKey,TRow>:TableCore<TKey,TRow>,ILiveCounter
    where TKey:unmanaged
    where TRow:struct,IRecord<TKey>{
    private readonly PersistedDictionary.Dictionary<TKey,Slot<TRow>> data;
    private long live;
    public PersistedTable(PersistedDictionary.Dictionary<TKey,Slot<TRow>> data,TxnLog log,int tableId,string name):base(log,tableId,name){
        this.data=data;
    }
    /// <summary>生きている行の列。FLinq では汎用の起点になる。列挙順は不定(追記順)。</summary>
    public LiveRows<TKey,TRow> Rows=>new(this.data);
    public override long Count=>this.live;
    /// <summary>保存・開き直しで使う生存件数。Persisted の Count は削除済みのキーも数えるので自分で数える。</summary>
    public long LiveCount{
        get=>this.live;
        set=>this.live=value;
    }
    public string CounterName=>this.Name;
    public override bool TryGet(TKey key,out TRow row){
        if(this.data.TryGetValue(key,out var slot)&&slot.Live){
            row=slot.Row;
            return true;
        }
        row=default;
        return false;
    }
    public override IEnumerable<TRow> EnumerateRows(){
        foreach(var row in this.Rows){
            yield return row;
        }
    }
    protected override void InsertRaw(TKey key,TRow row){
        this.data.Add(key,new(true,row));
        this.live++;
    }
    protected override void ReplaceRaw(TKey key,TRow row){
        this.data.Add(key,new(true,row));
    }
    protected override void DeleteRaw(TKey key){
        this.data.Add(key,new(false,default!));
        this.live--;
    }
}
/// <summary>Persisted の表の生きている行を列挙する(削除済みを飛ばす)。順序なしの列として扱う。</summary>
public readonly struct LiveRows<TKey,TRow>(PersistedDictionary.Dictionary<TKey,Slot<TRow>> data):IUnorderedEnumerable<TRow>
    where TKey:unmanaged
    where TRow:struct{
    public Enumerator GetEnumerator()=>new(data.GetEnumerator());
    IEnumerator<TRow> IEnumerable<TRow>.GetEnumerator()=>this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public struct Enumerator(IEnumerator<KeyValuePair<TKey,Slot<TRow>>> inner):IEnumerator<TRow>{
        private TRow current;
        public readonly TRow Current=>this.current;
        readonly object IEnumerator.Current=>this.current;
        public bool MoveNext(){
            while(inner.MoveNext()){
                var slot=inner.Current.Value;
                if(slot.Live){
                    this.current=slot.Row;
                    return true;
                }
            }
            return false;
        }
        public void Reset()=>throw new NotSupportedException();
        public readonly void Dispose()=>inner.Dispose();
    }
}
/// <summary>保存と一緒に記録する生存件数を持つもの。</summary>
public interface ILiveCounter{
    string CounterName{get;}
    long LiveCount{get;set;}
}
