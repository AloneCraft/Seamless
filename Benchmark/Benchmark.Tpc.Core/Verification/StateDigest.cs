using System.Text;
namespace Benchmark.Tpc.Core;
/// <summary>
/// 表 1 つのダイジェスト。件数と、各行の正規化バイト列の XxHash128 の和(mod 2^128)。和なので列挙の順序に依らず、重複にも反応する。
/// </summary>
public sealed class TableDigestBuilder(string name){
    private readonly CanonicalBuffer buffer=new();
    private readonly byte[] prefix=Encoding.UTF8.GetBytes(name+"\0");
    public string Name{get;}=name;
    public long Count{get;private set;}
    public UInt128 Sum{get;private set;}
    public void Add(object? value){
        this.buffer.Clear();
        this.buffer.WriteRaw(this.prefix);
        CanonicalWriter.Write(this.buffer,value);
        this.Sum+=this.buffer.Hash();
        this.Count++;
    }
    public TableDigest Build()=>new(this.Name,this.Count,this.Sum);
}
/// <summary>表または索引 1 つのダイジェスト。</summary>
public readonly record struct TableDigest(string Name,long Count,UInt128 Sum){
    public override string ToString()=>$"{this.Name}: count={this.Count} sum={this.Sum:X32}";
}
/// <summary>DB 全体のダイジェスト(表と索引ごと)。</summary>
public sealed record DatabaseDigest(IReadOnlyList<TableDigest> Tables,IReadOnlyList<TableDigest> Indexes){
    /// <summary>全体を 1 つにまとめた値(表示用)。</summary>
    public UInt128 Combined{
        get{
            var buffer=new CanonicalBuffer();
            foreach(var table in this.Tables){
                buffer.WriteString(table.Name);
                buffer.WriteInt64(table.Count);
                buffer.WriteUInt64((ulong)(table.Sum>>64));
                buffer.WriteUInt64((ulong)table.Sum);
            }
            foreach(var index in this.Indexes){
                buffer.WriteString(index.Name);
                buffer.WriteInt64(index.Count);
                buffer.WriteUInt64((ulong)(index.Sum>>64));
                buffer.WriteUInt64((ulong)index.Sum);
            }
            return buffer.Hash();
        }
    }
    /// <summary>食い違う表と索引の名前。</summary>
    public IReadOnlyList<string> Differences(DatabaseDigest other){
        var result=new List<string>();
        Compare(this.Tables,other.Tables,result);
        Compare(this.Indexes,other.Indexes,result);
        return result;
    }
    private static void Compare(IReadOnlyList<TableDigest> left,IReadOnlyList<TableDigest> right,List<string> result){
        var map=new System.Collections.Generic.Dictionary<string,TableDigest>(StringComparer.Ordinal);
        foreach(var item in right){
            map[item.Name]=item;
        }
        foreach(var item in left){
            if(!map.TryGetValue(item.Name,out var counterpart)||counterpart!=item){
                result.Add(item.Name);
            }
            map.Remove(item.Name);
        }
        foreach(var name in map.Keys){
            result.Add(name);
        }
    }
}
/// <summary>DB のダイジェストを取る・食い違う行を探す。</summary>
public static class StateDigest{
    public static DatabaseDigest Of(TpcDatabaseBase db){
        var tables=new List<TableDigest>(db.Tables.Count);
        foreach(var table in db.Tables){
            var builder=new TableDigestBuilder(table.Name);
            foreach(var row in table.EnumerateRowsBoxed()){
                builder.Add(row);
            }
            if(builder.Count!=table.Count){
                // 列挙した件数と表の件数が違うのは、保存先の件数の不具合(またはベンチの数え違い)。
                tables.Add(new(table.Name+"#count-mismatch",table.Count,builder.Sum));
            }
            tables.Add(builder.Build());
        }
        var indexes=new List<TableDigest>(db.Indexes.Count);
        foreach(var index in db.Indexes){
            var builder=new TableDigestBuilder(index.Name);
            foreach(var (owner,entry) in index.EnumerateEntriesBoxed()){
                builder.Add(new IndexItem(owner,entry));
            }
            indexes.Add(builder.Build());
        }
        return new(tables,indexes);
    }
    /// <summary>
    /// 2 つの DB の同じ表を突き合わせ、食い違う行を主キーの正規化バイト列の小さい順に最大 limit 件返す。
    /// 片方を列挙してもう片方を主キーで引くので、全件をメモリに載せない。
    /// </summary>
    public static IReadOnlyList<RowDifference> DifferentRows(ITableView left,ITableView right,int limit){
        var found=new SortedList<string,RowDifference>(StringComparer.Ordinal);
        void Keep(object key,object? leftRow,object? rightRow){
            var sortKey=Convert.ToHexString(CanonicalWriter.ToBytes(key));
            if(found.ContainsKey(sortKey)){
                return;
            }
            found.Add(sortKey,new(left.Name,key,leftRow,rightRow));
            if(found.Count>limit){
                found.RemoveAt(found.Count-1);
            }
        }
        foreach(var row in left.EnumerateRowsBoxed()){
            var key=left.KeyOf(row);
            if(!right.TryGetBoxed(key,out var other)){
                Keep(key,row,null);
            }else if(!CanonicalWriter.ToBytes(row).AsSpan().SequenceEqual(CanonicalWriter.ToBytes(other))){
                Keep(key,row,other);
            }
        }
        foreach(var row in right.EnumerateRowsBoxed()){
            var key=right.KeyOf(row);
            if(!left.TryGetBoxed(key,out _)){
                Keep(key,null,row);
            }
        }
        return [..found.Values];
    }
}
/// <summary>索引の論理的な 1 要素(持ち主, 要素)。チャンクの分け方に依らない。</summary>
public readonly record struct IndexItem(object Owner,object Entry);
/// <summary>2 つの DB で食い違った行。片方に無ければ null。</summary>
public sealed record RowDifference(string Table,object Key,object? Left,object? Right);
