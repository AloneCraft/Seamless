namespace Benchmark.Tpc.Core;
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
