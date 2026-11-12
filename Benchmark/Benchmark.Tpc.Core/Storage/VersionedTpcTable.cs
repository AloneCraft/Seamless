using Seamless.Collections;
using Seamless.Transactions.Collections;
namespace Benchmark.Tpc.Core;

/// <summary>既存TPC業務APIを共通scopeの私有変更へ接続する。Rowsの具体型をFLinqへ渡す。</summary>
public sealed class VersionedTpcTable<TKey,TRow>(VersionedTpcDomain domain,VersionedTransactionalTable<TKey,TRow> participant,TxnLog log,int tableId,string name):TableCore<TKey,TRow>(log,tableId,name)
    where TKey:unmanaged where TRow:struct,IRecord<TKey>{
    private TransactionCollection<TKey,TRow> Collection=>participant.For(domain.Current);
    public TransactionRows<TKey,TRow> Rows=>this.Collection.Rows;
    public override long Count=>global::System.Linq.Enumerable.LongCount(this.Rows);
    public override bool TryGet(TKey key,out TRow row)=>this.Collection.TryGetValue(key,out row);
    public override IEnumerable<TRow> EnumerateRows()=>this.Rows;
    protected override void InsertRaw(TKey key,TRow row){if(!this.Collection.Add(row)){throw new InvalidOperationException($"{this.Name}: duplicate key {key}.");}}
    protected override void ReplaceRaw(TKey key,TRow row)=>this.Collection[key]=row;
    protected override void DeleteRaw(TKey key){if(!this.Collection.Remove(key)){throw new KeyNotFoundException($"{this.Name}: missing key {key}.");}}
}
