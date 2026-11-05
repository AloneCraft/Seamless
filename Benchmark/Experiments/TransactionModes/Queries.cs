using Seamless.FLinq;
using Seamless.Collections.Tsavorite;

[assembly:LinqIntercept(false)]
namespace TransactionModes;

public readonly record struct GroupTotal(int Group,long Total);
public sealed class Queries{
    // リソースの寿命は通常の C# に任せ、生成対象は純粋な問い合わせに限定する。
    public static long Sum(Tx tx){using var lease=tx.Scan();return SumCore(tx);}
    public static int Count(Tx tx){using var lease=tx.Scan();return CountCore(tx);}
    public static bool Contains(Tx tx,int id){using var lease=tx.Scan();return ContainsCore(tx,id);}
    public static long First(Tx tx){using var lease=tx.Scan();return FirstCore(tx);}
    public static long Join(Tx tx){using var first=tx.Scan(0);using var second=tx.Scan(1);return JoinCore(tx);}
    public static GroupTotal[] Groups(Tx tx){using var lease=tx.Scan();return GroupsCore(tx,Comparer<int>.Default);}
    [LinqIntercept(true)]
    public static long SumCore(Tx tx){
        if(tx.PlainRows is{} plain){return plain.Where(row=>row.Group%2==0).Sum(row=>row.Value);}
        var result=tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0&&row.Group%2==0).Sum(row=>row.Value);
        foreach(var row in tx.Additional()){if(row.Group%2==0){result+=row.Value;}}
        return result;
    }
    [LinqIntercept(false)]
    public static long SumLinq(Tx tx){
        if(tx.PlainRows is{} plain){return plain.Where(row=>row.Group%2==0).Sum(row=>row.Value);}
        using var lease=tx.Scan();
        var result=tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0&&row.Group%2==0).Sum(row=>row.Value);
        foreach(var row in tx.Additional()){if(row.Group%2==0){result+=row.Value;}}
        return result;
    }
    [LinqIntercept(true)]
    public static int CountCore(Tx tx){
        if(tx.PlainRows is{} plain){return plain.Count;}
        return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Count()+tx.Additional().Count();
    }
    [LinqIntercept(true)]
    public static bool ContainsCore(Tx tx,int id){if(tx.PlainRows is{} plain){return plain.TryGetValue(id,out _);}return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Any(row=>row.Id==id)||tx.Additional().Any(row=>row.Id==id);}
    [LinqIntercept(true)]
    public static long FirstCore(Tx tx){return tx.Accesses[0].Rows.AsEnumerable().Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0&&row.Id==3).Select(row=>row.Value).FirstOrDefault();}
    [LinqIntercept(true)]
    public static long JoinCore(Tx tx){
        return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Concat(tx.Additional(0)).Join(tx.Accesses[1].Rows.Select(row=>tx.Visible(row,1)).Where(row=>row.Deleted==0).Concat(tx.Additional(1)),row=>row.Group,row=>row.Id,(left,right)=>left.Value+right.Value).Sum();
    }
    [LinqIntercept(false)]
    public static long JoinLinq(Tx tx){
        using var first=tx.Scan(0);using var second=tx.Scan(1);
        return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Concat(tx.Additional(0)).Join(tx.Accesses[1].Rows.Select(row=>tx.Visible(row,1)).Where(row=>row.Deleted==0).Concat(tx.Additional(1)),row=>row.Group,row=>row.Id,(left,right)=>left.Value+right.Value).Sum();
    }
    [LinqIntercept(true)]
    public static GroupTotal[] GroupsCore(Tx tx,IComparer<int> comparer){
        return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Concat(tx.Additional()).GroupBy(row=>row.Group).Select(group=>new GroupTotal(group.Key,group.Sum(row=>row.Value))).OrderBy(group=>group.Group,comparer).ToArray();
    }
    [LinqIntercept(false)]
    public static GroupTotal[] GroupsLinq(Tx tx){
        using var lease=tx.Scan();return tx.Accesses[0].Rows.Select(row=>tx.Visible(row,0)).Where(row=>row.Deleted==0).Concat(tx.Additional()).GroupBy(row=>row.Group).Select(group=>new GroupTotal(group.Key,group.Sum(row=>row.Value))).OrderBy(group=>group.Group).ToArray();
    }
    [LinqIntercept(true)]
    public static long PlainSum(Set<int,PlainRow> rows)=>rows.Where(row=>row.Group%2==0).Sum(row=>row.Value);
    [LinqIntercept(false)]
    public static long PlainSumLinq(Set<int,PlainRow> rows)=>rows.Where(row=>row.Group%2==0).Sum(row=>row.Value);
}
