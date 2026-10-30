namespace Benchmark.TpcC.Core;
/// <summary>
/// TPC-C の DB 全体のダイジェスト。表と索引の名前から区画の札(p0- など)を外し、区画をまたいで行ごとのハッシュの和を取るので、
/// 区画の数 P に依らない(P=1 と P=4 で同じデータなら同じ値)。ITEM は区画ごとの複製なので区画 0 の分だけを数え、
/// 複製どうしの一致は <see cref="ItemReplicaDifferences"/> で別に確かめる。
/// </summary>
public static class TpcCStateDigest{
    public static DatabaseDigest Of(ITpcCDatabase db){
        var first=db.PartitionBases[0];
        var tables=new List<TableDigest>(first.Tables.Count);
        for(var index=0;index<first.Tables.Count;index++){
            var logical=Logical(first.Tables[index].Name);
            var builder=new TableDigestBuilder(logical);
            long reported=0;
            foreach(var partition in db.PartitionBases){
                if(logical=="item"&&partition.Partition!=0){
                    continue;
                }
                var table=partition.Tables[index];
                reported+=table.Count;
                foreach(var row in table.EnumerateRowsBoxed()){
                    builder.Add(row);
                }
            }
            if(builder.Count!=reported){
                // 列挙した件数と表の件数が違うのは、保存先の件数の不具合(またはベンチの数え違い)。
                tables.Add(new(logical+"#count-mismatch",reported,builder.Sum));
            }
            tables.Add(builder.Build());
        }
        var indexes=new List<TableDigest>(first.Indexes.Count);
        for(var index=0;index<first.Indexes.Count;index++){
            var builder=new TableDigestBuilder(Logical(first.Indexes[index].Name));
            foreach(var partition in db.PartitionBases){
                foreach(var (owner,entry) in partition.Indexes[index].EnumerateEntriesBoxed()){
                    builder.Add(new IndexItem(owner,entry));
                }
            }
            indexes.Add(builder.Build());
        }
        return new DatabaseDigest(tables,indexes);
    }
    /// <summary>ITEM の複製が区画 0 と食い違っている区画の番号。</summary>
    public static IReadOnlyList<int> ItemReplicaDifferences(ITpcCDatabase db){
        var reference=ItemDigest(db.PartitionBases[0]);
        var result=new List<int>();
        for(var partition=1;partition<db.PartitionBases.Count;partition++){
            if(ItemDigest(db.PartitionBases[partition])!=reference){
                result.Add(partition);
            }
        }
        return result;
    }
    private static TableDigest ItemDigest(TpcCPartitionBase partition){
        var builder=new TableDigestBuilder("item");
        foreach(var row in partition.Item.EnumerateRows()){
            builder.Add(row);
        }
        return builder.Build();
    }
    /// <summary>"p3-customer" → "customer"。</summary>
    private static string Logical(string name)=>name[(name.IndexOf('-')+1)..];
}
